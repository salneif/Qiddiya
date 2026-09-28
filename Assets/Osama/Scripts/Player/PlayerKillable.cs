using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Death/respawn handler for the player, Little Nightmares style.
/// Attach it to the player object WITHOUT touching the movement script
/// (the movement controller is owned by someone else).
///
/// On <see cref="Kill"/>:
///  - Disables any movement/input scripts listed in <see cref="disableOnDeath"/>.
///  - Plays the burn/dissolve death effect (if assigned).
///  - Fires <see cref="onDeath"/> (sound / black screen / camera shake...).
///  - Teleports the player to the current respawn point and reforms it alive.
///
/// The respawn point can be updated at runtime by a checkpoint via
/// <see cref="SetRespawnPoint"/>.
/// </summary>
public class PlayerKillable : MonoBehaviour
{
    [Header("Disabled on death")]
    [Tooltip("Movement/input scripts to disable while dead. Re-enabled on respawn.")]
    [SerializeField] private Behaviour[] disableOnDeath;

    [Header("Death effect (burn)")]
    [Tooltip("Optional DeathDissolveEffect on the same player for a fiery death.")]
    [SerializeField] private DeathDissolveEffect deathEffect;

    [Header("Events")]
    [Tooltip("Invoked once the moment the player dies.")]
    [SerializeField] private UnityEvent onDeath;
    [Tooltip("Invoked the moment the player respawns.")]
    [SerializeField] private UnityEvent onRespawn;

    [Header("Respawn")]
    [Tooltip("Automatically respawn the player after death.")]
    [SerializeField] private bool autoRespawn = true;
    [Tooltip("How long the player stays gone after burning, before returning (seconds).")]
    [SerializeField] private float respawnDelay = 0.6f;
    [Tooltip("Current respawn point. Updated by checkpoints at runtime. " +
             "If null, the player returns to its start position.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("لا يُقتل خلال هذي المدّة بعد عودته — وإلا قتله ما قتله أوّل مرّة قبل " +
             "أن يتحرّك. صفر يُلغيها")]
    [SerializeField] private float safeAfterRespawn = 1.4f;
    [Tooltip("يتذكّر آخر أرضٍ وقف عليها، ويعود إليها إن لم يمرّ بنقطة حفظ بعد")]
    [SerializeField] private bool rememberLastGround = true;

    /// <summary>Is the player currently dead? (used by enemies to avoid double-kills)</summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// يُطلق لحظة الموت، أيًّا كان سببه. ساكنٌ عمدًا: من يريد أن يردّ على الموت —
    /// كاهتزاز اليد — يستمع مرّة، بدل أن يُربط في <c>On Death</c> في كل سين ويُنسى
    /// في واحد منها.
    /// </summary>
    public static event System.Action Died;

    /// <summary>يونيتي يُبقي المشتركين بين تشغيلتين في المحرر.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Died = null;

    /// <summary>The current respawn point (last checkpoint), or null.</summary>
    public Transform RespawnPoint => respawnPoint;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private float safeUntil;
    private Vector3 lastGround;
    private bool hasLastGround;
    private float groundTimer;
    private CharacterController groundSource;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        groundSource = GetComponent<CharacterController>();
        if (deathEffect == null)
            deathEffect = GetComponent<DeathDissolveEffect>();

        StartCoroutine(CaptureStart());
    }

    /// <summary>
    /// موضع البداية يُلتقط بعد أوّل إطار لا في <c>Awake</c>.
    ///
    /// <c>PlayerSpawnRouter</c> ينقل اللاعب إلى مدخل السين في <c>Start</c>، فالموضع
    /// الملتقط قبله هو الذي وُضع في السين لا الذي يقف فيه اللاعب فعلًا — ومن مات بلا
    /// نقطة حفظ عاد إلى مكانٍ لم يره قطّ.
    /// </summary>
    private IEnumerator CaptureStart()
    {
        yield return null;

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    /// <summary>
    /// يتذكّر آخر أرضٍ وقف عليها ساكنًا لحظة.
    ///
    /// شبكة أمانٍ لمن لم يمرّ بنقطة حفظ بعد: أقرب نقطةٍ في السيرك تبعد عن كمين
    /// المهرّج أكثر من مئة متر، فالعودة إليها عقوبةٌ لا استئناف.
    /// </summary>
    private void Update()
    {
        if (IsDead || !rememberLastGround) return;
        if (groundSource != null && !groundSource.isGrounded) { groundTimer = 0f; return; }

        groundTimer += Time.deltaTime;
        if (groundTimer < 0.6f) return;

        groundTimer = 0f;
        lastGround = transform.position;
        hasLastGround = true;
    }

    /// <summary>Sets the last checkpoint the player will respawn at.</summary>
    public void SetRespawnPoint(Transform point)
    {
        if (point != null)
            respawnPoint = point;
    }

    /// <summary>Kills the player: stops control, burns the body, then returns it to the checkpoint.</summary>
    public void Kill()
    {
        if (IsDead) return;
        if (Time.time < safeUntil) return;   // عاد للتوّ — لا يُقتل قبل أن يتحرّك
        IsDead = true;

        SetControlEnabled(false);
        onDeath?.Invoke();
        Died?.Invoke();

        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    /// <summary>Instantly respawns the player at the checkpoint and restores control.</summary>
    public void Respawn()
    {
        StopAllCoroutines();
        MoveToSpawn();
        if (deathEffect != null) deathEffect.ResetImmediate();
        IsDead = false;
        SetControlEnabled(true);
        onRespawn?.Invoke();
    }

    private IEnumerator DeathRoutine()
    {
        // 1) Burn away
        if (deathEffect != null)
            yield return deathEffect.PlayDeath();

        if (!autoRespawn)
            yield break;

        // 2) Stay gone for a moment
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        // 3) Teleport to the checkpoint (while invisible)
        MoveToSpawn();

        // 4) Reform from the ashes
        if (deathEffect != null)
            yield return deathEffect.PlayReform();

        // 5) Give control back
        IsDead = false;
        SetControlEnabled(true);
        onRespawn?.Invoke();
    }

    private void MoveToSpawn()
    {
        Vector3 pos;
        Quaternion rot;

        if (respawnPoint != null)
        {
            pos = respawnPoint.position;
            rot = respawnPoint.rotation;
        }
        else if (rememberLastGround && hasLastGround)
        {
            pos = lastGround;                 // آخر أرضٍ وقف عليها، أقرب من بداية السين
            rot = transform.rotation;
        }
        else
        {
            pos = startPosition;
            rot = startRotation;
        }

        TeleportTo(pos, rot);
        safeUntil = Time.time + Mathf.Max(0f, safeAfterRespawn);
    }

    /// <summary>
    /// Safe teleport: temporarily disables the CharacterController so it does not
    /// fight the position change.
    /// </summary>
    private void TeleportTo(Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// يجمّد تحكّم اللاعب بلا قتله — يستخدمه <see cref="LevelPortal"/> لحظة الانتقال
    /// حتى لا يكمل اللاعب مشيه أثناء التعتيم فيتعدّى الباب. يعتمد نفس قائمة
    /// <see cref="disableOnDeath"/> المضبوطة أصلًا، فلا يحتاج ضبطًا جديدًا.
    /// </summary>
    public void FreezeControl() => SetControlEnabled(false);

    /// <summary>يرجّع التحكّم بعد التجميد. لا يفعل شيئًا واللاعب ميت — الموت يملك التحكّم.</summary>
    public void UnfreezeControl()
    {
        if (!IsDead) SetControlEnabled(true);
    }

    private void SetControlEnabled(bool value)
    {
        if (disableOnDeath == null) return;
        foreach (var b in disableOnDeath)
            if (b != null) b.enabled = value;
    }
}

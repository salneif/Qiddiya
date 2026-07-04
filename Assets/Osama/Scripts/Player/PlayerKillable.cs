using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// مكوّن الموت للاعب بأسلوب Little Nightmares — يُركّب على كائن اللاعب
/// <b>دون تعديل سكربت حركته</b> (فسكربت الحركة ليس لنا).
///
/// عند <see cref="Kill"/>:
///  - يوقف أي سكربتات حركة تحدّدها في <see cref="disableOnDeath"/> (مثل سكربتات علي).
///  - يشغّل مؤثر الاحتراق (إن وُجد) فتتفتّت الشخصية بالنار.
///  - يشغّل حدث <see cref="onDeath"/> (لصوت/شاشة سوداء/اهتزاز...).
///  - ثم ينقل اللاعب إلى <see cref="respawnPoint"/> (تحدّده أنت) ويعيد تجسّده حيًّا.
///
/// العدو (RobotEyeAttack) هو من ينادي Kill() عند رؤيته للّاعب مكشوفًا.
/// </summary>
public class PlayerKillable : MonoBehaviour
{
    [Header("ما يُعطّل لحظة الموت")]
    [Tooltip("اسحب هنا سكربتات الحركة/الإدخال التي تريد إيقافها عند الموت " +
             "(مثل سكربتات التحكم). تُعاد تلقائيًا عند الإحياء.")]
    [SerializeField] private Behaviour[] disableOnDeath;

    [Header("مؤثر الموت (احتراق)")]
    [Tooltip("مكوّن DeathDissolveEffect على نفس اللاعب — اختياري. يعطي احتراقًا ناريًا عند الموت.")]
    [SerializeField] private DeathDissolveEffect deathEffect;

    [Header("أحداث")]
    [Tooltip("يُستدعى مرة واحدة لحظة الموت")]
    [SerializeField] private UnityEvent onDeath;
    [Tooltip("يُستدعى لحظة الإحياء")]
    [SerializeField] private UnityEvent onRespawn;

    [Header("الإحياء")]
    [Tooltip("إعادة اللاعب تلقائيًا بعد الموت")]
    [SerializeField] private bool autoRespawn = true;
    [Tooltip("مدة بقاء اللاعب مختفيًا بعد الاحتراق قبل أن يعود (ثواني)")]
    [SerializeField] private float respawnDelay = 0.6f;
    [Tooltip("نقطة الإحياء التي تحدّدها أنت — أنشئ Empty GameObject في المكان الذي تريد " +
             "أن يعود إليه اللاعب واسحبه هنا. إذا تُركت فارغة يعود لمكانه عند بداية اللعبة.")]
    [SerializeField] private Transform respawnPoint;

    /// <summary>هل اللاعب ميّت حاليًا؟ (يستخدمها العدو لتجنّب القتل المكرر)</summary>
    public bool IsDead { get; private set; }

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        if (deathEffect == null)
            deathEffect = GetComponent<DeathDissolveEffect>();
    }

    /// <summary>يقتل اللاعب: يوقف التحكم، يحرق الشخصية، ثم يعيدها لنقطة الإحياء.</summary>
    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        SetControlEnabled(false);
        onDeath?.Invoke();

        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    /// <summary>إحياء فوري (يدوي) لنقطة الإحياء وإرجاع التحكم.</summary>
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
        // 1) الاحتراق: تتفتّت الشخصية بالنار
        if (deathEffect != null)
            yield return deathEffect.PlayDeath();

        if (!autoRespawn)
            yield break;

        // 2) تبقى مختفية لحظة (شعور بالموت)
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        // 3) النقل إلى نقطة الإحياء (وهي مختفية)
        MoveToSpawn();

        // 4) إعادة التجسّد من النار
        if (deathEffect != null)
            yield return deathEffect.PlayReform();

        // 5) رجوع التحكم
        IsDead = false;
        SetControlEnabled(true);
        onRespawn?.Invoke();
    }

    private void MoveToSpawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRotation;
        TeleportTo(pos, rot);
    }

    /// <summary>
    /// نقل آمن للاعب: يعطّل CharacterController لحظة النقل حتى لا يقاوم تغيير الموضع.
    /// </summary>
    private void TeleportTo(Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
    }

    private void SetControlEnabled(bool value)
    {
        if (disableOnDeath == null) return;
        foreach (var b in disableOnDeath)
            if (b != null) b.enabled = value;
    }
}

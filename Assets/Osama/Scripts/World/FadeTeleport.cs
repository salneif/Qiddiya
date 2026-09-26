using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// نقل اللاعب داخل نفس السين مع تعتيم — ينتقل <b>من النقطة التي لمس فيها المنطقة</b>
/// بالضبط، لا بعد ما يتعدّاها.
///
/// السر في الترتيب: يجمّد اللاعب <b>أولًا</b> ثم يعتّم ثم ينقله. تعطيل الإدخال وحده
/// لا يكفي — الشخصية تكمل انزلاقها أثناء التعتيم فتبدو كأنها تجاوزت نقطة النقل ثم
/// اختفت. التجميد هنا يوقف سكربت الحركة كاملًا عبر <see cref="PlayerKillable"/>
/// (نفس قائمة Disable On Death المضبوطة أصلًا).
///
/// التركيب: كولايدر (Is Trigger) عند نقطة النقل + هذا السكربت + <see cref="destination"/>.
/// التعتيم: إمّا <see cref="fader"/> (يُلتقط من السين تلقائيًا) أو
/// <see cref="fadeOverlay"/> لو عندك CanvasGroup أسود جاهز.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FadeTeleport : MonoBehaviour
{
    [Header("الوجهة")]
    [Tooltip("النقطة التي يظهر فيها اللاعب")]
    [SerializeField] private Transform destination;
    [Tooltip("يأخذ دوران نقطة الوصول أيضًا — ليخرج متجهًا للطريق الصحيح")]
    [SerializeField] private bool useDestinationRotation = false;

    [Header("التعتيم")]
    [Tooltip("مموّه الشاشة — يُلتقط من السين تلقائيًا إذا تُرك فارغًا")]
    [SerializeField] private ScreenFader fader;
    [Tooltip("بديل: CanvasGroup أسود يغطي الشاشة (نفس الذي يستخدمه تيليبورت الخيمة)")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [Tooltip("مدة التعتيم بالثواني (تُستخدم مع CanvasGroup فقط؛ ScreenFader له مدته)")]
    [SerializeField] private float fadeDuration = 0.25f;
    [Tooltip("مدة البقاء على السواد بعد النقل — تخفي القفزة تمامًا")]
    [SerializeField] private float holdDelay = 0.15f;

    [Header("التفاعل")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("مهلة قبل أن تعمل أي نقطة نقل من جديد — تمنع النقل ذهابًا وإيابًا")]
    [SerializeField] private float reentryCooldown = 0.5f;
    [Tooltip("سكربتات إضافية تتعطّل أثناء النقل (متابعة الكاميرا مثلًا) — اختياري")]
    [SerializeField] private MonoBehaviour[] disableWhileTeleporting;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت لحظة النقل")]
    [SerializeField] private AudioClip teleportSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("أحداث")]
    [Tooltip("لحظة لمس اللاعب للمنطقة (قبل التعتيم)")]
    public UnityEvent onTeleportStarted;
    [Tooltip("بعد وصول اللاعب وانتهاء التعتيم")]
    public UnityEvent onTeleportFinished;

    /// <summary>نقل واحد في كل لحظة مهما تعددت النقاط.</summary>
    private static bool busy;
    private static float cooldownUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        busy = false;
        cooldownUntil = 0f;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (fader == null && fadeOverlay == null) fader = FindFirstObjectByType<ScreenFader>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (destination == null)
            Debug.LogWarning("[FadeTeleport] ما في وجهة — لن ينقل أحدًا.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (busy || Time.time < cooldownUntil) return;
        if (destination == null || !other.CompareTag(playerTag)) return;

        StartCoroutine(Sequence(other.transform));
    }

    private IEnumerator Sequence(Transform player)
    {
        busy = true;

        // التجميد أولًا: هنا بالضبط يقف اللاعب، فلا يتعدّى نقطة النقل أثناء التعتيم
        var killable = player.GetComponentInParent<PlayerKillable>();
        if (killable != null) killable.FreezeControl();
        SetExtrasEnabled(false);

        Play(teleportSound);
        onTeleportStarted?.Invoke();

        yield return FadeTo(1f);

        Move(player);

        if (holdDelay > 0f) yield return new WaitForSeconds(holdDelay);

        yield return FadeTo(0f);

        if (killable != null) killable.UnfreezeControl();
        SetExtrasEnabled(true);

        onTeleportFinished?.Invoke();
        cooldownUntil = Time.time + reentryCooldown;
        busy = false;
    }

    /// <summary>ينقل اللاعب — مع تعطيل الـ CharacterController لحظيًا وإلا قاوم النقل.</summary>
    private void Move(Transform player)
    {
        var controller = player.GetComponentInParent<CharacterController>();
        Transform body = controller != null ? controller.transform : player;

        if (controller != null) controller.enabled = false;

        if (useDestinationRotation) body.SetPositionAndRotation(destination.position, destination.rotation);
        else body.position = destination.position;

        if (controller != null) controller.enabled = true;
    }

    /// <summary>يعتّم أو يكشف — بـ ScreenFader إن وُجد، وإلا بالـ CanvasGroup.</summary>
    private IEnumerator FadeTo(float target)
    {
        if (fader != null)
        {
            bool done = false;
            if (target >= 1f) fader.FadeOut(() => done = true);
            else { fader.FadeIn(); done = true; }

            float guard = 0f;
            while (!done && guard < 5f) { guard += Time.deltaTime; yield return null; }
            yield break;
        }

        if (fadeOverlay == null) yield break;

        float from = fadeOverlay.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        fadeOverlay.alpha = target;
    }

    private void SetExtrasEnabled(bool value)
    {
        if (disableWhileTeleporting == null) return;
        foreach (var b in disableWhileTeleporting)
            if (b != null) b.enabled = value;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, soundVolume);
    }

    private void OnDrawGizmos()
    {
        if (destination == null) return;

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.3f);
    }
}

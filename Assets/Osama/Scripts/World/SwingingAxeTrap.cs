using System.Collections;
using UnityEngine;

/// <summary>
/// فخ الفاس المتأرجح بأسلوب سكايرم: يتأرجح مثل البندول يمينًا ويسارًا حول نقطة تعليق ثابتة
/// بحركة جيبية مستمرة وسلسة (أسرع ما يكون عند منتصف التأرجح، وأبطأ عند طرفيه).
///
/// التركيب في المشهد:
///  - كائن فارغ (Pivot) يوضع عند نقطة تعليق الفاس (أعلى الذراع/السلسلة) — هذا السكربت يُركّب عليه.
///  - الفاس نفسه (الموديل + Collider) يكون Child من الـ Pivot، بإزاحة لأسفل بمقدار طول الذراع.
///  - فعّل Is Trigger على كولايدر الفاس، وأضف عليه مكوّن <see cref="LavaKill"/> (killDelay = 0,
///    triggerOnce = false) — هو أصلاً "منطقة قتل عند اللمس" عامة، فيقتل اللاعب فورًا عبر
///    PlayerKillable.Kill() بدون أي كود إضافي.
///
/// لعمل ممر فيه عدة فاسات (مثل سكايرم): كرر Pivot+فاس على طول الممر، وغيّر
/// <see cref="phaseOffsetDegrees"/> لكل واحد (مثلاً 0 / 120 / 240) حتى تتبادل توقيت
/// انكشاف كل فاس، فيحتاج اللاعب يوقف ويمشي بين كل فاس وفاس بدل ما يعبر دفعة وحدة.
/// </summary>
public class SwingingAxeTrap : MonoBehaviour
{
    [Header("محور التأرجح")]
    [Tooltip("المحور المحلي الذي يدور حوله الفاس. الافتراضي forward (محور العمق) يعطي تأرجحًا " +
             "يمين/يسار في مستوى الشاشة — مناسب للّعبة الجانبية 2.5D.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Header("حركة البندول")]
    [Tooltip("أقصى زاوية ميل عن وضع السكون لكل جهة (درجات)")]
    [Range(5f, 90f)]
    [SerializeField] private float swingAmplitude = 55f;
    [Tooltip("مدة الدورة الكاملة يمين-يسار-يمين (ثواني) — أصغر = أسرع وأخطر")]
    [SerializeField] private float cycleDuration = 2.2f;
    [Tooltip("إزاحة الطور بالدرجات (0-360) لتبديل توقيت هذا الفاس عن باقي فاسات الممر. " +
             "مثال لثلاث فاسات متبادلة: 0 / 120 / 240.")]
    [SerializeField] private float phaseOffsetDegrees = 0f;

    [Header("التشغيل والإطفاء (الرافعة)")]
    [Tooltip("الفاس يبدأ شغّالًا؟")]
    [SerializeField] private bool startRunning = true;
    [Tooltip("مدة فقدان التأرجح حتى يسكن الفاس معلّقًا (ثواني). " +
             "غيّرها بين الفاسات الثلاث (1 / 1.5 / 2) فتسكن واحدًا بعد الآخر بدل دفعة وحدة.")]
    [SerializeField] private float stopDuration = 1.5f;
    [Tooltip("مكوّن القتل على الفاس — يتعطّل تلقائيًا عندما يهدأ التأرجح، فيصير الفاس " +
             "الساكن مجرّد ديكور تعدي من جنبه. اتركه فارغًا ليُلتقط LavaKill من الأبناء تلقائيًا.")]
    [SerializeField] private Behaviour killComponent;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت 'شووش' يُشغَّل كل ما عبر الفاس منتصف تأرجحه (أسرع نقطة) — تنبيه سمعي للاعب")]
    [SerializeField] private AudioClip whooshSound;

    [Header("تصحيح (Scene فقط)")]
    [Tooltip("طول الذراع التقريبي لرسم قوس التأرجح في نافذة Scene — لا يؤثر على اللعب")]
    [SerializeField] private float gizmoArmLength = 2f;

    /// <summary>أقل مدى تأرجح يظل الفاس عنده قاتلًا — تحته يُعتبر ساكنًا وغير مؤذٍ.</summary>
    private const float DeadlyThreshold = 0.2f;

    private Quaternion baseRotation;
    private float lastAngle;
    private float amplitudeScale;   // 1 = تأرجح كامل، 0 = ساكن معلّق للأسفل
    private Coroutine damping;

    /// <summary>هل الفاس شغّال (يتأرجح)؟</summary>
    public bool IsRunning { get; private set; }

    private void Awake()
    {
        baseRotation = transform.localRotation;
        // الفاس القاتل ابن للـ Pivot، فنلتقط مكوّن القتل منه بلا ربط يدوي
        if (killComponent == null) killComponent = GetComponentInChildren<LavaKill>(true);
        IsRunning = startRunning;
        amplitudeScale = startRunning ? 1f : 0f;
        lastAngle = CurrentAngle();
        SyncKillComponent();
    }

    /// <summary>يطفئ الفاس: يفقد تأرجحه تدريجيًا حتى يسكن — اربطه بـ WorldLever.onActivated.</summary>
    public void TurnOff() => SetRunning(false);

    /// <summary>يعيد تشغيل الفاس تدريجيًا.</summary>
    public void TurnOn() => SetRunning(true);

    /// <summary>يبدّل حالة الفاس.</summary>
    public void Toggle() => SetRunning(!IsRunning);

    /// <summary>يضبط حالة الفاس صراحةً.</summary>
    public void SetRunning(bool on)
    {
        if (IsRunning == on) return;
        IsRunning = on;

        if (damping != null) StopCoroutine(damping);
        damping = StartCoroutine(DampTo(on ? 1f : 0f));
    }

    private IEnumerator DampTo(float target)
    {
        float start = amplitudeScale;
        float t = 0f;
        while (t < stopDuration)
        {
            t += Time.deltaTime;
            float k = stopDuration > 0f ? Mathf.Clamp01(t / stopDuration) : 1f;
            amplitudeScale = Mathf.Lerp(start, target, k);
            yield return null;
        }
        amplitudeScale = target;
        damping = null;
    }

    private void Update()
    {
        float angle = CurrentAngle();
        transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, NormalizedAxis());

        // عبور منتصف التأرجح (الزاوية تغيّر إشارتها) = أسرع نقطة بالحركة → شغّل صوت الهسهسة
        if (whooshSound != null && audioSource != null && amplitudeScale > DeadlyThreshold &&
            Mathf.Sign(angle) != Mathf.Sign(lastAngle))
            audioSource.PlayOneShot(whooshSound);

        lastAngle = angle;
        SyncKillComponent();
    }

    /// <summary>
    /// القتل مربوط بقوة التأرجح لا بالزر: الفاس يظل قاتلًا وهو يتباطأ، ولا يصير
    /// آمنًا إلا بعد ما يهدأ فعلًا — فلا يقدر اللاعب يخترقه لحظة سحب الرافعة.
    /// </summary>
    private void SyncKillComponent()
    {
        if (killComponent == null) return;
        bool deadly = amplitudeScale > DeadlyThreshold;
        if (killComponent.enabled != deadly) killComponent.enabled = deadly;
    }

    private float CurrentAngle()
    {
        float phase = (Time.time / Mathf.Max(0.01f, cycleDuration)) * 360f + phaseOffsetDegrees;
        return swingAmplitude * amplitudeScale * Mathf.Sin(phase * Mathf.Deg2Rad);
    }

    private Vector3 NormalizedAxis() =>
        rotationAxis.sqrMagnitude > 0.0001f ? rotationAxis.normalized : Vector3.forward;

    private void OnDrawGizmosSelected()
    {
        Quaternion rest = Application.isPlaying ? baseRotation : transform.localRotation;
        Quaternion parentRot = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion worldRest = parentRot * rest;
        Vector3 axis = NormalizedAxis();

        Gizmos.color = Color.gray;
        Gizmos.DrawLine(transform.position, transform.position + worldRest * Vector3.down * gizmoArmLength);

        Gizmos.color = new Color(1f, 0.2f, 0.1f);
        Vector3 left = worldRest * Quaternion.AngleAxis(-swingAmplitude, axis) * Vector3.down;
        Vector3 right = worldRest * Quaternion.AngleAxis(swingAmplitude, axis) * Vector3.down;
        Gizmos.DrawLine(transform.position, transform.position + left * gizmoArmLength);
        Gizmos.DrawLine(transform.position, transform.position + right * gizmoArmLength);
    }
}

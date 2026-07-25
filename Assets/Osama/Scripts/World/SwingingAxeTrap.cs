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

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت 'شووش' يُشغَّل كل ما عبر الفاس منتصف تأرجحه (أسرع نقطة) — تنبيه سمعي للاعب")]
    [SerializeField] private AudioClip whooshSound;

    [Header("تصحيح (Scene فقط)")]
    [Tooltip("طول الذراع التقريبي لرسم قوس التأرجح في نافذة Scene — لا يؤثر على اللعب")]
    [SerializeField] private float gizmoArmLength = 2f;

    private Quaternion baseRotation;
    private float lastAngle;

    private void Awake()
    {
        baseRotation = transform.localRotation;
        lastAngle = CurrentAngle();
    }

    private void Update()
    {
        float angle = CurrentAngle();
        transform.localRotation = baseRotation * Quaternion.AngleAxis(angle, NormalizedAxis());

        // عبور منتصف التأرجح (الزاوية تغيّر إشارتها) = أسرع نقطة بالحركة → شغّل صوت الهسهسة
        if (whooshSound != null && audioSource != null && Mathf.Sign(angle) != Mathf.Sign(lastAngle))
            audioSource.PlayOneShot(whooshSound);

        lastAngle = angle;
    }

    private float CurrentAngle()
    {
        float phase = (Time.time / Mathf.Max(0.01f, cycleDuration)) * 360f + phaseOffsetDegrees;
        return swingAmplitude * Mathf.Sin(phase * Mathf.Deg2Rad);
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

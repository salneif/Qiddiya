using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// يتحكّم في تحويل العالم إلى أبيض وأسود بنعومة عبر تعديل قيمة Saturation
/// في Color Adjustments داخل الفوليوم — <b>فقط الـ Saturation</b>، بدون لمس
/// الـ weight، حتى تبقى بقية خصائصك في نفس الفوليوم تعمل طبيعيًا.
///
/// Saturation = 0 → ألوان طبيعية، Saturation = -100 → أبيض وأسود كامل.
///
/// الإعداد في يونتي:
///  1) فوليوم (Global أو غيره) عليه Profile فيه Add Override → Color Adjustments.
///  2) فعّل Saturation (القيمة نفسها يتحكم بها هذا السكربت وقت التشغيل).
///  3) اترك weight الفوليوم = 1.
///  4) اسحب الفوليوم إلى الحقل bwVolume.
///  5) تأكد أن كاميرا اللعب مفعّل عليها Post Processing.
/// </summary>
public class WorldBWController : MonoBehaviour
{
    public static WorldBWController Instance { get; private set; }

    [Tooltip("الفوليوم الذي يحتوي على Color Adjustments")]
    [SerializeField] private Volume bwVolume;

    [Tooltip("مدة الانتقال بين ملوّن وأبيض/أسود (ثواني)")]
    [SerializeField] private float transitionDuration = 0.6f;

    [Tooltip("قيمة Saturation في الحالة الملوّنة")]
    [SerializeField] private float coloredSaturation = 0f;

    [Tooltip("قيمة Saturation في حالة الأبيض والأسود")]
    [Range(-100f, 0f)]
    [SerializeField] private float bwSaturation = -100f;

    [Tooltip("هل يبدأ العالم أبيض وأسود؟")]
    [SerializeField] private bool startBlackAndWhite = false;

    private ColorAdjustments colorAdjustments;
    private Coroutine routine;
    private bool targetBW;

    /// <summary>هل العالم مستهدَف ليكون أبيض وأسود؟</summary>
    public bool IsBlackAndWhite => targetBW;

    private void Awake()
    {
        Instance = this;
        if (bwVolume == null) bwVolume = GetComponent<Volume>();

        // .profile يعطي نسخة وقت-تشغيل فلا نعدّل ملف الـ Profile الأصلي
        if (bwVolume != null && bwVolume.profile.TryGet(out colorAdjustments))
            colorAdjustments.saturation.overrideState = true;
        else
            Debug.LogWarning("[WorldBWController] لم يُعثر على Color Adjustments في الفوليوم. " +
                             "أضِف Override: Color Adjustments وفعّل Saturation.");

        SetInstant(startBlackAndWhite);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>يحوّل إلى أبيض/أسود (on=true) أو يرجّع الألوان (on=false) بنعومة.</summary>
    public void SetBlackAndWhite(bool on)
    {
        targetBW = on;
        TransitionTo(on ? bwSaturation : coloredSaturation);
    }

    /// <summary>يبدّل الحالة الحالية.</summary>
    public void Toggle() => SetBlackAndWhite(!targetBW);

    /// <summary>ضبط فوري بلا انتقال.</summary>
    public void SetInstant(bool on)
    {
        targetBW = on;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (colorAdjustments != null)
            colorAdjustments.saturation.value = on ? bwSaturation : coloredSaturation;
    }

    private void TransitionTo(float target)
    {
        if (colorAdjustments == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Blend(target));
    }

    private IEnumerator Blend(float target)
    {
        float start = colorAdjustments.saturation.value;
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = transitionDuration > 0f ? Mathf.Clamp01(t / transitionDuration) : 1f;
            colorAdjustments.saturation.value = Mathf.Lerp(start, target, k);
            yield return null;
        }
        colorAdjustments.saturation.value = target;
        routine = null;
    }
}

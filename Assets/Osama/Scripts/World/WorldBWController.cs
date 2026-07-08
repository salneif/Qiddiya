using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// يتحكّم في تحويل العالم إلى أبيض وأسود بنعومة عبر التحكّم في قوّة (weight)
/// فوليوم يحتوي على Color Adjustments بـ Saturation = -100.
///
/// weight = 0 → ألوان طبيعية، weight = 1 → أبيض وأسود كامل.
///
/// الإعداد في يونتي:
///  1) GameObject → Volume → Global Volume (سمّه BW_Volume).
///  2) New (لإنشاء Profile) ثم Add Override → Post-processing → Color Adjustments.
///  3) فعّل Saturation واجعلها -100.
///  4) اسحب هذا الفوليوم إلى الحقل bwVolume.
///  5) تأكد أن كاميرا اللعب مفعّل عليها Post Processing (في مكوّن Camera → Rendering).
///
/// (لاحقًا لجعل أشياء معيّنة تبقى ملوّنة نستخدم Renderer Feature بقناع — خطة منفصلة.)
/// </summary>
public class WorldBWController : MonoBehaviour
{
    public static WorldBWController Instance { get; private set; }

    [Tooltip("فوليوم فيه Color Adjustments (Saturation = -100)")]
    [SerializeField] private Volume bwVolume;

    [Tooltip("مدة الانتقال بين ملوّن وأبيض/أسود (ثواني)")]
    [SerializeField] private float transitionDuration = 0.6f;

    [Tooltip("هل يبدأ العالم أبيض وأسود؟")]
    [SerializeField] private bool startBlackAndWhite = false;

    private Coroutine routine;

    /// <summary>هل العالم حاليًا أبيض وأسود (أكثر من النصف)؟</summary>
    public bool IsBlackAndWhite => bwVolume != null && bwVolume.weight > 0.5f;

    private void Awake()
    {
        Instance = this;
        if (bwVolume == null) bwVolume = GetComponent<Volume>();
        if (bwVolume != null) bwVolume.weight = startBlackAndWhite ? 1f : 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>يحوّل العالم إلى أبيض/أسود (on=true) أو يرجّع الألوان (on=false) بنعومة.</summary>
    public void SetBlackAndWhite(bool on) => TransitionTo(on ? 1f : 0f);

    /// <summary>يبدّل الحالة الحالية.</summary>
    public void Toggle() => TransitionTo(IsBlackAndWhite ? 0f : 1f);

    /// <summary>ضبط فوري بلا انتقال.</summary>
    public void SetInstant(bool on)
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (bwVolume != null) bwVolume.weight = on ? 1f : 0f;
    }

    private void TransitionTo(float target)
    {
        if (bwVolume == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Blend(target));
    }

    private IEnumerator Blend(float target)
    {
        float start = bwVolume.weight;
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = transitionDuration > 0f ? Mathf.Clamp01(t / transitionDuration) : 1f;
            bwVolume.weight = Mathf.Lerp(start, target, k);
            yield return null;
        }
        bwVolume.weight = target;
        routine = null;
    }
}

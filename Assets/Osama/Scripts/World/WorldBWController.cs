using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// يتحكّم في تحويل العالم إلى أبيض وأسود بنعومة، بأحد وضعين:
///
///  1) VolumeSaturation: يعدّل قيمة Saturation في Color Adjustments داخل الفوليوم
///     (الشاشة كلها أبيض وأسود — بدون مناطق ملوّنة).
///
///  2) FullscreenZones: يرسل القيمة العامة _WorldBWAmount لشيدر الفولسكرين
///     (Osama/BWColorZoneFullscreen)، فيصير العالم أبيض وأسود ما عدا المناطق
///     الملوّنة حول أي ColorZoneInteractor (الآيتم). لا يلمس أي ماتيريال.
///
/// الواجهة نفسها في الوضعين (SetBlackAndWhite / Toggle / SetInstant)،
/// فسكربت LampSwitch يعمل بدون أي تعديل.
/// </summary>
public class WorldBWController : MonoBehaviour
{
    public enum BWMode
    {
        [InspectorName("Volume Saturation (كل الشاشة)")]
        VolumeSaturation,
        [InspectorName("Fullscreen Zones (مناطق ملوّنة حول الآيتم)")]
        FullscreenZones
    }

    public static WorldBWController Instance { get; private set; }

    [Header("الوضع")]
    [Tooltip("VolumeSaturation = كل الشاشة أبيض/أسود عبر الفوليوم. " +
             "FullscreenZones = أبيض/أسود مع مناطق ملوّنة حول ColorZoneInteractor (يتطلب Full Screen Pass).")]
    [SerializeField] private BWMode mode = BWMode.FullscreenZones;

    [Header("وضع الفوليوم (VolumeSaturation)")]
    [Tooltip("الفوليوم الذي يحتوي على Color Adjustments")]
    [SerializeField] private Volume bwVolume;
    [Tooltip("قيمة Saturation في الحالة الملوّنة")]
    [SerializeField] private float coloredSaturation = 0f;
    [Tooltip("قيمة Saturation في حالة الأبيض والأسود")]
    [Range(-100f, 0f)]
    [SerializeField] private float bwSaturation = -100f;

    [Header("عام")]
    [Tooltip("مدة الانتقال بين ملوّن وأبيض/أسود (ثواني)")]
    [SerializeField] private float transitionDuration = 0.6f;
    [Tooltip("هل يبدأ العالم أبيض وأسود؟")]
    [SerializeField] private bool startBlackAndWhite = false;

    private static readonly int WorldBWAmountId = Shader.PropertyToID("_WorldBWAmount");

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
        else if (mode == BWMode.VolumeSaturation)
            Debug.LogWarning("[WorldBWController] لم يُعثر على Color Adjustments في الفوليوم. " +
                             "أضِف Override: Color Adjustments وفعّل Saturation.");

        // في وضع الفولسكرين: الفوليوم لا يتدخل في الإشباع (الشيدر هو المسؤول)
        if (mode == BWMode.FullscreenZones && colorAdjustments != null)
            colorAdjustments.saturation.value = coloredSaturation;

        SetInstant(startBlackAndWhite);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // لا نترك الشاشة أبيض/أسود بعد إغلاق المشهد
        Shader.SetGlobalFloat(WorldBWAmountId, 0f);
    }

    /// <summary>يحوّل إلى أبيض/أسود (on=true) أو يرجّع الألوان (on=false) بنعومة.</summary>
    public void SetBlackAndWhite(bool on)
    {
        targetBW = on;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Blend(on ? 1f : 0f));
    }

    /// <summary>يبدّل الحالة الحالية.</summary>
    public void Toggle() => SetBlackAndWhite(!targetBW);

    /// <summary>ضبط فوري بلا انتقال.</summary>
    public void SetInstant(bool on)
    {
        targetBW = on;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        Apply(on ? 1f : 0f);
    }

    private IEnumerator Blend(float target)
    {
        float start = CurrentAmount();
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = transitionDuration > 0f ? Mathf.Clamp01(t / transitionDuration) : 1f;
            Apply(Mathf.Lerp(start, target, k));
            yield return null;
        }
        Apply(target);
        routine = null;
    }

    /// <summary>يطبّق شدة الأبيض والأسود (0 ملوّن → 1 أبيض/أسود) حسب الوضع.</summary>
    private void Apply(float amount)
    {
        if (mode == BWMode.FullscreenZones)
        {
            Shader.SetGlobalFloat(WorldBWAmountId, amount);
        }
        else if (colorAdjustments != null)
        {
            colorAdjustments.saturation.value =
                Mathf.Lerp(coloredSaturation, bwSaturation, amount);
        }
    }

    private float CurrentAmount()
    {
        if (mode == BWMode.FullscreenZones)
            return Shader.GetGlobalFloat(WorldBWAmountId);
        if (colorAdjustments != null && !Mathf.Approximately(bwSaturation, coloredSaturation))
            return Mathf.InverseLerp(coloredSaturation, bwSaturation,
                                     colorAdjustments.saturation.value);
        return targetBW ? 1f : 0f;
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// "قائد" تحوّل العالم — يحرّك كل الأنظمة بتوقيت ومنحنى <b>موحّد</b> فتمشي متناغمة:
///  - شدة الأبيض/الأسود للعالم كله (WorldBWController).
///  - نصف قطر المنطقة الملوّنة حول الآيتم (WorldInteractor).
///  - (اختياري) اهتزاز الكاميرا وصوت التحوّل لحظة البداية.
///
/// بدل ما يكون لكل نظام Duration خاص (فيمشون متفرّقين)، هذا السكربت يعطيهم
/// نفس <see cref="duration"/> ونفس <see cref="curve"/> — هنا يجي التناغم.
///
/// اربط <see cref="Play"/> (bool) أو <see cref="Toggle"/> بالتريغر/اللمبة.
/// ملاحظة: عطّل InteractorRadiusAnimator على نفس الآيتم حتى لا يتنازع على نصف القطر.
/// </summary>
public class WorldChangeSequence : MonoBehaviour
{
    [Header("الأنظمة")]
    [Tooltip("متحكّم الأبيض/الأسود للعالم كله")]
    [SerializeField] private WorldBWController bw;
    [Tooltip("الآيتم/المنطقة الملوّنة التي يتغيّر نصف قطرها")]
    [SerializeField] private WorldInteractor coloredZone;

    [Header("التوقيت الموحّد (هنا التناغم)")]
    [Tooltip("مدة التحوّل الكامل لكل الأنظمة (ثواني) — صغّرها ليكون أسرع")]
    [SerializeField] private float duration = 2f;
    [Tooltip("منحنى واحد يقود كل شيء — نفس الإحساس للأبيض/الأسود والدائرة")]
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("نصف قطر المنطقة الملوّنة")]
    [Tooltip("نصف القطر في الحالة العادية (ملوّن)")]
    [SerializeField] private float normalRadius = 0f;
    [Tooltip("نصف القطر في الحالة المتحوّلة (أبيض/أسود) — كبّره ليغطي المستوى")]
    [SerializeField] private float changedRadius = 40f;

    [Header("لمسات (اختياري)")]
    [SerializeField] private CameraShake cameraShake;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip changeSound;

    [Header("البداية")]
    [Tooltip("يبدأ العالم متحوّلًا (أبيض/أسود) عند التشغيل")]
    [SerializeField] private bool startChanged = false;

    [Header("اختبار (في المحرر)")]
    [SerializeField] private Key testToggleKey = Key.G;

    [Header("أحداث")]
    public UnityEvent onChanged;   // اكتمل التحوّل لأبيض/أسود
    public UnityEvent onRestored;  // رجع ملوّنًا

    private bool isChanged;
    private Coroutine routine;

    /// <summary>هل العالم متحوّل (أبيض/أسود) حاليًا؟</summary>
    public bool IsChanged => isChanged;

    private void Start()
    {
        // ضبط الحالة الابتدائية فورًا
        isChanged = startChanged;
        if (bw != null) bw.SetAmount01(startChanged ? 1f : 0f);
        if (coloredZone != null) coloredZone.Radius = startChanged ? changedRadius : normalRadius;
    }

    private void Update()
    {
        if (testToggleKey != Key.None && Keyboard.current != null &&
            Keyboard.current[testToggleKey].wasPressedThisFrame)
            Toggle();
    }

    /// <summary>يحوّل العالم لأبيض/أسود (toChanged=true) أو يرجّعه ملوّنًا.</summary>
    public void Play(bool toChanged)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(toChanged));
    }

    /// <summary>يبدّل الحالة الحالية.</summary>
    public void Toggle() => Play(!isChanged);

    private IEnumerator Run(bool toChanged)
    {
        // لمسات لحظة البداية
        if (changeSound != null && sfxSource != null) sfxSource.PlayOneShot(changeSound);
        if (cameraShake != null) cameraShake.Shake();

        float bwFrom = isChanged ? 1f : 0f;
        float bwTo = toChanged ? 1f : 0f;
        float rFrom = coloredZone != null ? coloredZone.Radius : 0f;
        float rTo = toChanged ? changedRadius : normalRadius;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = curve.Evaluate(duration > 0f ? Mathf.Clamp01(t / duration) : 1f);

            if (bw != null) bw.SetAmount01(Mathf.Lerp(bwFrom, bwTo, k));
            if (coloredZone != null) coloredZone.Radius = Mathf.Lerp(rFrom, rTo, k);

            yield return null;
        }

        if (bw != null) bw.SetAmount01(bwTo);
        if (coloredZone != null) coloredZone.Radius = rTo;

        isChanged = toChanged;
        routine = null;

        if (toChanged) onChanged?.Invoke();
        else onRestored?.Invoke();
    }
}

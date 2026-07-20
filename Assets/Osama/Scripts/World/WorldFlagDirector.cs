using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// مخرج حالة العالم المرتبطة بالعلم — النسخة المطوّرة من كود المشعل المرجعي،
/// لكن بالأحداث بدل فحص كل إطار، وبالشيدر بدل تبديل الإضاءة يدويًا.
///
/// عند التقاط العلم:
///  - العالم يقلب أبيض/أسود عبر WorldChangeSequence (الشيدر + الدائرة الملوّنة معًا).
///  - الموسيقى تصير أبطأ (pitch) ومكتومة/مضخّمة (Low Pass) بانتقال ناعم.
///  - كائنات وسم DarkObject تظهر (ممرات/أشياء عالم الظل) ووسم LightObject تختفي.
///  - حدث onFlagTaken لفتح البوابة وغيرها.
/// وعند وضع العلم في مقبسه: كل شيء يرجع بالعكس.
/// </summary>
public class WorldFlagDirector : MonoBehaviour
{
    [Header("العلم")]
    [SerializeField] private FlagItem flag;

    [Header("تحوّل العالم (الشيدر)")]
    [Tooltip("قائد التحوّل — يقلب الأبيض/الأسود والدائرة الملوّنة بتوقيت موحّد")]
    [SerializeField] private WorldChangeSequence sequence;
    [Tooltip("لو مفعّل: العالم يبدأ أبيض/أسود، وأخذ العلم يلوّنه (والعكس عند الإرجاع). " +
             "لو مطفي: العالم يبدأ ملوّنًا، وأخذ العلم يقلبه أبيض/أسود.")]
    [SerializeField] private bool flagColorsTheWorld = true;

    [Header("الموسيقى")]
    [SerializeField] private AudioSource soundtrack;
    [Tooltip("سرعة الأغنية أثناء حمل العلم (1 = طبيعية ناعمة)")]
    [SerializeField] private float heldPitch = 1f;
    [Tooltip("تردد الكتم أثناء الحمل (22000 = صافية بلا كتم)")]
    [SerializeField] private float heldCutoff = 22000f;
    [Tooltip("سرعة الأغنية والعلم في الأرض (أبطأ = أثقل)")]
    [SerializeField] private float groundPitch = 0.55f;
    [Tooltip("تردد الكتم والعلم في الأرض (أصغر = مكتومة/مضخّمة أكثر)")]
    [SerializeField] private float groundCutoff = 700f;
    [Tooltip("مدة الانتقال الصوتي (ثواني)")]
    [SerializeField] private float musicFadeTime = 1.2f;

    [Header("كائنات العالمين (بالوسوم)")]
    [Tooltip("وسم كائنات العالم المضيء/الملوّن")]
    [SerializeField] private string lightTag = "LightObject";
    [Tooltip("وسم كائنات العالم المظلم/الظل (ممرات/جسور...)")]
    [SerializeField] private string darkTag = "DarkObject";
    [Tooltip("لو مفعّل: LightObject تظهر عند حمل العلم و DarkObject بدونه " +
             "(يطابق: بدون علم = عالم مظلم، أخذ العلم = ينوّر). " +
             "لو مطفي: العكس.")]
    [SerializeField] private bool lightObjectsShowWhenHeld = true;

    [Header("أحداث")]
    [Tooltip("عند أخذ العلم (افتح البوابة هنا)")]
    public UnityEvent onFlagTaken;
    [Tooltip("عند إرجاع العلم لمقبسه")]
    public UnityEvent onFlagReturned;

    private readonly List<GameObject> lightObjects = new();
    private readonly List<GameObject> darkObjects = new();
    private AudioLowPassFilter lowPass;
    private Coroutine musicRoutine;

    private void Start()
    {
        // تُجمع بالوسم مرة واحدة (لازم تكون فعّالة عند بداية اللعبة ليجدها)
        lightObjects.AddRange(GameObject.FindGameObjectsWithTag(lightTag));
        darkObjects.AddRange(GameObject.FindGameObjectsWithTag(darkTag));

        // الحالة الابتدائية حسب مكان العلم (بدون علم افتراضيًا)
        SwapWorldObjects(flag != null && flag.IsHeld);

        if (soundtrack != null)
        {
            lowPass = soundtrack.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = soundtrack.gameObject.AddComponent<AudioLowPassFilter>();

            // الحالة الابتدائية فورًا حسب مكان العلم:
            // في الأرض = مكتومة وبطيئة، محمول = ناعمة وصافية
            bool held = flag != null && flag.IsHeld;
            soundtrack.pitch = held ? heldPitch : groundPitch;
            lowPass.cutoffFrequency = held ? heldCutoff : groundCutoff;
        }
    }

    private void OnEnable()
    {
        if (flag != null)
        {
            flag.PickedUp += HandleFlagTaken;
            flag.Placed += HandleFlagReturned;
        }
    }

    private void OnDisable()
    {
        if (flag != null)
        {
            flag.PickedUp -= HandleFlagTaken;
            flag.Placed -= HandleFlagReturned;
        }
    }

    private void HandleFlagTaken()
    {
        // 1) العالم يقلب أبيض/أسود (الشيدر + الدائرة بتوقيت موحّد)
        // أخذ العلم: يلوّن (Play false) أو يقلب أبيض/أسود (Play true) حسب الإعداد
        if (sequence != null) sequence.Play(!flagColorsTheWorld);

        // 2) كائنات عالم الظل تظهر والطبيعية تختفي
        SwapWorldObjects(held: true);

        // 3) الموسيقى: أبطأ ومكتومة بانتقال ناعم
        BlendMusic(heldPitch, heldCutoff);

        onFlagTaken?.Invoke();
    }

    private void HandleFlagReturned()
    {
        // إرجاع العلم: عكس حالة الأخذ
        if (sequence != null) sequence.Play(flagColorsTheWorld);
        SwapWorldObjects(held: false);
        BlendMusic(groundPitch, groundCutoff); // ترجع مكتومة وبطيئة
        onFlagReturned?.Invoke();
    }

    private void SwapWorldObjects(bool held)
    {
        // من يظهر عند حمل العلم يحدده الخيار (المضيء أو المظلم)
        bool lightVisible = lightObjectsShowWhenHeld ? held : !held;

        foreach (var go in lightObjects)
            if (go != null) go.SetActive(lightVisible);
        foreach (var go in darkObjects)
            if (go != null) go.SetActive(!lightVisible);
    }

    private void BlendMusic(float targetPitch, float targetCutoff)
    {
        if (soundtrack == null) return;
        if (musicRoutine != null) StopCoroutine(musicRoutine);
        musicRoutine = StartCoroutine(MusicBlend(targetPitch, targetCutoff));
    }

    private IEnumerator MusicBlend(float targetPitch, float targetCutoff)
    {
        float startPitch = soundtrack.pitch;
        float startCutoff = lowPass != null ? lowPass.cutoffFrequency : 22000f;
        float t = 0f;

        while (t < musicFadeTime)
        {
            t += Time.deltaTime;
            float k = musicFadeTime > 0f ? Mathf.Clamp01(t / musicFadeTime) : 1f;
            soundtrack.pitch = Mathf.Lerp(startPitch, targetPitch, k);
            if (lowPass != null)
                lowPass.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, k);
            yield return null;
        }

        soundtrack.pitch = targetPitch;
        if (lowPass != null) lowPass.cutoffFrequency = targetCutoff;
        musicRoutine = null;
    }
}

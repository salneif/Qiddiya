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

    [Header("الموسيقى")]
    [SerializeField] private AudioSource soundtrack;
    [Tooltip("سرعة الأغنية أثناء حمل العلم (أبطأ)")]
    [SerializeField] private float heldPitch = 0.55f;
    [Tooltip("تردد الكتم أثناء الحمل (أصغر = مكتومة/مضخّمة أكثر)")]
    [SerializeField] private float heldCutoff = 700f;
    [Tooltip("مدة الانتقال الصوتي (ثواني)")]
    [SerializeField] private float musicFadeTime = 1.2f;

    [Header("كائنات العالمين (بالوسوم)")]
    [Tooltip("وسم كائنات العالم الطبيعي — تختفي أثناء حمل العلم")]
    [SerializeField] private string lightTag = "LightObject";
    [Tooltip("وسم كائنات عالم الظل (ممرات/جسور...) — تظهر أثناء حمل العلم")]
    [SerializeField] private string darkTag = "DarkObject";

    [Header("أحداث")]
    [Tooltip("عند أخذ العلم (افتح البوابة هنا)")]
    public UnityEvent onFlagTaken;
    [Tooltip("عند إرجاع العلم لمقبسه")]
    public UnityEvent onFlagReturned;

    private readonly List<GameObject> lightObjects = new();
    private readonly List<GameObject> darkObjects = new();
    private AudioLowPassFilter lowPass;
    private float normalPitch = 1f;
    private Coroutine musicRoutine;

    private void Start()
    {
        // تُجمع بالوسم مرة واحدة (لازم تكون فعّالة عند بداية اللعبة ليجدها)
        lightObjects.AddRange(GameObject.FindGameObjectsWithTag(lightTag));
        darkObjects.AddRange(GameObject.FindGameObjectsWithTag(darkTag));

        // الحالة الطبيعية: كائنات الظل مخفية
        foreach (var go in darkObjects) go.SetActive(false);

        if (soundtrack != null)
        {
            normalPitch = soundtrack.pitch;
            lowPass = soundtrack.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = soundtrack.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
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
        if (sequence != null) sequence.Play(true);

        // 2) كائنات عالم الظل تظهر والطبيعية تختفي
        SwapWorldObjects(held: true);

        // 3) الموسيقى: أبطأ ومكتومة بانتقال ناعم
        BlendMusic(heldPitch, heldCutoff);

        onFlagTaken?.Invoke();
    }

    private void HandleFlagReturned()
    {
        if (sequence != null) sequence.Play(false);
        SwapWorldObjects(held: false);
        BlendMusic(normalPitch, 22000f);
        onFlagReturned?.Invoke();
    }

    private void SwapWorldObjects(bool held)
    {
        foreach (var go in lightObjects)
            if (go != null) go.SetActive(!held);
        foreach (var go in darkObjects)
            if (go != null) go.SetActive(held);
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

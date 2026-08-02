using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// منطقة تُخفت (أو ترفع) مستوى مصدر صوت بهدوء عند دخول اللاعب.
///
/// استخدامها الأساسي: قبل باب الانتقال — تخفت موسيقى المطاردة تدريجيًا فيصل
/// اللاعب للباب والمشهد صامت، بدل قطع مفاجئ يكشف أن هناك تبديل مكان.
///
/// تعمل على عدة مصادر معًا، فتقدر تسكت الموسيقى والأجواء بضربة واحدة.
///
/// التركيب: كائن فيه Collider (Is Trigger) قبل الباب بخطوات + هذا السكربت.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AudioFadeZone : MonoBehaviour
{
    [Header("الهدف")]
    [Tooltip("المصادر التي يتغيّر مستواها — الموسيقى والأجواء")]
    [SerializeField] private AudioSource[] targets;

    [Header("التلاشي")]
    [Tooltip("المستوى المطلوب الوصول إليه. 0 = صمت تام.")]
    [Range(0f, 1f)]
    [SerializeField] private float targetVolume = 0f;
    [Tooltip("مدة التلاشي (ثواني) — أطلها لباب انتقال، وقصّرها لمفاجأة")]
    [SerializeField] private float fadeDuration = 2f;

    [Header("الشروط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("مرة واحدة ثم يتعطّل")]
    [SerializeField] private bool triggerOnce = true;
    [Tooltip("يرجع المستوى الأصلي عند خروج اللاعب (لا يعمل مع 'مرة واحدة')")]
    [SerializeField] private bool restoreOnExit = false;

    [Header("أحداث")]
    [Tooltip("عند اكتمال التلاشي — مفيد لبدء الانتقال بعد الصمت")]
    public UnityEvent onFadeComplete;

    private float[] originalVolumes;
    private Coroutine routine;
    private bool used;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (targets == null) return;

        // نحفظ المستويات الأصلية لنقدر نرجّعها
        originalVolumes = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            originalVolumes[i] = targets[i] != null ? targets[i].volume : 1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used && triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        used = true;
        StartFade(targetVolume, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!restoreOnExit || triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        StartFade(-1f, false); // -1 = ارجع لكل مصدر مستواه الأصلي
    }

    /// <summary>يبدأ التلاشي يدويًا — اربطه بأي حدث.</summary>
    public void FadeOut() => StartFade(targetVolume, true);

    /// <summary>يرجّع المستويات الأصلية.</summary>
    public void FadeIn() => StartFade(-1f, false);

    private void StartFade(float volume, bool fireEvent)
    {
        if (targets == null || targets.Length == 0) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FadeRoutine(volume, fireEvent));
    }

    private IEnumerator FadeRoutine(float volume, bool fireEvent)
    {
        var from = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            from[i] = targets[i] != null ? targets[i].volume : 0f;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = fadeDuration > 0f ? Mathf.Clamp01(t / fadeDuration) : 1f;

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                float to = volume < 0f ? originalVolumes[i] : volume;
                targets[i].volume = Mathf.Lerp(from[i], to, k);
            }
            yield return null;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            targets[i].volume = volume < 0f ? originalVolumes[i] : volume;
        }

        routine = null;
        if (fireEvent) onFadeComplete?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.7f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

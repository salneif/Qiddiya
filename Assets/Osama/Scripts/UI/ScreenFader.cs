using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// مموّه شاشة (Fade) للانتقالات الاحترافية بين القوائم والمشاهد.
/// يُركّب على كائن فيه CanvasGroup لصورة سوداء تغطّي الشاشة بالكامل.
/// يبدأ بالتلاشي من الأسود عند فتح القائمة، ويعتّم الشاشة ثم يحمّل المشهد عند Play.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Tooltip("مدة التلاشي (ثواني)")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("يبدأ بالتلاشي من الأسود تلقائيًا عند التشغيل")]
    [SerializeField] private bool fadeInOnStart = true;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (fadeInOnStart)
            FadeIn();
        else
            group.alpha = 0f;
    }

    /// <summary>يتلاشى من الأسود (1) إلى الشفاف (0) — يكشف القائمة/المشهد.</summary>
    public void FadeIn()
    {
        StopAllCoroutines();
        group.blocksRaycasts = false;
        StartCoroutine(Fade(1f, 0f, null));
    }

    /// <summary>يعتّم الشاشة إلى الأسود ثم يحمّل المشهد المطلوب.</summary>
    public void FadeOutAndLoad(string sceneName)
    {
        StopAllCoroutines();
        group.blocksRaycasts = true; // يمنع الضغط أثناء الانتقال
        StartCoroutine(Fade(group.alpha, 1f, () => LoadSceneAsync(sceneName)));
    }

    /// <summary>يعتّم الشاشة إلى الأسود ثم ينفّذ إجراءً (بدون تحميل مشهد).</summary>
    public void FadeOut(Action onComplete = null)
    {
        StopAllCoroutines();
        group.blocksRaycasts = true;
        StartCoroutine(Fade(group.alpha, 1f, onComplete));
    }

    private void LoadSceneAsync(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ScreenFader] اسم المشهد فارغ.");
            return;
        }
        SceneManager.LoadSceneAsync(sceneName);
    }

    private IEnumerator Fade(float from, float to, Action onComplete)
    {
        group.alpha = from;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // يعمل حتى لو Time.timeScale = 0
            group.alpha = Mathf.Lerp(from, to, fadeDuration > 0f ? t / fadeDuration : 1f);
            yield return null;
        }
        group.alpha = to;
        onComplete?.Invoke();
    }
}

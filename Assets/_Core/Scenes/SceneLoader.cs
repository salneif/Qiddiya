using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// تحميل المشاهد بالاسم مع تلاشٍ اختياري. يستبدل كل سكربتات Play المتفرقة.
/// ضعه على كائن في المشهد، أو استدعِ SceneLoader.Load("اسم المشهد") من أي مكان.
///
/// مهم: أي مشهد تريد تحميله يجب أن يكون مضافًا في
/// File > Build Profiles > Scene List وإلا سيفشل التحميل.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Header("المشهد")]
    [Tooltip("اسم المشهد الذي يفتحه زر Play — بدون امتداد .unity")]
    [SerializeField] private string sceneName = "Hub";

    [Header("الانتقال")]
    [Tooltip("مموّه الشاشة — إن تُرك فارغًا يُحمّل المشهد مباشرة")]
    [SerializeField] private ScreenFader fader;

    [Tooltip("تأخير بسيط قبل التحميل ليكتمل صوت الضغط على الزر")]
    [SerializeField, Range(0f, 1f)] private float delayBeforeLoad = 0f;

    private static bool isLoading;

    /// <summary>
    /// القفل ثابت (static) ليمنع نقرتين متتاليتين على Play. لكن الكائن الذي يحمل
    /// الكوروتين يُدمَّر أثناء تحميل المشهد، فلا يصل السطر الذي يرفع القفل ويبقى
    /// مرفوعًا للأبد — فيصير زر Play ميتًا عند العودة للقائمة. نرفعه هنا بدلًا من ذلك.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallLockRelease()
    {
        isLoading = false;
        SceneManager.sceneLoaded -= ReleaseLock;
        SceneManager.sceneLoaded += ReleaseLock;
    }

    private static void ReleaseLock(Scene scene, LoadSceneMode mode) => isLoading = false;

    /// <summary>اربطه بزر Play في الـ Inspector (بدون تمرير أي قيمة).</summary>
    public void LoadTargetScene() => LoadScene(sceneName);

    /// <summary>اربطه بأي زر وحدّد اسم المشهد من الـ Inspector مباشرة.</summary>
    public void LoadScene(string targetScene)
    {
        if (isLoading) return;

        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogError("[SceneLoader] اسم المشهد فارغ.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError(
                $"[SceneLoader] المشهد \"{targetScene}\" غير موجود في قائمة المشاهد. " +
                "أضِفه من File > Build Profiles > Scene List.", this);
            return;
        }

        isLoading = true;
        Time.timeScale = 1f; // اللعبة قد تكون متوقفة من لوحة الإيقاف

        if (fader != null)
            fader.FadeOut(() => StartCoroutine(LoadRoutine(targetScene)));
        else
            StartCoroutine(LoadRoutine(targetScene));
    }

    /// <summary>يعيد تحميل المشهد الحالي (زر Restart).</summary>
    public void ReloadCurrentScene() => LoadScene(SceneManager.GetActiveScene().name);

    /// <summary>يخرج من اللعبة، ويوقف التشغيل داخل المحرر.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadRoutine(string targetScene)
    {
        if (delayBeforeLoad > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeLoad);

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
        while (!operation.isDone)
            yield return null;

        isLoading = false;
    }
}

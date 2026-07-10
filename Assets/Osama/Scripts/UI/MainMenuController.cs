using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// تحكّم القائمة الرئيسية: تشغيل اللعبة (مع تلاشٍ)، فتح/إغلاق الإعدادات، والخروج.
/// اربط دواله بأحداث OnClick للأزرار من الـ Inspector.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("المشهد")]
    [Tooltip("اسم مشهد اللعبة الذي يُحمّل عند الضغط على Play (يجب إضافته لقائمة المشاهد في Build)")]
    [SerializeField] private string gameSceneName = "osama_scene";

    [Header("الانتقال")]
    [Tooltip("مموّه الشاشة للانتقال الناعم — إن تُرك فارغًا يُحمّل المشهد مباشرة")]
    [SerializeField] private ScreenFader fader;

    [Header("الإعدادات")]
    [Tooltip("لوحة الإعدادات (تُخفى عند البداية)")]
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // القائمة تعمل والوقت طبيعي دائمًا
        Time.timeScale = 1f;
    }

    /// <summary>يبدأ اللعبة: تلاشٍ للأسود ثم تحميل مشهد اللعبة.</summary>
    public void PlayGame()
    {
        if (fader != null)
            fader.FadeOutAndLoad(gameSceneName);
        else
            SceneManager.LoadSceneAsync(gameSceneName);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>يخرج من اللعبة (يوقف التشغيل داخل المحرر).</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

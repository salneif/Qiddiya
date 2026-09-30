using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// لمساتٌ صغيرة تفرق بين لعبة طالب ولعبةٍ محترفة:
///
/// <list type="bullet">
/// <item><b>مزامنة الشاشة (V-Sync)</b>: جودة PC كانت بلا حدٍّ للإطارات — القائمة تركض بآلاف
/// الإطارات فتسخن البطاقة ويُسمع أزيزها، ويتمزّق المشهد عند الالتفات.</item>
/// <item><b>الصوت يسكت حين تُترك النافذة</b> (Alt-Tab)، ويعود معها. في البلد وحده — لا يُسكت
/// المحرر كلما ضُغط خارج نافذة اللعب.</item>
/// <item><b>انفصال يد التحكّم يوقف اللعبة</b>: بطاريةٌ نفدت وسط فخّ لا تقتل اللاعب — تُفتح قائمة
/// الإيقاف، ولافتةٌ تقول ما حدث بعد العودة.</item>
/// <item><b>رقم النسخة</b> صغيرًا في ركن القائمة الرئيسية.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
public class ChromaPolish : MonoBehaviour
{
    private const string Version = "CHROMAFALL  v1.1";
    private const string MenuScene = "Hub-Menu";

    private static ChromaPolish instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaPolish") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaPolish>();
        DontDestroyOnLoad(host);

        if (QualitySettings.vSyncCount == 0) QualitySettings.vSyncCount = 1;
        instance.Stamp(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private Canvas canvas;
    private CanvasGroup group;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (Application.isEditor) return;
        AudioListener.pause = !focused;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Stamp(scene);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!(device is Gamepad)) return;
        if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) return;
        if (!InputScheme.UsingGamepad || ChromaEvents.Quiet) return;

        PauseMenuFix.RequestPause();
        ChromaFunEvents.Announce(new ChromaFunEvents.Banner
        {
            header = "CONTROLLER",
            title = "DISCONNECTED",
            line = "The game paused itself. Reconnect and press Options.",
            letter = "!",
            accent = new Color(1f, 0.45f, 0.4f),
            sfx = "Skin_Locked",
        });
    }

    /// <summary>رقم النسخة أسفل يمين القائمة الرئيسية وحدها.</summary>
    private void Stamp(Scene scene)
    {
        bool menu = scene.name == MenuScene;
        if (!menu) { if (canvas != null) canvas.enabled = false; return; }

        if (canvas == null)
        {
            canvas = ChromaWardrobeArt.NewCanvas(transform, "Version", 10, out group);
            Color paper = ChromaWardrobeArt.Paper;
            TextMeshProUGUI text = ChromaWardrobeArt.NewText(canvas.transform, "Version", false, 22f,
                                                             new Color(paper.r, paper.g, paper.b, 0.55f),
                                                             TextAlignmentOptions.Right);
            ChromaWardrobeArt.Place(text, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-28f, 18f), new Vector2(500f, 30f));
            text.text = Version;
        }
        canvas.enabled = true;
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <b>«Continue»</b> في القائمة الرئيسية: يكمل اللاعب من آخر مرحلةٍ دخلها، بأعلامه ووجوهه.
///
/// <see cref="GameProgress"/> لا يحفظ بين الجلسات (مطفأ عمدًا للتطوير)، فالحفظ هنا في مفاتيح
/// خاصّة <c>Chroma.Save.*</c>: المشهد، والأعلام المغروسة والمحمولة، وعدّ اللعبة، والوجوه المجموعة.
/// يُكتب مع كل دخول مرحلةٍ أو هب، ومع كل تغيّرٍ في الأعلام، وعند العودة للقائمة والخروج.
/// ويُمسح مع اللعبة الجديدة (الانترو يصفّر التقدّم).
///
/// الزرّ نسخةٌ من زرّ Play وقت التشغيل (مشهد عبير لا يُمسّ) برسمةٍ بحروف أزرارها نفسها
/// (<c>Resources/Menu/Continue_pic</c>)، يأخذ مكان Play وتنزل الأزرار تحته خطوة.
/// </summary>
[DisallowMultipleComponent]
public class ChromaSave : MonoBehaviour
{
    private const string Key = "Chroma.Save.";
    private const string MenuScene = "Hub-Menu";
    private const string PlayName = "Play_Button", MenuRoot = "Menu-UI";
    private const string Art = "Menu/Continue_pic";
    private const string AddedName = "Continue_Button (Chroma)";
    private static readonly string[] Resumable = { "Hub", "Steam_Final", "AliLvl2_SultanVersion", "Main_Circus" };

    private static ChromaSave instance;
    private static bool loading;

    /// <summary>هل هناك لعبةٌ محفوظة يُكمل منها؟</summary>
    public static bool HasSave => !string.IsNullOrEmpty(PlayerPrefs.GetString(Key + "Scene", string.Empty));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaSave") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaSave>();
        DontDestroyOnLoad(host);
        instance.Arrived(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        loading = false;
    }

    private bool inMenu, buttonDone;
    private float nextTry;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameProgress.Changed += OnProgress;
        GameProgress.ProgressReset += OnProgressReset;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameProgress.Changed -= OnProgress;
        GameProgress.ProgressReset -= OnProgressReset;
    }

    private void OnApplicationQuit() => Store(null);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Arrived(scene);
    }

    private void Arrived(Scene scene)
    {
        inMenu = scene.name == MenuScene;
        buttonDone = false;
        nextTry = 0f;

        if (Array.IndexOf(Resumable, scene.name) >= 0) Store(scene.name);
        else if (inMenu && GameProgress.Instance.AllPlanted) OnProgressReset();   // خُتمت اللعبة: لا شيء يُكمل
        else if (inMenu) Store(null);   // الرجوع من الإيقاف: الوجوه والأعلام كما تُركت
    }

    private void OnProgress()
    {
        if (!loading && ChromaEvents.GameplayScene) Store(null);
    }

    private void OnProgressReset()
    {
        if (loading) return;
        foreach (string k in new[] { "Scene", "Planted", "Carried", "Run", "Faces" }) PlayerPrefs.DeleteKey(Key + k);
        PlayerPrefs.Save();
    }

    /// <summary>يحفظ الحالة. <paramref name="scene"/> فارغ = المشهد المحفوظ لا يتغيّر (ولا يُنشئ حفظًا جديدًا).</summary>
    private static void Store(string scene)
    {
        if (loading) return;
        if (scene != null) PlayerPrefs.SetString(Key + "Scene", scene);
        else if (!HasSave) return;

        GameProgress progress = GameProgress.Instance;
        int mask = 0;
        foreach (FlagId id in Enum.GetValues(typeof(FlagId)))
            if (id != FlagId.None && progress.IsPlanted(id)) mask |= 1 << (int)id;

        PlayerPrefs.SetInt(Key + "Planted", mask);
        PlayerPrefs.SetInt(Key + "Carried", (int)progress.CarriedFlag);
        PlayerPrefs.SetInt(Key + "Run", ChromaBank.Run);
        PlayerPrefs.SetString(Key + "Faces", string.Join("|", ChromaBank.CollectedIds()));
        PlayerPrefs.Save();
    }

    /// <summary>يعيد اللعبة المحفوظة كما هي ويأخذ اللاعب لمشهدها.</summary>
    public static void Continue()
    {
        string scene = PlayerPrefs.GetString(Key + "Scene", string.Empty);
        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene) || LoadingOverlay.IsBusy) return;

        int mask = PlayerPrefs.GetInt(Key + "Planted", 0);
        var carried = (FlagId)PlayerPrefs.GetInt(Key + "Carried", (int)FlagId.None);
        int run = PlayerPrefs.GetInt(Key + "Run", 0);
        string[] faces = PlayerPrefs.GetString(Key + "Faces", string.Empty).Split('|');

        loading = true;
        try
        {
            GameProgress progress = GameProgress.Instance;
            ChromaEvents.Restoring(() =>
            {
                progress.ResetProgress();   // من حالةٍ نظيفة، ثم المحفوظ فوقها
                foreach (FlagId id in Enum.GetValues(typeof(FlagId)))
                    if (id != FlagId.None && (mask & (1 << (int)id)) != 0) progress.PlantFlag(id);
                if (carried != FlagId.None && !progress.IsPlanted(carried)) progress.CarryFlag(carried);
            });
            ChromaBank.Restore(run, faces);
            ChromaTutorial.Skip();
        }
        finally { loading = false; }

        if (Time.timeScale == 0f) Time.timeScale = 1f;
        if (!LoadingOverlay.Go(scene)) SceneManager.LoadSceneAsync(scene);
    }

    // ---------- الزرّ ----------

    private void Update()
    {
        if (!inMenu || buttonDone || Time.unscaledTime < nextTry) return;
        nextTry = Time.unscaledTime + 0.5f;   // القائمة قد تظهر بعد شاشةٍ أولى
        AddButton();
    }

    private void AddButton()
    {
        if (!HasSave) { buttonDone = true; return; }

        Button play = null;
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null || b.transform.parent == null) continue;
            if (b.name == AddedName) { buttonDone = true; return; }
            if (b.name.Trim() == PlayName && b.transform.parent.name.Trim() == MenuRoot) play = b;
        }
        if (play == null) return;

        var art = Resources.Load<Sprite>(Art);
        var playRect = play.transform as RectTransform;
        if (art == null || playRect == null) { buttonDone = true; return; }
        buttonDone = true;

        // لوحة القائمة تعيد كل زرٍّ لموضعه الأوّل كلّما فُتحت: نعيدها لحالها (لا زرّ منتفخ يُنسخ)،
        // ثم نلتقطها من جديد بعد النقل
        Transform menu = play.transform.parent;
        UIPanel[] panels = MenuBaseline.Settle(menu);

        // الخطوة بين الأزرار: من Play إلى أقرب زرٍّ تحته
        float step = 0f, py = playRect.anchoredPosition.y;
        foreach (Transform child in menu)
        {
            if (child == play.transform || !(child is RectTransform r) || child.GetComponent<Button>() == null) continue;
            float gap = py - r.anchoredPosition.y;
            if (gap > 1f && (step <= 0f || gap < step)) step = gap;
        }
        if (step <= 0f) return;

        GameObject copy = Instantiate(play.gameObject, menu);
        copy.name = AddedName;
        copy.transform.SetSiblingIndex(play.transform.GetSiblingIndex());
        ((RectTransform)copy.transform).anchoredPosition = playRect.anchoredPosition;

        // Play وما تحته ينزلون خطوة
        foreach (Transform child in menu)
        {
            if (child == copy.transform || !(child is RectTransform r) || child.GetComponent<Button>() == null) continue;
            if (r.anchoredPosition.y <= py + 0.5f) r.anchoredPosition += Vector2.down * step;
        }

        var image = copy.GetComponent<Image>();
        if (image != null) image.sprite = art;

        var button = copy.GetComponent<Button>();
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentMethodName(i) != "PlayOneShot")
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);   // لا LoadTargetScene
        button.onClick.AddListener(Continue);

        MenuBaseline.Recapture(panels);
        UIPanel panel = menu.GetComponent<UIPanel>();
        MenuBaseline.SetFirst(panel, copy);   // يد التحكّم تبدأ على Continue
        if (panel != null) panel.LastSelected = copy;

        EventSystem events = EventSystem.current;
        if (events != null && (events.currentSelectedGameObject == null || events.currentSelectedGameObject == play.gameObject))
            events.SetSelectedGameObject(copy);
    }
}

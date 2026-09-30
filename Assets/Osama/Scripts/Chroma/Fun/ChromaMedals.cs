using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>ميداليات الوقت</b> لستيم والتوايلايت والسيرك: مؤقّتٌ صغير في الركن الأعلى الأيسر يبدأ
/// حين تنتهي شاشة التحميل ويقف مع الإيقاف، وعند الخروج من بوابة المرحلة تُحسب الميدالية
/// وأفضل زمن (محفوظ) وتعرضها بطاقة الهب (<see cref="ChromaLevelStats"/>). الحدود في <see cref="Levels"/>.
/// </summary>
[DisallowMultipleComponent]
public class ChromaMedals : MonoBehaviour
{
    private struct Level
    {
        public string scene, name;
        public float gold, silver, bronze;   // ثوانٍ
    }

    private static readonly Level[] Levels =
    {
        new Level { scene = "Steam_Final",           name = "STEAM TOWN", gold = 210f, silver = 300f, bronze = 450f },
        new Level { scene = "AliLvl2_SultanVersion", name = "TWILIGHT",   gold = 300f, silver = 420f, bronze = 600f },
        new Level { scene = "Main_Circus",           name = "THE CIRCUS", gold = 270f, silver = 390f, bronze = 540f },
    };

    internal static readonly string[] MedalNames = { "", "BRONZE", "SILVER", "GOLD" };
    internal static readonly Color[] MedalColors =
    {
        new Color(0.75f, 0.75f, 0.75f), new Color(0.80f, 0.52f, 0.28f),
        new Color(0.78f, 0.80f, 0.84f), new Color(1f, 0.80f, 0.25f),
    };

    private const string BestKey = "Chroma.Best.";

    /// <summary>اسم المرحلة للعرض (STEAM TOWN…)، أو اسم السين إن لم تكن منها.</summary>
    internal static string LevelName(string scene)
    {
        foreach (Level l in Levels) if (l.scene == scene) return l.name;
        return scene;
    }

    /// <summary>أفضل زمن محفوظ لهذه المرحلة، وصفر إن لم تُكمل بعد.</summary>
    internal static float Best(string scene) => PlayerPrefs.GetFloat(BestKey + scene, 0f);

    internal static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }

    private static ChromaMedals instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaMedals") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaMedals>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private int level = -1;
    private bool started, running;
    private float elapsed;
    private Canvas canvas;
    private CanvasGroup group;
    private TextMeshProUGUI clock, clockShade;
    private int shownSecond = -1;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaFunEvents.LevelLeft += OnLevelLeft;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaFunEvents.LevelLeft -= OnLevelLeft;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(scene);
    }

    private void Begin(Scene scene)
    {
        level = -1;
        for (int i = 0; i < Levels.Length; i++) if (Levels[i].scene == scene.name) level = i;
        started = running = false;
        elapsed = 0f;
        shownSecond = -1;
        if (canvas != null) canvas.enabled = false;
    }

    private void Update()
    {
        if (level < 0) return;
        if (!started)
        {
            if (ChromaEvents.Quiet) return;
            started = running = true;
            Build();
        }

        if (running) elapsed += Time.deltaTime;   // الإيقاف يوقفه وحده (deltaTime = 0)

        bool show = running && !ChromaEvents.Quiet && Time.timeScale > 0f;
        if (canvas == null) return;
        if (canvas.enabled != show) canvas.enabled = show;
        int second = Mathf.FloorToInt(elapsed);
        if (show && second != shownSecond)
        {
            shownSecond = second;
            string text = Clock(elapsed);
            clock.text = clockShade.text = text;
        }
    }

    private void OnLevelLeft(string from, string to)
    {
        if (level < 0 || !running || Levels[level].scene != from) return;
        running = false;
        if (canvas != null) canvas.enabled = false;

        Level l = Levels[level];
        int medal = elapsed <= l.gold ? 3 : elapsed <= l.silver ? 2 : elapsed <= l.bronze ? 1 : 0;
        float best = PlayerPrefs.GetFloat(BestKey + l.scene, 0f);
        bool newBest = best <= 0f || elapsed < best;
        if (newBest)
        {
            best = elapsed;
            PlayerPrefs.SetFloat(BestKey + l.scene, best);
            PlayerPrefs.Save();
        }

        // البطاقة في الهب (ChromaLevelStats) تعرض الميدالية مع إحصائيات المرحلة
        ChromaFunEvents.RaiseMedal(l.scene, medal, elapsed, newBest);
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "Clock", -1, out group);   // تحت ستائر السين
        Color paper = ChromaWardrobeArt.Paper, ink = ChromaWardrobeArt.Ink;
        clockShade = ChromaWardrobeArt.NewText(canvas.transform, "Shade", false, 38f, new Color(ink.r, ink.g, ink.b, 0.8f),
                                               TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(clockShade, new Vector2(0, 1), new Vector2(0, 1), new Vector2(43, -33), new Vector2(200, 50));
        clock = ChromaWardrobeArt.NewText(canvas.transform, "Time", false, 38f, paper, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(clock, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -30), new Vector2(200, 50));
    }
}

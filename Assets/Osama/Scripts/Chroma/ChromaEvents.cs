using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// لحظات اللعبة المهمّة في مكانٍ واحد — لمن يريد أن يحتفل بها (قطرات، مؤثّرات، صوت).
///
/// اللعبة فيها أكثر من نظامٍ لكل لحظة: نقاط حفظنا ونقاط علي، وأعلامٌ تُحمل بـ
/// <see cref="FlagItem"/> وأخرى بـ<see cref="FlagCarry"/> و<see cref="ExternalFlagPickup"/>،
/// وموتٌ من <see cref="PlayerKillable"/> وموتٌ من ماء علي. هنا تُجمع كلها، فمن يستمع
/// لا يحتاج أن يعرف أيّها وقع — ولا أن يعيد الانعكاس على سكربتات الآخرين.
///
/// <b>كل حدث يأتي بموضعه في العالم</b>: موضع اللاعب لحظتها، أو موضع الشيء نفسه إن عُرف.
/// واللحظة الواحدة التي يُبلغ عنها نظامان (علمٌ عليه FlagItem وFlagCarry معًا) تُطلق
/// مرّة واحدة.
/// </summary>
public static class ChromaEvents
{
    /// <summary>وصل اللاعب نقطة حفظ (نقاطنا ونقاط علي).</summary>
    public static event Action<Vector3> CheckpointReached;

    /// <summary>التقط علمًا — علم مرحلة أو علمًا داخل لغز.</summary>
    public static event Action<Vector3> FlagPickedUp;

    /// <summary>غرس علمًا: في قاعدته في الهب أو في مقبسه داخل مرحلة.</summary>
    public static event Action<Vector3> FlagPlanted;

    /// <summary>حُلّ لغز (الأعمدة الدوّارة، الساعة...).</summary>
    public static event Action<Vector3> PuzzleSolved;

    /// <summary>انفتحت بوابة.</summary>
    public static event Action<Vector3> GateOpened;

    /// <summary>مات اللاعب.</summary>
    public static event Action<Vector3> PlayerDied;

    /// <summary>
    /// سينٌ يُلعب فيه: ليس القائمة ولا الانترو. يُحسب مرّة عند تحميل كل سين.
    /// </summary>
    public static bool GameplayScene { get; private set; }

    /// <summary>
    /// <b>لا احتفال الآن</b>: قائمة أو انترو، أو شاشة تحميل فوق الشاشة، أو الكريديت يعمل.
    /// كل عدّادٍ ومؤثّرٍ فوق اللعب يسأل هذا قبل أن يظهر.
    /// </summary>
    public static bool Quiet => !GameplayScene || LoadingOverlay.IsBusy || GameCredits.Rolling;

    private const string MenuScene = "Hub-Menu";

    private static bool IsGameplay(Scene scene) =>
        scene.IsValid() && scene.name != MenuScene && scene.name != NewGameScene;

    /// <summary>أقلّ مدة بين حدثين من النوع نفسه — لحظة واحدة يُبلغ عنها نظامان.</summary>
    private const float Dedupe = 0.5f;

    private static float lastCheckpoint = -10f, lastPicked = -10f, lastPlanted = -10f,
                         lastSolved = -10f, lastGate = -10f, lastDied = -10f;

    private static FlagId lastCarried = FlagId.None;
    private static int lastPlantedCount;
    private static bool progressKnown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CheckpointReached = FlagPickedUp = FlagPlanted = PuzzleSolved = GateOpened = PlayerDied = null;
        GameplayScene = false;
        lastCheckpoint = lastPicked = lastPlanted = lastSolved = lastGate = lastDied = -10f;
        lastCarried = FlagId.None;
        lastPlantedCount = 0;
        progressKnown = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Checkpoint.Activated -= OnCheckpoint;
        Checkpoint.Activated += OnCheckpoint;
        PlayerKillable.Died -= OnDied;
        PlayerKillable.Died += OnDied;
        FlagItem.AnyPickedUp -= OnFlagItemPicked;
        FlagItem.AnyPickedUp += OnFlagItemPicked;
        FlagItem.AnyPlanted -= OnFlagItemPlanted;
        FlagItem.AnyPlanted += OnFlagItemPlanted;
        GameProgress.Changed -= OnProgress;
        GameProgress.Changed += OnProgress;
        GameProgress.ProgressReset -= OnProgressReset;
        GameProgress.ProgressReset += OnProgressReset;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= ChromaBank.Flush;
        Application.quitting += ChromaBank.Flush;

        GameplayScene = IsGameplay(SceneManager.GetActiveScene());
    }

    // ---------- من يُبلغ ----------

    public static void RaiseCheckpoint(Vector3 at) => Fire(CheckpointReached, ref lastCheckpoint, at);
    public static void RaiseFlagPickedUp(Vector3 at) => Fire(FlagPickedUp, ref lastPicked, at);
    public static void RaiseFlagPlanted(Vector3 at) => Fire(FlagPlanted, ref lastPlanted, at);
    public static void RaisePuzzleSolved(Vector3 at) => Fire(PuzzleSolved, ref lastSolved, at);
    public static void RaiseGateOpened(Vector3 at) => Fire(GateOpened, ref lastGate, at);
    public static void RaisePlayerDied(Vector3 at) => Fire(PlayerDied, ref lastDied, at);

    /// <summary>موضع اللاعب الآن، أو <paramref name="fallback"/> إن لم يوجد.</summary>
    public static Vector3 PlayerPosition(Vector3 fallback = default)
    {
        GameObject player = PlayerLocator.Find("Player");
        return player != null ? player.transform.position : fallback;
    }

    private static void Fire(Action<Vector3> e, ref float last, Vector3 at)
    {
        float now = Time.unscaledTime;
        if (now - last < Dedupe) return;
        last = now;

        if (e == null) return;
        // مستمعٌ واحدٌ معطوب لا يُسكت البقيّة ولا يكسر من أطلق الحدث
        foreach (Action<Vector3> listener in e.GetInvocationList())
        {
            try { listener(at); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    // ---------- المصادر ----------

    private static void OnCheckpoint(Checkpoint cp) =>
        RaiseCheckpoint(cp != null ? cp.transform.position : PlayerPosition());

    private static void OnDied() => RaisePlayerDied(PlayerPosition());

    private static void OnFlagItemPicked(FlagItem flag) =>
        RaiseFlagPickedUp(flag != null ? flag.transform.position : PlayerPosition());

    private static void OnFlagItemPlanted(FlagItem flag) =>
        RaiseFlagPlanted(flag != null ? flag.transform.position : PlayerPosition());

    /// <summary>
    /// أعلام المراحل تمرّ كلها بـ<see cref="GameProgress"/> أيًّا كان سكربتها، فنقارن
    /// حالته بما قبلها: علمٌ محمول جديد = التقاط، عدد مغروس زاد = غرس.
    /// </summary>
    private static void OnProgress()
    {
        GameProgress p = GameProgress.Instance;
        if (p == null) return;

        FlagId carried = p.CarriedFlag;
        int planted = p.PlantedCount;

        if (progressKnown)
        {
            if (carried != FlagId.None && carried != lastCarried) RaiseFlagPickedUp(PlayerPosition());
            if (planted > lastPlantedCount) RaiseFlagPlanted(PlayerPosition());
        }

        lastCarried = carried;
        lastPlantedCount = planted;
        progressKnown = true;
    }

    private static void OnProgressReset()
    {
        lastCarried = FlagId.None;
        lastPlantedCount = 0;
        progressKnown = true;
        ChromaBank.ResetRun();
    }

    /// <summary>كل لعبةٍ جديدة تمرّ بالانترو — زرّ اللعب في القائمة يأخذ إليه وحده.</summary>
    private const string NewGameScene = "Intro";

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ChromaBank.Flush();
        if (mode == LoadSceneMode.Single) GameplayScene = IsGameplay(scene);

        // الرجوع للقائمة من الإيقاف لا يمسح التقدّم، فكان اللاعب التالي يبدأ بأعلام من
        // قبله ويظهر في ستيم عند مدخلٍ غير البداية. اللعبة الجديدة تبدأ من الانترو دائمًا
        if (mode == LoadSceneMode.Single && scene.name == NewGameScene)
        {
            GameProgress.Instance.ResetProgress();
            return;
        }

        // حالة التقدّم تُقرأ من جديد كي لا يُحسب ما حُمّل من القرص التقاطًا
        GameProgress p = GameProgress.Instance;
        if (p != null)
        {
            lastCarried = p.CarriedFlag;
            lastPlantedCount = p.PlantedCount;
            progressKnown = true;
        }
    }
}

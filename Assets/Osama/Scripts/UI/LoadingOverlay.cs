using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// شاشة تحميل تعيش فوق كل المشاهد: صورة الوجهة، وبار يعبّي، وعلي يركض فوقه.
///
/// تُستدعى بسطر واحد من أي مكان: <c>LoadingOverlay.Go("Hub")</c>.
///
/// لماذا Overlay لا مشهد تحميل مستقل: <see cref="ScreenFader"/> لا يعيش بين المشاهد،
/// ولكل مشهد نسخته الخاصة، فلو كانت الشاشة كائنًا عاديًا لاختفت لحظة التحميل — وهي
/// بالضبط اللحظة التي نريدها فيها. ولأن مشهدًا مستقلًا يعني تعديل قائمة البناء
/// المشتركة وتحميلين متتاليين بدل واحد.
///
/// لماذا تبني واجهتها بالكود ولا تستعمل بريفاب: البريفاب يحتاج ربطًا يدويًا في
/// المحرر ومراجع قد تسقط من البلد، وهذا السكربت يعمل في أي مشهد بلا أن يضيف أحد
/// شيئًا — يكفي أن تُنادى. الصور تُحمَّل من <c>Assets/Osama/Resources/Loading</c>
/// فتدخل البناء دائمًا (نفس درس <c>OsamaGroundMarker.mat</c>).
///
/// القيم أدناه تظهر في الـ Inspector أثناء التشغيل (الكائن باقٍ بين المشاهد) فتُجرَّب
/// حيًّا، ولتثبيتها غيّر القيمة الافتراضية هنا في السكربت.
/// </summary>
[DisallowMultipleComponent]
public class LoadingOverlay : MonoBehaviour
{
    /// <summary>وجهة واحدة: أي مشهد، وأي صورة تمثّله، وما يُكتب تحت البار.</summary>
    [System.Serializable]
    public struct Destination
    {
        [Tooltip("اسم المشهد كما هو في قائمة البناء")]
        public string sceneName;

        [Tooltip("اسم الصورة داخل Osama/Resources/Loading بلا امتداد")]
        public string imageName;

        [Tooltip("ما يُكتب تحت البار — لاتيني، فخط يونيتي المدمج بلا حروف عربية")]
        public string title;

        public Destination(string sceneName, string imageName, string title)
        {
            this.sceneName = sceneName;
            this.imageName = imageName;
            this.title = title;
        }
    }

    [Header("التوقيت")]
    [Tooltip("مدة امتلاء البار (ثواني) — استعراضية، لا تتبع سرعة التحميل الحقيقية")]
    [SerializeField] private float barDuration = 6f;
    [Tooltip("ظهور الشاشة")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [Tooltip("اختفاء الشاشة — طوّلها إن رأيت سوادًا بين الشاشة والمشهد")]
    [SerializeField] private float fadeOutDuration = 0.45f;
    [Tooltip("انتظار بعد تفعيل المشهد ليمرّ Start فيه (نقل اللاعب) قبل أن تنكشف الشاشة")]
    [SerializeField] private float holdAfterActivate = 0.15f;
    [Tooltip("خفوت صوت المشهد الذي نغادره. أطول من ظهور الشاشة عمدًا: يذوب الصوت تحتها بدل " +
             "أن ينقطع لحظة تغطّي. ويدخل صوت الوجهة وحده بعد التفعيل (ChromaAudioFade)")]
    [SerializeField] private float audioFadeOut = 0.9f;

    [Header("الشكل")]
    [Tooltip("تعتيم فوق الصورة ليقرأ البار والنص")]
    [SerializeField, Range(0f, 1f)] private float artDim = 0.3f;
    [Tooltip("تكبير بطيء للصورة أثناء التحميل — صفر يوقفه")]
    [SerializeField, Range(0f, 0.3f)] private float artZoom = 0.05f;
    [Tooltip("عرض البار وارتفاعه بمقاس 1920x1080")]
    [SerializeField] private Vector2 barSize = new Vector2(1100f, 16f);
    [Tooltip("ارتفاع البار عن أسفل الشاشة")]
    [SerializeField] private float barBottom = 130f;
    [Tooltip("لون تعبئة البار")]
    [SerializeField] private Color barColor = new Color(1f, 0.83f, 0.38f, 1f);
    [Tooltip("ارتفاع علي على الشاشة")]
    [SerializeField] private float runnerHeight = 125f;
    [Tooltip("إطارات ركض علي في الثانية")]
    [SerializeField] private float runnerFps = 8f;
    [Tooltip("يذيب كل إطار في الذي بعده فتختفي القفزة بينهما")]
    [SerializeField] private bool blendFrames = true;
    [Tooltip("نطّة وميلٌ يتبعان دورة الركض — حركةٌ متّصلة تُخفي خطوات الإطارات")]
    [SerializeField] private float runnerBob = 7f;

    [Tooltip("ارتفاع رأس الشخصية في ركن الشاشة. صفر = بلا رأس")]
    [SerializeField] private float headHeight = 150f;
    [Tooltip("يكتب اسم الوجهة تحت البار")]
    [SerializeField] private bool showTitle = true;
    [Tooltip("سطر تلميح يتبدّل أسفل يسار الشاشة")]
    [SerializeField] private bool showTips = true;
    [Tooltip("كم يبقى كل تلميح")]
    [SerializeField] private float tipSeconds = 3.5f;

    [Header("الوجهات")]
    [Tooltip("تُملأ تلقائيًا بالافتراضي إن تُركت فارغة")]
    [SerializeField] private List<Destination> destinations = new List<Destination>();

    /// <summary>
    /// الوجهات الافتراضية. العناوين لاتينية عمدًا: الخط الوحيد المضمون داخل
    /// <c>Resources</c> هو LiberationSans وهو بلا حروف عربية، فالعربية تطلع مربّعات.
    /// </summary>
    private static readonly Destination[] DefaultDestinations =
    {
        new Destination("Hub",                   "Loading_Hub",      "THE HUB"),
        new Destination("Steam_Final",           "Loading_Steam",    "STEAM TOWN"),
        new Destination("AliLvl2_SultanVersion", "Loading_Twilight", "TWILIGHT"),
        new Destination("Main_Circus",           "Loading_Circus",   "THE CIRCUS"),
        new Destination("Intro",                 "Loading_Hub",      ""),
        new Destination("Hub-Menu",              "Loading_Hub",      "THE HUB"),
    };

    /// <summary>
    /// تلاميح تحت البار. لاتينية كالعناوين — الخط المدمج بلا حروف عربية — وكلها
    /// أشياء حقيقية في اللعبة: من كتب تلميحًا لا يصحّ أضاع لاعبًا.
    /// </summary>
    private static readonly string[] Tips =
    {
        "SQUARE INTERACTS  -  LEVERS, DOORS, FLAGS",
        "HOLD SQUARE TO PUSH AND PULL CRATES",
        "OPTIONS PAUSES  -  CIRCLE GOES BACK",
        "THE LIGHT GIVES THE WORLD ITS COLOUR BACK",
        "LAVA BURNS  -  THE SIGNS ARE THERE FOR A REASON",
        "CARRY EACH FLAG BACK TO THE HUB",
        "A CHECKPOINT IS WHERE YOU WILL COME BACK",
    };

    private const string ArtFolder = "Loading/";
    private const int MaxRunFrames = 24;

    private static LoadingOverlay instance;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform canvasRect;
    private Image art;
    private Image dim;
    private RectTransform barTrack;
    private RectTransform barFill;
    private RectTransform runner;
    private Image runnerImage;
    private Image runnerGhost;
    private Text tip;
    private int tipIndex = -1;
    private float tipTimer;
    private TurningHead head;
    private Text label;

    private Sprite[] runFrames;
    private float fill;
    private float frameTimer;
    private int frameIndex;
    private bool busy;

    /// <summary>هل هناك انتقال جارٍ الآن؟</summary>
    public static bool IsBusy => instance != null && instance.busy;

    /// <summary>
    /// تصفير الحالات الساكنة عند كل تشغيل — ضروري مع
    /// "Enter Play Mode without Domain Reload" وإلا بقي مرجع كائن ميّت.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>الشاشة — تُنشئ نفسها عند أول طلب، فلا يحتاج أي مشهد أن يضيفها.</summary>
    private static LoadingOverlay Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindFirstObjectByType<LoadingOverlay>();
            if (instance == null)
                // RectTransform من البداية: الكانفس يحتاجه، والكائن العادي يولد بـTransform
                new GameObject("LoadingOverlay (تلقائي)", typeof(RectTransform))
                    .AddComponent<LoadingOverlay>();

            return instance;   // ضُبط داخل Awake لحظة AddComponent
        }
    }

    /// <summary>
    /// يعرض الشاشة ويحمّل المشهد. يرجّع <c>false</c> إن لم يستطع — عندها ينفّذ
    /// المنادي طريقه القديم، فلا يعلق اللاعب بلا انتقال.
    /// </summary>
    public static bool Go(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[LoadingOverlay] اسم السين فارغ.");
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LoadingOverlay] السين \"{sceneName}\" غير موجود في قائمة " +
                           "مشاهد البناء — أضِفه من File → Build Profiles → Scene List.");
            return false;
        }

        LoadingOverlay overlay = Instance;
        if (overlay == null) return false;
        if (overlay.busy) return true;   // نداء ثانٍ أثناء انتقال: تجاهله ولا تُرجِع المنادي لطريقه

        overlay.StartCoroutine(overlay.Run(sceneName));
        return true;
    }

    private void Awake()
    {
        // نسخة ثانية (رجعنا للقائمة مثلًا) — الأصلية هي المرجع
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);     // DontDestroyOnLoad لا تقبل إلا كائنًا جذرًا
        DontDestroyOnLoad(gameObject);

        if (destinations == null || destinations.Count == 0)
            destinations = new List<Destination>(DefaultDestinations);

        LoadRunFrames();
        Build();
        Hide();
    }

    // ───────────────────────────── بناء الواجهة ─────────────────────────────

    /// <summary>يبني الكانفس وطبقاته مرة واحدة: خلفية، صورة، تعتيم، بار، علي، نص.</summary>
    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;    // فوق ScreenFader وكل واجهات المشاهد

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();

        // الكانفس ينشئ RectTransform بنفسه، لكن التحويل الآمن يكفينا شرّ الحالة النادرة
        // التي يُركّب فيها السكربت يدويًا على كائن عادي
        canvasRect = transform as RectTransform;

        // خلفية سوداء: تملأ ما زاد عن الصورة على شاشة بنسبة غريبة
        Stretch(NewImage("Backdrop", Color.black).rectTransform);

        // الصورة: تُقاس يدويًا لتغطّي الشاشة (Cover) بدل أن تُترك بأشرطة سوداء
        art = NewImage("Art", Color.white);
        art.preserveAspect = false;    // التغطية تتم بالمقاس لا بالخاصية
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        art.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        dim = NewImage("Dim", new Color(0f, 0f, 0f, artDim));
        Stretch(dim.rectTransform);

        BuildBar();
        BuildLabel();
        BuildTip();

        // آخر ما يُبنى فيُرسم فوق الجميع: ركن أسفل اليمين، صغيرًا، يلتفت وهي تُحمِّل
        if (headHeight > 0f)
            head = TurningHead.Build(transform, headHeight, new Vector2(1f, 0f),
                                     new Vector2(78f, 58f));
    }

    private void BuildBar()
    {
        Image track = NewImage("BarTrack", new Color(0f, 0f, 0f, 0.55f));
        barTrack = track.rectTransform;
        barTrack.anchorMin = barTrack.anchorMax = new Vector2(0.5f, 0f);
        barTrack.pivot = new Vector2(0.5f, 0f);
        barTrack.anchoredPosition = new Vector2(0f, barBottom);
        barTrack.sizeDelta = barSize;

        // التعبئة بعرض RectTransform لا بـ Image.type = Filled: الأخيرة تحتاج سبرايت
        // مقصوصًا، وهذه تعمل بلون صافٍ بلا أي أصل
        Image fillImage = NewImage("BarFill", barColor);
        barFill = fillImage.rectTransform;
        barFill.SetParent(barTrack, false);
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.anchoredPosition = Vector2.zero;
        barFill.sizeDelta = new Vector2(0f, 0f);

        // الطيف يُضمّ قبل الراكض فيصير أخاه الأكبر ويُرسم خلفه: يحمل الإطار السابق
        // ويخفت، فالانتقال بين إطارين يذوب بدل أن يقفز
        runnerGhost = NewImage("RunnerGhost", Color.white);
        runnerGhost.enabled = false;
        RectTransform ghost = runnerGhost.rectTransform;
        ghost.SetParent(barTrack, false);

        runnerImage = NewImage("Runner", Color.white);
        runner = runnerImage.rectTransform;
        runner.SetParent(barTrack, false);
        runner.anchorMin = runner.anchorMax = new Vector2(0f, 0.5f);
        runner.pivot = new Vector2(0.5f, 0f);   // قدماه على خط البار
        runner.sizeDelta = RunnerSize();

        // الطيف صورةٌ من الراكض تمامًا، وإلا انزلق تحته بدل أن يذوب فيه
        ghost.anchorMin = runner.anchorMin;
        ghost.anchorMax = runner.anchorMax;
        ghost.pivot = runner.pivot;
        ghost.sizeDelta = runner.sizeDelta;
        runnerImage.enabled = runFrames != null && runFrames.Length > 0;
        if (runnerImage.enabled) runnerImage.sprite = runFrames[0];
    }

    /// <summary>
    /// اسم الوجهة تحت البار بـ<c>UI.Text</c> وخط يونيتي المدمج، لا TextMeshPro.
    ///
    /// TMP رسم مربّعات وردية: إعدادات TMP في هذا المشروع بلا خط افتراضي، والخط
    /// المحمَّل من Resources مادّته بشيدر مفقود — والوردي في يونيتي معناه شيدر ضائع.
    /// الخط المدمج يمر على شيدر الواجهة العادي فيشتغل في المحرر والبلد بلا أي أصل
    /// ولا إعدادات، وهذا كل المطلوب من سطر واحد تحت بار التحميل.
    /// </summary>
    private void BuildLabel()
    {
        if (!showTitle) return;

        Font font = BuiltinFont();
        if (font == null)
        {
            Debug.LogWarning("[LoadingOverlay] ما لقيت خطًا مدمجًا — الشاشة بلا نص.");
            return;
        }

        var go = new GameObject("Title", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        label = go.AddComponent<Text>();
        label.font = font;
        label.fontSize = 28;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.UpperCenter;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, barBottom - 18f);
        rect.sizeDelta = new Vector2(1200f, 48f);
    }

    /// <summary>
    /// سطر التلميح أسفل يسار الشاشة. الشاشة كانت فارغة إلا من اسم الوجهة، وأربع
    /// ثوانٍ من فراغ طويلة — والتلميح يملؤها بشيء ينفع اللاعب لا بزينة.
    /// </summary>
    private void BuildTip()
    {
        if (!showTips) return;

        Font font = BuiltinFont();
        if (font == null) return;

        var go = new GameObject("Tip", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        tip = go.AddComponent<Text>();
        tip.font = font;
        tip.fontSize = 19;
        tip.alignment = TextAnchor.UpperCenter;
        tip.color = new Color(1f, 1f, 1f, 0f);
        tip.raycastTarget = false;
        tip.horizontalOverflow = HorizontalWrapMode.Overflow;
        tip.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = tip.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, TipBottom);
        rect.sizeDelta = new Vector2(1400f, 44f);

        NextTip();
    }

    /// <summary>تحت اسم الوجهة مباشرة، في الوسط — سطران يقرآن معًا لا شيئان متفرّقان.</summary>
    private float TipBottom => barBottom - 66f;

    /// <summary>تلميحٌ غير الذي قبله — التكرار في شاشةٍ قصيرة يُلاحَظ فورًا.</summary>
    private void NextTip()
    {
        if (tip == null || Tips.Length == 0) return;

        int pick = tipIndex;
        for (int guard = 0; guard < 8 && pick == tipIndex; guard++)
            pick = Random.Range(0, Tips.Length);

        tipIndex = pick;
        tipTimer = 0f;
        tip.text = Tips[pick];
    }

    /// <summary>خط يونيتي المدمج — موجود في كل بناء بلا استيراد.</summary>
    private static Font BuiltinFont()
    {
        // الاسم تغيّر في 2022.2: Arial.ttf صار LegacyRuntime.ttf، وطلب الاسم القديم
        // يطبع خطأ في الكونسول — فلا نجرّبه، ونسقط على خط النظام إن غاب
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        return font;
    }

    /// <summary>مسافة بين الحروف — UI.Text بلا تباعد، فنفرّقها بأنفسنا.</summary>
    private static string Spaced(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return string.Join(" ", text.ToCharArray());
    }

    private Image NewImage(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>إطارات الركض بالترتيب، ويتوقّف عند أول رقم ناقص — فزيادتها لا تحتاج كودًا.</summary>
    private void LoadRunFrames()
    {
        var frames = new List<Sprite>();
        for (int i = 1; i <= MaxRunFrames; i++)
        {
            Sprite frame = Resources.Load<Sprite>($"{ArtFolder}Run_{i:00}");
            if (frame == null) break;
            frames.Add(frame);
        }

        runFrames = frames.ToArray();
        if (runFrames.Length == 0)
            Debug.LogWarning("[LoadingOverlay] ما لقيت إطارات الركض في Osama/Resources/Loading " +
                             "— البار يشتغل بلا شخصية.");
    }

    private Vector2 RunnerSize()
    {
        float ratio = 171f / 256f;   // مقاس الإطارات المصدَّرة
        if (runFrames != null && runFrames.Length > 0 && runFrames[0] != null)
        {
            Rect r = runFrames[0].rect;
            if (r.height > 0f) ratio = r.width / r.height;
        }

        return new Vector2(runnerHeight * ratio, runnerHeight);
    }

    // ───────────────────────────── الانتقال ─────────────────────────────

    /// <summary>
    /// التسلسل كاملًا: ظهور → تحميل بلا تفعيل → بار أربع ثوانٍ → تفعيل → انتظار
    /// لحظة ليمرّ Start في الوجهة → اختفاء.
    ///
    /// والصوت يخفت مع الظهور ويبقى صامتًا طوال التحميل: المشهد القديم حيٌّ تحت الشاشة
    /// ثوانيَ، وموسيقاه كانت تعزف فوق صورة الوجهة ثم تنقطع لحظة التفعيل.
    /// </summary>
    private IEnumerator Run(string sceneName)
    {
        busy = true;
        ChromaAudioFade.FadeOut(audioFadeOut);
        Apply(Lookup(sceneName));
        fill = 0f;
        frameTimer = 0f;
        frameIndex = 0;
        NextTip();
        Layout(0f);

        canvas.enabled = true;
        group.blocksRaycasts = true;
        group.interactable = false;
        yield return FadeTo(1f, fadeInDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadingOverlay] فشل تحميل \"{sceneName}\".");
            ChromaAudioFade.FadeIn(audioFadeOut);   // بقينا حيث كنّا، فيعود صوته
            yield return FadeTo(0f, fadeOutDuration);
            Hide();
            busy = false;
            yield break;
        }

        // يونيتي يقف عند 0.9 وينتظر الإذن — بدون هذا يدخل المشهد قبل أن نُري شيئًا
        operation.allowSceneActivation = false;

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.unscaledDeltaTime;   // اللعبة قد تكون متوقفة (timeScale = 0)
            float byTime = barDuration > 0f ? elapsed / barDuration : 1f;
            bool ready = operation.progress >= 0.9f;

            // سين بطيء: البار يزحف ويقف قبل النهاية بدل أن يكذب ويكتمل وينتظر
            fill = Mathf.Clamp01(ready ? byTime : Mathf.Min(byTime, 0.95f));
            Layout(Time.unscaledDeltaTime);

            if (ready && byTime >= 1f) break;
            yield return null;
        }

        fill = 1f;
        Layout(0f);
        operation.allowSceneActivation = true;

        while (!operation.isDone) yield return null;

        // PlayerSpawnRouter ينقل اللاعب في Start — لولا هذه اللحظة لظهرت القفزة
        if (holdAfterActivate > 0f) yield return new WaitForSecondsRealtime(holdAfterActivate);

        yield return FadeTo(0f, fadeOutDuration);
        Hide();
        busy = false;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, t / duration);
            Layout(Time.unscaledDeltaTime);
            yield return null;
        }

        group.alpha = target;
    }

    private void Hide()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        canvas.enabled = false;
    }

    /// <summary>الوجهة المطابقة للمشهد، وإلا الأولى في القائمة حتى لا تبقى الشاشة فارغة.</summary>
    private Destination Lookup(string sceneName)
    {
        for (int i = 0; i < destinations.Count; i++)
            if (string.Equals(destinations[i].sceneName, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return destinations[i];

        Debug.LogWarning($"[LoadingOverlay] ما فيه صورة مربوطة بالسين \"{sceneName}\" — " +
                         "استعملت الافتراضية.");
        return destinations.Count > 0 ? destinations[0] : default;
    }

    private void Apply(Destination destination)
    {
        Sprite sprite = string.IsNullOrEmpty(destination.imageName)
            ? null
            : Resources.Load<Sprite>(ArtFolder + destination.imageName);

        art.sprite = sprite;
        art.enabled = sprite != null;
        dim.color = new Color(0f, 0f, 0f, artDim);

        if (label != null) label.text = Spaced(destination.title);
    }

    /// <summary>
    /// يضع كل شيء حسب التقدّم الحالي. يُنادى من الكوروتين لا من Update: الشاشة
    /// مخفيّة معظم الوقت، ولا داعي لأن تحسب شيئًا وهي مطفأة.
    /// </summary>
    private void Layout(float deltaTime)
    {
        CoverScreen();

        // القيم تُعاد كل إطار لا مرة واحدة: بلا بريفاب، الطريقة الوحيدة لضبطها هي
        // تغييرها في الـ Inspector أثناء التشغيل ورؤية الأثر فورًا
        barTrack.anchoredPosition = new Vector2(0f, barBottom);
        barTrack.sizeDelta = barSize;
        dim.color = new Color(0f, 0f, 0f, artDim);

        float width = barTrack.rect.width;
        barFill.sizeDelta = new Vector2(width * fill, 0f);

        if (label != null)
            label.rectTransform.anchoredPosition = new Vector2(0f, barBottom - 18f);

        if (tip != null)
            tip.rectTransform.anchoredPosition = new Vector2(0f, TipBottom);

        if (runnerImage != null && runnerImage.enabled)
        {
            runner.sizeDelta = RunnerSize();
            runner.anchoredPosition = new Vector2(width * fill, barSize.y * 0.5f);
            Animate(deltaTime);
        }

        head?.Turn(deltaTime);
        Tip(deltaTime);
    }

    /// <summary>يبدّل التلميح، ويُدخله ويُخرجه بتلاشٍ فلا يقفز نصٌّ مكان نصّ.</summary>
    private void Tip(float deltaTime)
    {
        if (tip == null || Tips.Length == 0) return;

        tipTimer += deltaTime;
        if (tipTimer >= tipSeconds) NextTip();

        const float Fade = 0.45f;
        float a = Mathf.Min(tipTimer / Fade, (tipSeconds - tipTimer) / Fade);
        tip.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a) * 0.62f);
    }

    /// <summary>يكبّر الصورة لتغطّي الشاشة كاملة بلا أشرطة، مع تكبير بطيء أثناء التحميل.</summary>
    private void CoverScreen()
    {
        if (art == null) return;

        float ratio = 16f / 9f;
        if (art.sprite != null && art.sprite.rect.height > 0f)
            ratio = art.sprite.rect.width / art.sprite.rect.height;

        Vector2 screen = canvasRect != null
            ? canvasRect.rect.size
            : new Vector2(Screen.width, Screen.height);
        float w = screen.x;
        float h = w / ratio;
        if (h < screen.y)
        {
            h = screen.y;
            w = h * ratio;
        }

        float zoom = 1f + artZoom * fill;
        art.rectTransform.sizeDelta = new Vector2(w * zoom, h * zoom);
    }

    /// <summary>
    /// مشية علي.
    ///
    /// الإطارات ثمانية فقط وتتشابه: الجذع والذراعان يكادان لا يتغيّران وكل الفرق في
    /// الساقين، فالعين تلمح <b>صورتين تتبادلان</b> لا شخصًا يركض. وثلاثة أشياء تُصلح
    /// ذلك بلا إطار واحد جديد:
    ///
    /// ١. <b>سرعة أعلى</b> — دورةٌ بطيئة بإطارات قليلة تُري كل قفزة؛ وإذا أسرعت
    ///    أكملت العين ما بينها.
    /// ٢. <b>مزجٌ بين الإطار والذي بعده</b> في أول ثلث مدّته: الانتقال يذوب بدل أن
    ///    يقفز. وقصرُ نافذة المزج مقصود — المزج الدائم يُري شبحًا مزدوجًا.
    /// ٣. <b>نطّة وميلٌ متّصلان</b> يتبعان الدورة: حركةٌ لا تتوقّف عند حدود الإطارات
    ///    فتُخفي خطواتها. وهي أيضًا ما يفتقده الرسم — القفزُ في الركض قبل الساقين.
    /// </summary>
    private void Animate(float deltaTime)
    {
        if (runFrames.Length < 2 || runnerFps <= 0f) return;

        frameTimer += deltaTime;
        float step = 1f / runnerFps;
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frameIndex = (frameIndex + 1) % runFrames.Length;
        }

        runnerImage.sprite = runFrames[frameIndex];

        float within = Mathf.Clamp01(frameTimer / step);   // أين نحن داخل هذا الإطار
        Ghost(within);
        Bounce(within);
    }

    /// <summary>الإطار السابق خلفه يخفت — فالقفزة بين الإطارين تصير ذوبانًا.</summary>
    private void Ghost(float within)
    {
        // نافذةٌ أوسع مع الإيقاع الأبطأ: كل إطار يبقى أطول، فالذوبان بينهما يحتاج
        // وقتًا أكثر وإلا عاد يُرى قفزةً في آخر الإطار
        const float Window = 0.55f;

        if (!blendFrames || runnerGhost == null)
        {
            if (runnerGhost != null) runnerGhost.enabled = false;
            return;
        }

        int previous = (frameIndex - 1 + runFrames.Length) % runFrames.Length;
        runnerGhost.sprite = runFrames[previous];
        runnerGhost.rectTransform.sizeDelta = runner.sizeDelta;
        runnerGhost.rectTransform.anchoredPosition = runner.anchoredPosition;
        runnerGhost.rectTransform.localRotation = runner.localRotation;

        float alpha = Mathf.Clamp01(1f - within / Window);
        runnerGhost.enabled = alpha > 0.01f;
        runnerGhost.color = new Color(1f, 1f, 1f, alpha);
    }

    /// <summary>
    /// النطّة على ضِعف تردّد الدورة — قدمٌ ثم قدم، وارتفاعةٌ بينهما — والميل يتبعها
    /// فيبدو مدفوعًا إلى الأمام.
    /// </summary>
    private void Bounce(float within)
    {
        if (runnerBob <= 0f) return;

        float cycle = (frameIndex + within) / runFrames.Length;   // 0..1 على الدورة
        float phase = cycle * Mathf.PI * 4f;                      // قدمان في الدورة

        float lift = Mathf.Abs(Mathf.Sin(phase)) * runnerBob;
        runner.anchoredPosition += new Vector2(0f, lift);
        runner.localRotation = Quaternion.Euler(0f, 0f, -3.5f - Mathf.Sin(phase) * 2.5f);

        if (runnerGhost != null && runnerGhost.enabled)
        {
            runnerGhost.rectTransform.anchoredPosition = runner.anchoredPosition;
            runnerGhost.rectTransform.localRotation = runner.localRotation;
        }
    }

    private void OnValidate()
    {
        barDuration = Mathf.Max(0f, barDuration);
        audioFadeOut = Mathf.Max(0f, audioFadeOut);
        runnerFps = Mathf.Max(0f, runnerFps);
        runnerBob = Mathf.Max(0f, runnerBob);
        tipSeconds = Mathf.Max(1f, tipSeconds);
        runnerHeight = Mathf.Max(1f, runnerHeight);
        headHeight = Mathf.Max(0f, headHeight);
        barSize = new Vector2(Mathf.Max(1f, barSize.x), Mathf.Max(1f, barSize.y));
    }
}

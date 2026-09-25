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
    [SerializeField] private float barDuration = 4f;
    [Tooltip("ظهور الشاشة")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [Tooltip("اختفاء الشاشة — طوّلها إن رأيت سوادًا بين الشاشة والمشهد")]
    [SerializeField] private float fadeOutDuration = 0.45f;
    [Tooltip("انتظار بعد تفعيل المشهد ليمرّ Start فيه (نقل اللاعب) قبل أن تنكشف الشاشة")]
    [SerializeField] private float holdAfterActivate = 0.15f;

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
    [SerializeField] private float runnerFps = 12f;
    [Tooltip("يكتب اسم الوجهة تحت البار")]
    [SerializeField] private bool showTitle = true;

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

        runnerImage = NewImage("Runner", Color.white);
        runner = runnerImage.rectTransform;
        runner.SetParent(barTrack, false);
        runner.anchorMin = runner.anchorMax = new Vector2(0f, 0.5f);
        runner.pivot = new Vector2(0.5f, 0f);   // قدماه على خط البار
        runner.sizeDelta = RunnerSize();
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
    /// </summary>
    private IEnumerator Run(string sceneName)
    {
        busy = true;
        Apply(Lookup(sceneName));
        fill = 0f;
        frameTimer = 0f;
        frameIndex = 0;
        Layout(0f);

        canvas.enabled = true;
        group.blocksRaycasts = true;
        group.interactable = false;
        yield return FadeTo(1f, fadeInDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"[LoadingOverlay] فشل تحميل \"{sceneName}\".");
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

        if (runnerImage != null && runnerImage.enabled)
        {
            runner.sizeDelta = RunnerSize();
            runner.anchoredPosition = new Vector2(width * fill, barSize.y * 0.5f);
            Animate(deltaTime);
        }
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
    }

    private void OnValidate()
    {
        barDuration = Mathf.Max(0f, barDuration);
        runnerFps = Mathf.Max(0f, runnerFps);
        runnerHeight = Mathf.Max(1f, runnerHeight);
        barSize = new Vector2(Mathf.Max(1f, barSize.x), Mathf.Max(1f, barSize.y));
    }
}

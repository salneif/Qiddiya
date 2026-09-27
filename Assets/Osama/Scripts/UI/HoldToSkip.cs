using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// تخطّي الانترو بإمساك أي زرّ خمس ثوانٍ، وبارٌ تحت الشاشة يعدّها.
///
/// زرّ التخطّي في سين الانترو يُنقر بالماوس، ولا شيء يُحدّده — فمن بيده يد التحكّم
/// كان يرى الزرّ ولا يقدر يضغطه. والنقرة الواحدة تُنهي الفيديو بالغلط: يد تسقط على
/// الماوس وقد ذهب الافتتاح.
///
/// فهذا يحلّ الاثنين: <b>أي زرّ</b> — كيبورد أو يد أو ماوس — يبدأ العدّ، والخمس ثوانٍ
/// تجعل التخطّي قصدًا لا زلّة. لا يحتاج تحديدًا ولا <c>EventSystem</c>، فلا فرق بين
/// اليد والكيبورد هنا.
///
/// كل شيء مبنيّ بالكود: كانفس ونصّ وبار. ولا نلمس سين علي ولا سكربته — نناديه هو
/// (<c>VideoSkipButton.SkipVideo</c>) بالاسم عبر الانعكاس ليبقى الانتقال انتقاله،
/// وإن غاب نحمّل السين المكتوب في حقله.
///
/// ويُركّب نفسه تلقائيًا، لكنه لا يظهر إلا في سين فيه <c>VideoPlayer</c> — أي حيث
/// فيديو يُتخطّى فعلًا، لا في كل مشهد.
/// </summary>
[DisallowMultipleComponent]
public class HoldToSkip : MonoBehaviour
{
    private const float HoldSeconds = 5f;
    private const float AppearDelay = 1.2f;
    private const float FadeSeconds = 0.9f;
    private const float ReleaseDrain = 3f;      // التصفير أسرع من العدّ: رفع اليد قرار
    private const float BreathSeconds = 2.6f;   // نفَسٌ بطيء في الانتظار
    private const float LingerSeconds = 4f;     // كم يبقى معروضًا قبل أن ينسحب
    private const float LeaveSeconds = 1.1f;    // تعتيمٌ قبل المغادرة
    private const float ArriveSeconds = 0.7f;   // وانكشافٌ بعد الوصول

    /// <summary>
    /// وجهة الانترو. سكربتا علي يعودان بها إلى <c>Hub-Menu</c> — أي أن اللاعب يضغط
    /// «العب» فيرى الفيلم ثم يجد نفسه في القائمة مرّة أخرى، بلا لعب. وأول مرحلة في
    /// اللعبة هي ستيم تاون، فإليها يذهب.
    /// </summary>
    private const string FirstLevel = "Steam_Final";

    /// <summary>
    /// حبرٌ أسود لا أبيض: خلفية الانترو ورقٌ كريميّ، والأبيض عليه لا يُرى.
    /// ومعه هالةٌ فاتحة رقيقة، فلو أظلمت لقطةٌ لاحقًا بقي مقروءًا.
    /// </summary>
    private static readonly Color Ink = new Color(0.07f, 0.06f, 0.05f, 1f);
    private static readonly Color Halo = new Color(1f, 0.99f, 0.96f, 0.55f);

    private static HoldToSkip instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("HoldToSkip") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<HoldToSkip>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        cachedWhite = null;
    }

    private MonoBehaviour skipper;          // VideoSkipButton الخاص بعلي
    private MonoBehaviour ender;            // A_AfterIntro — ينقل وحده حين ينتهي العدّ
    private FieldInfo enderClock;
    private GameObject skipButton;          // زرّه، نُخفيه فما يبقى تخطّيان
    private Canvas canvas;
    private CanvasGroup group;
    private Image fill;
    private Image black;
    private Text label;
    private float volume = 1f;

    private float held;
    private float shown;
    private float linger;
    private float alpha;
    private bool skipping;
    private bool scanned;

    private void OnEnable() =>
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() =>
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                               UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        scanned = false;
        held = shown = linger = alpha = 0f;

        if (group != null) group.alpha = 0f;

        // غادرنا في سواد: نكشفه هنا لا في السين السابق، وإلا رأى اللاعب ومضة
        // الانتقال نفسها — وهي ما كان يفاجئه
        if (skipping) { StartCoroutine(Arrive()); return; }

        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!scanned) Scan();
        if (canvas == null || !canvas.gameObject.activeSelf || skipping) return;

        // الفيديو انتهى من نفسه: لا شيء يُتخطّى بعد
        if (skipper == null) { canvas.gameObject.SetActive(false); return; }

        float step = Time.unscaledDeltaTime;
        shown += step;

        bool touching = Holding();

        // اللمس يُعيده ويمدّ بقاءه. وإلا انسحب بعد ثوانٍ — فلا يقعد على الشاشة طول
        // الفيديو كأنه شيء علق فيها
        bool arrived = shown > AppearDelay;
        if (arrived) linger = touching ? LingerSeconds : linger - step;

        bool wanted = arrived && linger > 0f;

        // الوصول للهدف بمنحنى لا بخطّ: الخطّي يظهر دفعةً ثم يقف، فيبدو معلّقًا
        alpha = Mathf.MoveTowards(alpha, wanted ? 1f : 0f, step / FadeSeconds);
        float entered = alpha * alpha * (3f - 2f * alpha);

        bool holding = touching && entered > 0.35f;

        held = holding
            ? held + step
            : Mathf.Max(0f, held - step * ReleaseDrain);

        float progress = Mathf.Clamp01(held / HoldSeconds);
        fill.fillAmount = progress;

        // نفَسٌ بطيء في الانتظار فلا يبدو صورةً واقفة، ويثبت تمامًا عند الإمساك
        float breath = Mathf.Lerp(0.62f, 1f,
            Mathf.Sin(shown / BreathSeconds * Mathf.PI * 2f) * 0.5f + 0.5f);
        group.alpha = entered * Mathf.Lerp(breath, 1f, progress);

        // والحبر نفسه يغمق وهو ممسوك، فيعرف أن الإمساك مسموع
        label.color = Fade(Ink, Mathf.Lerp(0.72f, 1f, progress));
        fill.color = Fade(Ink, Mathf.Lerp(0.8f, 1f, progress));

        if (progress >= 1f) { Skip(); return; }

        // العدّ يقارب نهايته: نعتّم الآن، ونؤجّل عدّه إلى ما لا نهاية فيبقى النقل
        // واحدًا — نقلنا نحن، إلى المرحلة الأولى لا إلى القائمة
        if (ender != null && (float)enderClock.GetValue(ender) <= LeaveSeconds)
        {
            enderClock.SetValue(ender, float.MaxValue);
            skipping = true;
            StartCoroutine(Leave(load: true));
        }
    }

    private static Color Fade(Color color, float alpha) =>
        new Color(color.r, color.g, color.b, alpha);

    /// <summary>أي زرّ: كيبورد أو يد أو ماوس. العصيّ لا تُحسب إمساكًا.</summary>
    private static bool Holding()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed) return true;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;

        return InteractInput.AnyPadButtonHeld;
    }

    /// <summary>
    /// ننتقل بشاشة التحميل: صورةُ ستيم تاون واسمها تقولان للاعب إلى أين هو ذاهب،
    /// بدل أن يجد نفسه في مكان لم يُهيَّأ له.
    ///
    /// ولا نستعمل <c>VideoSkipButton.SkipVideo()</c> ولا <c>nextSceneName</c>: كلاهما
    /// يعود بالقائمة الرئيسية، وهذا ما كان يجعل «العب» يعرض الفيلم ثم يرجع من حيث بدأ.
    /// </summary>
    private void Skip()
    {
        skipping = true;
        StartCoroutine(Leave(load: true));
    }

    /// <summary>
    /// السواد قبل النقل لا بعده.
    ///
    /// النداء المباشر كان يقطع الفيديو في منتصف لقطة ويرمي السين التالي في وجه
    /// اللاعب في إطار واحد — صوتٌ يُقطع وصورةٌ تُستبدل بلا مهلة. فنعتّم ونخفت الصوت
    /// أولًا، والفيديو يكمل تحت السواد، ثم ننتقل.
    ///
    /// و<c>load</c> يكون false حين تكون نهاية الفيديو الطبيعية هي التي ستنقل: نعتّم
    /// لها ولا ننقل نحن، فالنقل يبقى نقلها.
    /// </summary>
    private IEnumerator Leave(bool load)
    {
        volume = AudioListener.volume;

        for (float t = 0f; t < LeaveSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / LeaveSeconds);
            k = k * k * (3f - 2f * k);

            black.color = Fade(Color.black, k);
            group.alpha *= 1f - k;               // التلميح ينطفئ مع اللقطة
            AudioListener.volume = volume * (1f - k);
            yield return null;
        }

        black.color = Color.black;
        AudioListener.volume = 0f;

        if (load) Load();
    }

    /// <summary>
    /// والانكشاف في السين الجديد: لو لم نفعل بقي السواد على شاشته، ولو أطفأناه دفعةً
    /// عادت الومضة التي عتّمنا من أجلها.
    /// </summary>
    private IEnumerator Arrive()
    {
        for (float t = 0f; t < ArriveSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / ArriveSeconds);
            black.color = Fade(Color.black, 1f - k * k * (3f - 2f * k));
            AudioListener.volume = Mathf.Lerp(0f, volume, k);
            yield return null;
        }

        AudioListener.volume = volume;
        black.color = Fade(Color.black, 0f);
        skipping = false;
        canvas.gameObject.SetActive(false);
    }

    private void Load()
    {
        if (LoadingOverlay.Go(FirstLevel)) return;

        // الشاشة رفضت (السين ليس في قائمة البناء مثلًا): ننقل بأنفسنا ولا نترك
        // اللاعب في سواد لا ينتهي
        UnityEngine.SceneManagement.SceneManager.LoadScene(FirstLevel);
    }

    // ---------- البناء ----------

    /// <summary>لا نظهر إلا حيث فيديو يُتخطّى: وجود <c>VideoPlayer</c> هو العلامة.</summary>
    private void Scan()
    {
        scanned = true;
        skipper = null;
        skipButton = null;

        if (FindAnyObjectByType<VideoPlayer>() == null) return;

        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null || component.GetType().Name != "VideoSkipButton") continue;

            skipper = component;
            skipButton = component.GetComponent<Button>()?.gameObject;
            break;
        }

        // ونهاية الفيديو الطبيعية تنقل هي الأخرى في إطار واحد، فنعتّم قبلها بمثل ما
        // نعتّم قبل التخطّي — وإلا كان الانتقالان اثنين بإحساسين
        ender = null;
        enderClock = null;
        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null || component.GetType().Name != "A_AfterIntro") continue;

            FieldInfo clock = component.GetType().GetField("_currentTime",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (clock != null && clock.FieldType == typeof(float))
            {
                ender = component;
                enderClock = clock;
            }
            break;
        }

        if (skipper == null) return;

        // زرّ النقرة وبارُنا لا يجتمعان: النقرة تُنهي الافتتاح بزلّة يد، وهي أصلًا
        // لا تعمل بيد التحكّم. فيبقى تخطٍّ واحد، مقصود، يعمل بكل شيء
        if (skipButton != null) skipButton.SetActive(false);

        if (canvas == null) Build();
        canvas.gameObject.SetActive(true);
        group.alpha = 0f;
        fill.fillAmount = 0f;
        held = shown = alpha = 0f;
        linger = LingerSeconds;
    }

    private void Build()
    {
        var root = new GameObject("HoldToSkip_Canvas");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;        // فوق الفيديو، وتحت شاشة التحميل (5000)

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite white = White();

        // التلميح في طبقة وحده، فالسواد بعده يغطّيه ولا يتبع شفافيته
        var promptRoot = new GameObject("Prompt", typeof(RectTransform));
        promptRoot.transform.SetParent(root.transform, false);
        Stretch((RectTransform)promptRoot.transform);

        group = promptRoot.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;      // لا يحجب شيئًا — نقرأ الأزرار بأنفسنا
        group.interactable = false;

        Transform prompt = promptRoot.transform;

        // هالةٌ فاتحة تحت الحوض بقليل: تفصل الحبر عن أي لقطة مهما كان لونها
        Panel(prompt, "Halo", white, Halo, new Vector2(566f, 16f), new Vector2(0f, 96f));

        // الحوض: شريط رقيق أسفل الشاشة، بعرض الثلث فيُقرأ ولا يزحم الصورة
        RectTransform track = Panel(prompt, "Track", white,
                                   Fade(Ink, 0.22f),
                                   new Vector2(560f, 10f), new Vector2(0f, 96f));

        RectTransform bar = Panel(track, "Fill", white, Ink,
                                  Vector2.zero, Vector2.zero);
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = Vector2.one;
        bar.sizeDelta = Vector2.zero;

        fill = bar.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        label = Label(prompt, "HOLD ANY BUTTON TO SKIP", new Vector2(0f, 134f));

        // حدٌّ فاتح حول الحروف: الحبر يبقى مقروءًا ولو أظلمت اللقطة تحته
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = Halo;
        outline.effectDistance = new Vector2(2f, -2f);

        // السواد آخر الأبناء فيُرسم فوق الجميع، ويعيش بين السينين معنا
        black = Panel(root.transform, "Black", white, Fade(Color.black, 0f),
                      Vector2.zero, Vector2.zero).GetComponent<Image>();
        Stretch((RectTransform)black.transform);
    }

    /// <summary>يملأ الشاشة كاملة مهما كان مقاسها.</summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private static RectTransform Panel(Transform parent, string name, Sprite sprite,
                                       Color color, Vector2 size, Vector2 offset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        return rect;
    }

    /// <summary>
    /// <c>UI.Text</c> بخطّ يونيتي المدمج لا TMP: TMP يحتاج شيدره وأصوله في البلد،
    /// وغيابها يرسم مربّعات وردية بلا خطأ واحد في الكونسول.
    /// </summary>
    private static Text Label(Transform parent, string message, Vector2 offset)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();

        // الاسم تغيّر في 2022.2: Arial.ttf صار LegacyRuntime.ttf
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = message;
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Ink;
        text.raycastTarget = false;

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(700f, 36f);
        rect.anchoredPosition = offset;
        return text;
    }

    private static Sprite cachedWhite;

    private static Sprite White()
    {
        if (cachedWhite != null) return cachedWhite;

        var texture = new Texture2D(4, 4) { hideFlags = HideFlags.HideAndDontSave };
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();

        cachedWhite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        cachedWhite.hideFlags = HideFlags.HideAndDontSave;
        return cachedWhite;
    }
}

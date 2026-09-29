using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// عدّاد القطرات في ركن الشاشة الأعلى الأيمن: قطرةٌ بلون آخر ما جُمع، وعدد قطرات هذه
/// اللعبة بخطّ الكرايون، وتحته «١٢ / ٦٤» — ما جُمع من قطرات هذا السين من أصلها.
///
/// <b>الكسب يُرى ويُحسّ</b>: الرقم ينتفض ويتدحرج إلى قيمته، والقطرة تومض، و«+١» يطفو
/// من جانبه، وشارة «x5!» تتراقص ما دامت سلسلة الالتقاط جارية. <b>والسكون يُريح</b>:
/// بعد ٤ ث بلا كسبٍ يخفت إلى ٣٥٪ — حاضرٌ لمن يبحث عنه، لا يزاحم المرحلة.
///
/// <b>يختفي</b> حين <see cref="ChromaEvents.Quiet"/> (القائمة، الانترو، التحميل، الكريديت)،
/// وبلا لاعب، وحين تتوقّف اللعبة (<c>timeScale = 0</c>) — إلا إن طلبته خزانة الأزياء
/// بـ<see cref="ForceVisible"/> لترى رصيدك وأنت تختار.
///
/// الركن محجوز: ٣٨٠×١٥٠ في مرجع ١٩٢٠×١٠٨٠، بهامش ٤٠ يمينًا و٣٠ أعلى — شريحة الأزياء
/// تحت ١٩٠ من الأعلى، والإعلانات في الوسط. كل الحركة بالزمن الحقيقي: يعمل والزمن موقوف.
/// </summary>
[DisallowMultipleComponent]
public class ChromaHud : MonoBehaviour
{
    /// <summary>يُظهر العدّاد واللعبة موقوفة — تضبطه خزانة الأزياء وهي مفتوحة وتعيده عند إغلاقها.</summary>
    public static bool ForceVisible { get; set; }

    private const int SortingOrder = 60;
    private const float Right = 40f, Top = 30f;
    private const float Width = 380f, Height = 150f;
    private const float IdleAfter = 4f, IdleAlpha = 0.35f;
    private const float PopSeconds = 0.9f, PopRise = 46f, PopMerge = 0.3f;
    private const int ComboBadgeAt = 3;

    // التخطيط داخل الركن (بكسلات المرجع، من الزاوية العليا اليمنى)
    private static readonly Vector2 IconAt = new Vector2(-42f, -44f);
    private const float IconSize = 84f;
    private const float CountRight = 92f, CountY = -44f, CountSize = 60f;
    private const float TallyRight = 96f, TallyY = -108f, TallySize = 30f;
    private const float PopY = -62f, PopSize = 40f;
    private const float BadgeSize = 42f;

    private static ChromaHud instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("ChromaHud") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaHud>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ForceVisible = false;
    }

    /// <summary>نصٌّ بظلّ حبرٍ خلفه — يُقرأ على السماء البيضاء وعلى الظلّ الأسود معًا.</summary>
    private sealed class Inked
    {
        public RectTransform rect;
        public TMP_Text face, shade;

        public void Set(string format, float a)
        {
            face.SetText(format, a);
            shade.SetText(format, a);
        }

        public void Set(string format, float a, float b)
        {
            face.SetText(format, a, b);
            shade.SetText(format, a, b);
        }

        public float Width => face.preferredWidth;
    }

    private sealed class Pop
    {
        public Inked label;
        public float at = -10f;
        public int amount;
        public float x;
    }

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform icon;
    private Image fill;
    private Inked count, tally, badge;
    private readonly Pop[] pops = new Pop[5];
    private int nextPop;
    private bool built, broken;

    private float alpha;
    private bool wasShown;
    private float lastGainAt = -10f, punchAt = -10f;
    private float shown;
    private int drawn = -1;
    private int drawnFound = -1, drawnTotal = -1;
    private float countWidth, tallyWidth, badgeWidth;
    private Color iconColor = Color.white;

    private bool badgeOn;
    private int badgeCombo;
    private float badgeAt = -10f, badgeBumpAt = -10f;

    private GameObject player;
    private float nextLookup;

    private void OnEnable()
    {
        ChromaBank.Gained += OnGained;
        ChromaBank.RunReset += OnRunReset;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ChromaBank.Gained -= OnGained;
        ChromaBank.RunReset -= OnRunReset;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        player = null;
        nextLookup = 0f;
        foreach (Pop p in pops)
        {
            if (p == null) continue;
            p.at = -10f;
            p.label.rect.gameObject.SetActive(false);
        }
    }

    private void OnGained(int amount, Vector3 at)
    {
        float now = Time.unscaledTime;
        lastGainAt = now;
        punchAt = now;
        iconColor = ChromaDropField.LastColor;
        if (built) Float(amount, now);
    }

    private void OnRunReset()
    {
        shown = 0f;
        drawn = -1;
    }

    // ---------- كل إطار ----------

    private void LateUpdate()
    {
        bool show = !ChromaEvents.Quiet && (Time.timeScale > 0f || ForceVisible) && HasPlayer();
        if (!built)
        {
            if (!show || broken) return;
            if (!TryBuild()) { broken = true; return; }
        }

        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

        // كلّما عاد للظهور (بعد التحميل، بعد الإيقاف) يظهر كاملًا لحظات — تذكيرٌ برصيدك ثم يخفت
        if (show && !wasShown) lastGainAt = now;
        wasShown = show;

        float target = !show ? 0f : ForceVisible || now - lastGainAt < IdleAfter ? 1f : IdleAlpha;
        alpha = Mathf.MoveTowards(alpha, target, dt * (target > alpha ? 5f : 2.5f));
        group.alpha = alpha;

        bool on = alpha > 0.001f;
        if (canvas.enabled != on) canvas.enabled = on;
        if (!on) return;

        Count(now, dt);
        Tally();
        Badge(now);
        Pops(now);
    }

    /// <summary>الرقم يتدحرج إلى قيمته وينتفض — ١٫٣ ثم يرتدّ تحت الواحد قليلًا ويستقرّ.</summary>
    private void Count(float now, float dt)
    {
        int run = ChromaBank.Run;
        if (run < shown) shown = run;
        else shown = Mathf.MoveTowards(shown, run, Mathf.Max(12f, (run - shown) * 8f) * dt);

        int value = Mathf.FloorToInt(shown + 0.001f);
        if (value != drawn)
        {
            drawn = value;
            count.Set("{0}", value);
            countWidth = count.Width;
        }

        float t = now - punchAt;
        float punch = 1f + 0.3f * Mathf.Exp(-9f * t) * Mathf.Cos(20f * t);
        count.rect.localScale = new Vector3(punch, punch, 1f);

        // القطرة تومض أبيض ثم تأخذ لون آخر ما جُمع، وترتجّ معه
        float flash = Mathf.Clamp01(t / 0.3f);
        fill.color = Color.Lerp(Color.white, iconColor, flash);
        float bounce = 1f + 0.2f * Mathf.Exp(-10f * t) * Mathf.Cos(24f * t);
        icon.localScale = new Vector3(bounce, bounce, 1f);
    }

    private void Tally()
    {
        int found = ChromaDropField.PlacedCollected, total = ChromaDropField.PlacedTotal;
        if (found == drawnFound && total == drawnTotal) return;
        drawnFound = found;
        drawnTotal = total;

        bool any = total > 0;
        if (tally.rect.gameObject.activeSelf != any) tally.rect.gameObject.SetActive(any);
        if (!any) { tallyWidth = 0f; return; }

        tally.Set("{0} / {1}", Mathf.Min(found, total), total);
        tallyWidth = tally.Width;
    }

    /// <summary>الشارة تقفز للظهور، وتنتفض مع كل قطرةٍ في السلسلة، وتتمايل ما دامت.</summary>
    private void Badge(float now)
    {
        int combo = ChromaDropField.Combo;
        bool want = combo >= ComboBadgeAt;

        if (want)
        {
            if (!badgeOn)
            {
                badgeOn = true;
                badgeAt = now;
                badge.rect.gameObject.SetActive(true);
            }
            if (combo != badgeCombo)
            {
                badgeCombo = combo;
                badge.Set("x{0}!", combo);
                badgeWidth = badge.Width;
                badgeBumpAt = now;
            }
        }
        else if (badgeOn)
        {
            badgeOn = false;
            badgeAt = now;
            badgeCombo = 0;
        }

        if (!badge.rect.gameObject.activeSelf) return;

        float t = now - badgeAt;
        float size;
        if (badgeOn)
        {
            size = BackOut(Mathf.Clamp01(t / 0.25f)) * (1f + 0.25f * Mathf.Exp(-12f * (now - badgeBumpAt)));
        }
        else
        {
            size = 1f - Mathf.Clamp01(t / 0.18f);
            if (size <= 0f) { badge.rect.gameObject.SetActive(false); return; }
        }

        float right = TallyRight + (tallyWidth > 0f ? tallyWidth + 18f : 0f);
        badge.rect.anchoredPosition = new Vector2(-(right + badgeWidth * 0.5f), TallyY);
        badge.rect.localScale = new Vector3(size, size, 1f);
        badge.rect.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(now * 13f));
    }

    /// <summary>
    /// «+١» يطفو من يسار الرقم. كسبٌ يلحق كسبًا خلال لحظة يُضاف إلى الطافي نفسه —
    /// خمس عشرة قطرةً معًا تُقرأ «+15» لا خمسة عشر رقمًا متراكبًا.
    /// </summary>
    private void Float(int amount, float now)
    {
        Pop last = pops[(nextPop + pops.Length - 1) % pops.Length];
        if (last.label.rect.gameObject.activeSelf && now - last.at < PopMerge)
        {
            last.amount += amount;
            last.at = now;
            last.label.Set("+{0}", last.amount);
            Paint(last);
            return;
        }

        Pop p = pops[nextPop];
        nextPop = (nextPop + 1) % pops.Length;
        p.amount = amount;
        p.at = now;
        p.x = -(CountRight + countWidth + 12f);
        p.label.Set("+{0}", amount);
        Paint(p);
        p.label.rect.gameObject.SetActive(true);
    }

    private void Paint(Pop p) => p.label.face.color = Color.Lerp(iconColor, Color.white, 0.3f);

    private void Pops(float now)
    {
        foreach (Pop p in pops)
        {
            if (!p.label.rect.gameObject.activeSelf) continue;

            float t = now - p.at;
            if (t >= PopSeconds) { p.label.rect.gameObject.SetActive(false); continue; }

            float k = t / PopSeconds;
            float rise = 1f - (1f - k) * (1f - k);
            float size = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.12f) / 0.15f));
            float fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;

            p.label.rect.anchoredPosition = new Vector2(p.x, PopY + PopRise * rise);
            p.label.rect.localScale = new Vector3(size, size, 1f);
            SetAlpha(p.label, fade);
        }
    }

    private static void SetAlpha(Inked label, float a)
    {
        label.face.alpha = a;
        label.shade.alpha = a * 0.85f;
    }

    private bool HasPlayer()
    {
        if (player != null && player.activeInHierarchy) return true;
        if (Time.unscaledTime < nextLookup) return false;
        nextLookup = Time.unscaledTime + 0.5f;
        player = PlayerLocator.Find("Player");
        return player != null;
    }

    // ---------- البناء ----------

    /// <summary>عدّادٌ لم يُبنَ يُطفأ مرّة ويسكت — لا خطأٌ في الكونسول كل إطار.</summary>
    private bool TryBuild()
    {
        try
        {
            return Build();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e, this);
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
            return false;
        }
    }

    /// <summary>
    /// يُبنى أوّل ما يُحتاج، تحت جذرٍ مطفأ: TMP يقرأ خطّه في <c>Awake</c>، فلو صحا قبل أن
    /// نعطيه خطّ اللعبة لبحث عن خطّه الافتراضي — وقد لا يكون.
    /// </summary>
    private bool Build()
    {
        ChromaStyle style = ChromaStyle.Get();
        TMP_FontAsset body = style.bodyFont != null ? style.bodyFont : DefaultFont();
        TMP_FontAsset title = style.titleFont != null ? style.titleFont : body;
        if (body == null)
        {
            Debug.LogWarning("[ChromaHud] لا خطّ للعدّاد (ChromaStyle ولا TMP Settings) — العدّاد مطفأ.", this);
            return false;
        }

        var root = new GameObject("ChromaHud_Canvas");
        root.SetActive(false);
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;     // فوق واجهات السين، وتحت الإعلانات وشاشة التحميل

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;      // عدّادٌ لا زرّ: لا يبتلع نقرة
        group.interactable = false;
        group.alpha = 0f;

        // الركن: مثبّتٌ في الزاوية العليا اليمنى، فيبقى فيها في كل نسبة شاشة
        RectTransform corner = Rect("Corner", root.transform, new Vector2(1f, 1f), new Vector2(-Right, -Top),
                                    new Vector2(Width, Height));

        icon = Rect("Icon", corner, new Vector2(0.5f, 0.5f), IconAt, new Vector2(IconSize, IconSize));
        iconColor = style.Palette(0);
        // وجه الولد هو العدّاد (وحبره فيه)؛ وإن غاب رجعت القطرة بحبرها
        Sprite face = Resources.Load<Sprite>("Chroma/Face/FaceIcon");
        fill = Picture("Fill", icon, face != null ? face : Resources.Load<Sprite>("Chroma/Fx/DropFill"), iconColor);
        if (face == null) Picture("Ink", icon, Resources.Load<Sprite>("Chroma/Fx/DropInk"), style.ink);

        count = Label("Count", corner, body, CountSize, new Vector2(1f, 0.5f), new Vector2(-CountRight, CountY),
                      new Vector2(260f, 80f), TextAlignmentOptions.Right, style.paper, style.ink);
        tally = Label("Tally", corner, body, TallySize, new Vector2(1f, 0.5f), new Vector2(-TallyRight, TallyY),
                      new Vector2(220f, 38f), TextAlignmentOptions.Right, new Color(style.paper.r, style.paper.g, style.paper.b, 0.9f), style.ink);
        // محور الشارة في وسطها: تتمايل حول نفسها لا حول حافّتها كباب
        badge = Label("Combo", corner, title, BadgeSize, new Vector2(0.5f, 0.5f), new Vector2(-TallyRight, TallyY),
                      new Vector2(140f, 52f), TextAlignmentOptions.Center, style.gold, style.ink);
        badge.rect.gameObject.SetActive(false);
        tally.rect.gameObject.SetActive(false);

        for (int i = 0; i < pops.Length; i++)
        {
            Inked label = Label("Plus" + i, corner, body, PopSize, new Vector2(1f, 0.5f), new Vector2(-CountRight, PopY),
                                new Vector2(140f, 50f), TextAlignmentOptions.Right, Color.white, style.ink);
            label.rect.gameObject.SetActive(false);
            pops[i] = new Pop { label = label };
        }

        root.SetActive(true);
        canvas.enabled = false;
        built = true;
        return true;
    }

    private static TMP_FontAsset DefaultFont() =>
        TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;

    private static RectTransform Rect(string name, Transform parent, Vector2 pivot, Vector2 at, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
        return rect;
    }

    private static Image Picture(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = color;
        if (sprite == null) image.enabled = false;    // صورةٌ غائبة: مربّعٌ أبيض أسوأ من لا شيء
        return image;
    }

    private static Inked Label(string name, RectTransform parent, TMP_FontAsset font, float size, Vector2 pivot,
                               Vector2 at, Vector2 box, TextAlignmentOptions align, Color color, Color ink)
    {
        RectTransform rect = Rect(name, parent, pivot, at, box);
        var label = new Inked { rect = rect };
        label.shade = Text("Shade", rect, font, size, align, new Color(ink.r, ink.g, ink.b, 0.85f), new Vector2(3f, -4f));
        label.face = Text("Face", rect, font, size, align, color, Vector2.zero);
        return label;
    }

    private static TMP_Text Text(string name, RectTransform parent, TMP_FontAsset font, float size,
                                 TextAlignmentOptions align, Color color, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset;
        rect.offsetMax = offset;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        text.color = color;
        text.text = string.Empty;
        return text;
    }

    private static float BackOut(float x)
    {
        const float s = 1.7f;
        x -= 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }
}

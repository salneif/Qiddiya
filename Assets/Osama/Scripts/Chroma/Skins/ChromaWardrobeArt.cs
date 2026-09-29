using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// رسوم الخزانة ولافتة الأزياء، <b>تُولَّد بالكود</b> مرّة في الجلسة: مستطيلٌ مستدير
/// وإطاره، دائرة وحلقة، سهم، قفل، ورموز يد التحكّم.
///
/// بالكود لا بصور: الواجهة أشكالٌ بسيطة بلونٍ واحد، ودالّة المسافة ترسمها ناعمة الحوافّ
/// بأي مقاس — وصورةٌ لكل شكل تعني عشرة ملفّات وميتاها لما يُكتب في سطر. كلها بيضاء،
/// واللون من <see cref="Graphic.color"/>.
///
/// ومعها مصانع العناصر (كانفس، صورة، نصّ) بإعداداتٍ واحدة، فلا تنسى واجهةٌ منها
/// <c>raycastTarget</c> فتبتلع نقرةً ليست لها.
/// </summary>
public static class ChromaWardrobeArt
{
    /// <summary>حدود القطع التسعي لـ<see cref="Round"/> بالبكسل — أكبر من نصف القطر بهامش التنعيم.</summary>
    public const float RoundBorder = 26f;

    private static readonly List<Sprite> made = new List<Sprite>();
    private static Sprite round, roundLine, disc, ring, chevron, padlock, cross, dpad, fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        made.Clear();
        round = roundLine = disc = ring = chevron = padlock = cross = dpad = fade = null;
    }

    public static Color Ink => ChromaStyle.Get().ink;
    public static Color Paper => ChromaStyle.Get().paper;

    public static TMP_FontAsset BodyFont => Font(ChromaStyle.Get().bodyFont);
    public static TMP_FontAsset TitleFont => Font(ChromaStyle.Get().titleFont);

    private static TMP_FontAsset Font(TMP_FontAsset wanted) =>
        wanted != null ? wanted : TMP_Settings.defaultFontAsset;

    // ---------- الأشكال ----------

    /// <summary>مستطيلٌ مستدير الزوايا، يُقطع تسعيًّا — خلفيّات وأغطية مفاتيح.</summary>
    public static Sprite Round => round != null ? round
        : round = Make("Round", 64, 64, p => Cover(Box(p - Center(64), new Vector2(31f, 31f), 22f)), RoundBorder);

    /// <summary>إطار <see cref="Round"/> وحده، بنفس الحافّة الخارجية.</summary>
    public static Sprite RoundLine => roundLine != null ? roundLine
        : roundLine = Make("RoundLine", 64, 64, p =>
        {
            float d = Box(p - Center(64), new Vector2(31f, 31f), 22f);
            return Cover(Mathf.Max(d, -d - 5f));
        }, RoundBorder);

    public static Sprite Disc => disc != null ? disc
        : disc = Make("Disc", 128, 128, p => Cover((p - Center(128)).magnitude - 62f), 0f);

    public static Sprite Ring => ring != null ? ring
        : ring = Make("Ring", 128, 128, p => Cover(Mathf.Abs((p - Center(128)).magnitude - 55f) - 6f), 0f);

    /// <summary>سهمٌ يشير يمينًا. لليسار: مقياس x سالب.</summary>
    public static Sprite Chevron => chevron != null ? chevron
        : chevron = Make("Chevron", 64, 64, p =>
        {
            float d = Mathf.Min(Segment(p, new Vector2(22f, 12f), new Vector2(43f, 32f)),
                                Segment(p, new Vector2(43f, 32f), new Vector2(22f, 52f)));
            return Cover(d - 5f);
        }, 0f);

    public static Sprite Padlock => padlock != null ? padlock
        : padlock = Make("Padlock", 64, 64, p =>
        {
            float body = Box(p - new Vector2(32f, 22f), new Vector2(17f, 12f), 4f);
            // العروة: نصف دائرةٍ فوق ساقين قصيرتين تنزلان في الجسم
            Vector2 c = new Vector2(32f, 40f);
            float shackle = p.y >= c.y
                ? Mathf.Abs((p - c).magnitude - 11f) - 3.5f
                : Mathf.Min(Segment(p, new Vector2(21f, 30f), new Vector2(21f, 40f)),
                            Segment(p, new Vector2(43f, 30f), new Vector2(43f, 40f))) - 3.5f;
            float keyhole = (p - new Vector2(32f, 23f)).magnitude - 3.5f;
            return Cover(Mathf.Max(Mathf.Min(body, shackle), -keyhole));
        }, 0f);

    /// <summary>رمز الإكس في يد بلايستيشن.</summary>
    public static Sprite Cross => cross != null ? cross
        : cross = Make("Cross", 64, 64, p =>
        {
            float d = Mathf.Min(Segment(p, new Vector2(16f, 16f), new Vector2(48f, 48f)),
                                Segment(p, new Vector2(16f, 48f), new Vector2(48f, 16f)));
            return Cover(d - 4.5f);
        }, 0f);

    /// <summary>الأسهم في يد التحكّم: صليبٌ مستدير الأطراف.</summary>
    public static Sprite DPad => dpad != null ? dpad
        : dpad = Make("DPad", 64, 64, p =>
        {
            Vector2 q = p - Center(64);
            return Cover(Mathf.Min(Box(q, new Vector2(29f, 10f), 4f), Box(q, new Vector2(10f, 29f), 4f)));
        }, 0f);

    /// <summary>تعتيمٌ من اليسار يذوب نحو اليمين — خلف اللوحة، لا فوق اللاعب.</summary>
    public static Sprite Fade => fade != null ? fade
        : fade = Make("Fade", 64, 4, p =>
        {
            float k = 1f - Mathf.Clamp01((p.x - 8f) / 56f);
            return k * k * (3f - 2f * k);
        }, 0f);

    /// <summary>
    /// يحرّر ما وُلّد. تناديه الخزانة حين يُهدم كائنها (الخروج من اللعب): النسيج
    /// المولَّد لا يموت مع السين، وفي المحرر يبقى بعد إيقاف التشغيل.
    /// </summary>
    public static void Release()
    {
        foreach (Sprite sprite in made)
        {
            if (sprite == null) continue;
            if (sprite.texture != null) UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
        ResetStatics();
    }

    private static Sprite Make(string name, int width, int height, Func<Vector2, float> coverage, float border)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ChromaWardrobe_" + name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,   // تحميل سينٍ ينظّف ما لا مرجع له في المشهد
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float a = Mathf.Clamp01(coverage(new Vector2(x + 0.5f, y + 0.5f)));
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                                      100f, 0, SpriteMeshType.FullRect,
                                      new Vector4(border, border, border, border));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        made.Add(sprite);
        return sprite;
    }

    private static Vector2 Center(int size) => new Vector2(size * 0.5f, size * 0.5f);

    /// <summary>من المسافة (بكسل، سالبٌ داخل الشكل) إلى تغطية بحافّةٍ ناعمة بعرض بكسل.</summary>
    private static float Cover(float distance) => Mathf.Clamp01(0.5f - distance);

    private static float Box(Vector2 p, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + new Vector2(radius, radius);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
               + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    // ---------- العناصر ----------

    /// <summary>كانفس فوق الشاشة بمقياس اللعبة (1920×1080، مطابقة 0.5)، مطفأ حتى يُحتاج.</summary>
    public static Canvas NewCanvas(Transform parent, string name, int order, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;      // تُقاد بالكيبورد واليد، ولا تبتلع نقرة
        group.interactable = false;

        canvas.enabled = false;
        return canvas;
    }

    public static RectTransform NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
    {
        var image = NewRect(parent, name).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sliced) image.type = Image.Type.Sliced;
        return image;
    }

    public static TextMeshProUGUI NewText(Transform parent, string name, bool title, float size, Color color,
                                          TextAlignmentOptions alignment)
    {
        var text = NewRect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = title ? TitleFont : BodyFont;
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// صفٌّ أفقيّ للزرّ ونصّه بجانبه. <paramref name="fit"/> = يأخذ عرض محتواه بنفسه؛
    /// الصفّ داخل صفٍّ آخر لا يحتاجه — أبوه يسأله عن عرضه.
    /// </summary>
    public static RectTransform NewRow(Transform parent, string name, float spacing, bool fit = true)
    {
        RectTransform row = NewRect(parent, name);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (fit)
        {
            var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return row;
    }

    public static RectTransform Place(Component item, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)item.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>يملأ الأب، مزاحًا بـ<paramref name="shift"/> — للظلال.</summary>
    public static RectTransform Stretch(Component item, Vector2 shift = default)
    {
        var rect = (RectTransform)item.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = shift;
        rect.offsetMax = shift;
        return rect;
    }

    /// <summary>بطاقة ورقٍ بإطارٍ من الحبر وظلٍّ مزاح — هويّة واجهات اللون.</summary>
    public static void Card(RectTransform parent, float corners)
    {
        Color ink = Ink;

        Image shadow = NewImage(parent, "Shadow", Round, new Color(ink.r, ink.g, ink.b, 0.3f), true);
        shadow.pixelsPerUnitMultiplier = corners;
        Stretch(shadow, new Vector2(10f, -10f));

        Image paper = NewImage(parent, "Paper", Round, Paper, true);
        paper.pixelsPerUnitMultiplier = corners;
        Stretch(paper);

        Image line = NewImage(parent, "Line", RoundLine, ink, true);
        line.pixelsPerUnitMultiplier = corners;
        Stretch(line);
    }

    // ---------- الحركة ----------

    /// <summary>اقترابٌ أُسّيّ مستقلّ عن معدّل الإطارات — يلحق هدفه بسرعة ثم يلين.</summary>
    public static float Damp(float current, float target, float speed, float dt) =>
        Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));

    public static Color Damp(Color current, Color target, float speed, float dt) =>
        Color.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));

    /// <summary>يتجاوز هدفه قليلًا ثم يستقرّ — ظهورٌ بنطّة لا بانزلاق.</summary>
    public static float BackOut(float x)
    {
        const float s = 1.70158f;
        x = Mathf.Clamp01(x) - 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// زرٌّ مرسوم بجهاز اللاعب الآن: غطاء مفتاحٍ للكيبورد، أو زرٌّ من يد التحكّم.
///
/// الصور الجاهزة (<see cref="PromptIcons"/>) لا تغطّي Tab ولا Enter ولا زرّ الاختيار
/// في اليد، فيُرسم الزرّ هنا من أشكال <see cref="ChromaWardrobeArt"/>: وجهٌ ورقيّ فوق
/// قاعدةٍ من الحبر، كمفتاحٍ حقيقيّ — يُقرأ على الورق وعلى الشاشة المعتمة معًا. واسم زرّ
/// الاختيار <b>يُقرأ من اليد الموصولة</b> (Share في بلايستيشن، View في إكس بوكس)، فلا
/// نسمّيه باسمٍ ليس على يد اللاعب.
///
/// من يملكه ينادي <see cref="Refresh"/> عند <see cref="InputScheme.Changed"/>، والصفّ
/// الذي هو فيه يأخذ عرضه الجديد وحده.
///
/// <b>والأولى صور أسامة</b> (<c>Resources/Prompts</c>): مفتاح الكيبورد الأبيض (<c>KeyE</c>) ولوح اليد
/// الطباشيري (<c>PadE</c>) بكل رموزهما. الرسم أعلاه احتياطٌ لما لا صورة له (السهمان).
/// </summary>
public sealed class ChromaWardrobePrompt
{
    public enum Pad
    {
        Cross, Circle, DPad, Select,
        Square, Triangle, Options, R3, DPadUp, DPadDown, LeftStick, RightStick, Triggers,
    }

    private const string Icons = "Prompts/";

    private static readonly Color CrossBlue = new Color(0.55f, 0.72f, 1f);
    private static readonly Color CircleRed = new Color(1f, 0.46f, 0.46f);

    public RectTransform Rect { get; }

    private readonly float height;
    private readonly Pad pad;
    private readonly GameObject keys;
    private readonly RectTransform padArt;
    private readonly LayoutElement layout;
    private readonly float keysWidth;
    private readonly RectTransform selectCap;
    private readonly TextMeshProUGUI selectLabel;
    private string selectName;
    private readonly int padIcons;   // صورٌ لليد، أو صفرٌ والرسم احتياط

    /// <param name="keyLabels">
    /// غطاءٌ لكل عنصر. <c>"&lt;"</c> و<c>"&gt;"</c> سهمان مرسومان — خطّ اللعبة بلا هذين الحرفين.
    /// </param>
    public ChromaWardrobePrompt(Transform parent, float height, Pad pad, params string[] keyLabels)
    {
        this.height = height;
        this.pad = pad;

        Rect = ChromaWardrobeArt.NewRect(parent, "Prompt");
        Rect.sizeDelta = new Vector2(height, height);
        layout = Rect.gameObject.AddComponent<LayoutElement>();

        RectTransform keyRoot = Holder("Keys");
        keys = keyRoot.gameObject;

        const float gap = 6f;
        float x = 0f;
        foreach (string label in keyLabels)
        {
            RectTransform cap = Icon(keyRoot, KeyIcon(label));
            float width = height;
            if (cap == null)
            {
                cap = Keycap(keyRoot, label, out TextMeshProUGUI text);
                width = text != null ? Fit(cap, text) : height;
            }
            cap.anchoredPosition = new Vector2(x, 0f);
            x += width + gap;
        }
        keysWidth = Mathf.Max(height, x - gap);

        padArt = Holder("Pad");
        padIcons = 0;
        foreach (string name in PadIcons(pad))
        {
            RectTransform icon = Icon(padArt, Load("Pad" + name));
            if (icon == null) { padIcons = 0; break; }
            icon.anchoredPosition = new Vector2(padIcons * (height + gap), 0f);
            padIcons++;
        }
        if (padIcons == 0)
        switch (pad)
        {
            case Pad.Cross:
                Glyph(ChromaWardrobeArt.Cross, CrossBlue, 0.62f);
                break;
            case Pad.Circle:
                Glyph(ChromaWardrobeArt.Ring, CircleRed, 0.6f);
                break;
            case Pad.DPad:
                ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(padArt, "DPad", ChromaWardrobeArt.DPad,
                                                                     ChromaWardrobeArt.Ink));
                break;
            case Pad.Select:
                selectCap = Keycap(padArt, "SELECT", out selectLabel);
                break;
        }

        Refresh();
    }

    /// <summary>زرٌّ بصورته وكلمةٌ بعده — صفٌّ صغير يدخل في صفٍّ أكبر (تلميحات الخزانة والتصوير والأوسمة).</summary>
    public static ChromaWardrobePrompt Labeled(RectTransform parent, string label, Color color, float size,
                                               float fontSize, Pad pad, params string[] keys)
    {
        RectTransform group = ChromaWardrobeArt.NewRow(parent, label, 10f, false);
        group.sizeDelta = new Vector2(10f, size + 8f);

        var prompt = new ChromaWardrobePrompt(group, size, pad, keys);
        TextMeshProUGUI text = ChromaWardrobeArt.NewText(group, "Text", false, fontSize, color, TextAlignmentOptions.Left);
        text.text = label;
        text.rectTransform.sizeDelta = new Vector2(10f, size + 8f);
        return prompt;
    }

    private static string[] PadIcons(Pad pad)
    {
        switch (pad)
        {
            case Pad.Select: return new[] { "Share" };
            case Pad.Triggers: return new[] { "L2", "R2" };
            default: return new[] { pad.ToString() };
        }
    }

    /// <summary>صورة مفتاح الكيبورد لهذا النصّ، أو null (السهمان يُرسمان).</summary>
    private static Sprite KeyIcon(string label)
    {
        switch (label)
        {
            case "ENTER": return Load("KeyEnter");
            case "TAB": return Load("KeyTab");
            case "ESC": return Load("KeyEsc");
            case "SPACE": return Load("KeySpace");
            case "ARROWS": return Load("KeyArrows");
        }
        return label != null && label.Length == 1 && char.IsLetter(label[0]) ? Load("Key" + char.ToUpperInvariant(label[0])) : null;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> cache =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    private static Sprite Load(string name)
    {
        if (cache.TryGetValue(name, out Sprite found)) return found;
        return cache[name] = Resources.Load<Sprite>(Icons + name);
    }

    /// <summary>صورةٌ مربّعة بارتفاع الزرّ، أو null إن لم توجد.</summary>
    private RectTransform Icon(Transform parent, Sprite sprite)
    {
        if (sprite == null) return null;
        Image image = ChromaWardrobeArt.NewImage(parent, sprite.name, sprite, Color.white);
        image.preserveAspect = true;
        return ChromaWardrobeArt.Place(image, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                                       new Vector2(height, height));
    }

    /// <summary>يرسم زرّ الجهاز الذي يلعب به اللاعب الآن، ويأخذ عرضه.</summary>
    public void Refresh()
    {
        bool gamepad = InputScheme.UsingGamepad;
        keys.SetActive(!gamepad);
        padArt.gameObject.SetActive(gamepad);

        float width = !gamepad ? keysWidth
                     : padIcons > 0 ? padIcons * height + (padIcons - 1) * 6f
                     : pad == Pad.Select ? SelectWidth() : height;
        Rect.sizeDelta = new Vector2(width, height);
        layout.preferredWidth = width;
        layout.preferredHeight = height;
    }

    /// <summary>زرّ الاختيار باسمه من اليد الموصولة. النصّ يُكتب إن تغيّر الاسم فقط.</summary>
    private float SelectWidth()
    {
        Gamepad device = Gamepad.current;
        string name = device != null ? device.selectButton.displayName : null;
        if (name != selectName)
        {
            selectName = name;
            selectLabel.text = string.IsNullOrEmpty(name) ? "SELECT" : name.ToUpperInvariant();
        }
        return Fit(selectCap, selectLabel);
    }

    /// <summary>عرض الغطاء على قدر نصّه، ولا يضيق عن مربّع.</summary>
    private float Fit(RectTransform cap, TextMeshProUGUI text)
    {
        float width = Mathf.Max(height, text.GetPreferredValues(text.text).x + height * 0.6f);
        cap.sizeDelta = new Vector2(width, height);
        return width;
    }

    private RectTransform Holder(string name)
    {
        RectTransform holder = ChromaWardrobeArt.NewRect(Rect, name);
        ChromaWardrobeArt.Place(holder, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                                new Vector2(height, height));
        return holder;
    }

    /// <summary>رمزٌ ملوّن فوق قرصٍ من الحبر — كأزرار الوجه في يد بلايستيشن.</summary>
    private void Glyph(Sprite symbol, Color color, float size)
    {
        ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(padArt, "Button", ChromaWardrobeArt.Disc,
                                                             ChromaWardrobeArt.Ink));
        Image mark = ChromaWardrobeArt.NewImage(padArt, "Symbol", symbol, color);
        ChromaWardrobeArt.Place(mark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(height * size, height * size));
    }

    /// <summary>
    /// غطاء مفتاح: قاعدةٌ من الحبر ووجهٌ ورقيّ مرفوعٌ فوقها. <paramref name="text"/> =
    /// نصّه، أو null للسهمين.
    /// </summary>
    private RectTransform Keycap(Transform parent, string label, out TextMeshProUGUI text)
    {
        RectTransform cap = ChromaWardrobeArt.NewRect(parent, "Key");
        ChromaWardrobeArt.Place(cap, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                                new Vector2(height, height));

        float corners = ChromaWardrobeArt.RoundBorder / (height * 0.26f);
        Image rim = ChromaWardrobeArt.NewImage(cap, "Base", ChromaWardrobeArt.Round, ChromaWardrobeArt.Ink, true);
        rim.pixelsPerUnitMultiplier = corners;
        ChromaWardrobeArt.Stretch(rim);

        Image face = ChromaWardrobeArt.NewImage(cap, "Face", ChromaWardrobeArt.Round, ChromaWardrobeArt.Paper, true);
        face.pixelsPerUnitMultiplier = corners * 1.15f;
        RectTransform faceRect = ChromaWardrobeArt.Stretch(face);
        faceRect.offsetMin = new Vector2(3f, height * 0.13f);    // الحبر يظهر تحته: حافّة المفتاح
        faceRect.offsetMax = new Vector2(-3f, -2f);

        if (label == "<" || label == ">")
        {
            Image mark = ChromaWardrobeArt.NewImage(face.transform, "Arrow", ChromaWardrobeArt.Chevron,
                                                    ChromaWardrobeArt.Ink);
            ChromaWardrobeArt.Place(mark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                    new Vector2(height * 0.56f, height * 0.56f));
            if (label == "<") mark.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            text = null;
            return cap;
        }

        text = ChromaWardrobeArt.NewText(face.transform, "Label", false, height * 0.5f, ChromaWardrobeArt.Ink,
                                         TextAlignmentOptions.Center);
        text.text = label;
        ChromaWardrobeArt.Stretch(text);
        return cap;
    }
}

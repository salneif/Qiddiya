using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// صفحة الأوسمة داخل الخزانة: Q / Y تفتحها وتغلقها (والدائرة/Tab تغلقها). بطاقة ورقٍ فوق
/// الخزانة بشبكة ٤×٣، المفتوح ذهبيّ بحرفه والمقفل رماديّ، وسطرٌ يصف المختار. والخزانة تحتها
/// لا تأخذ الأزرار ما دامت مفتوحة (<see cref="Shown"/>).
/// </summary>
[DisallowMultipleComponent]
public class ChromaAchievementsPage : MonoBehaviour
{
    private const int SortingOrder = 3650;   // فوق الخزانة (3600)
    private const int Columns = 4, Rows = 3;
    private const float TileW = 220f, TileH = 176f, CardW = 1000f, CardH = 760f;

    private static ChromaAchievementsPage instance;
    private static int closedFrame = -10;

    /// <summary>مفتوحة الآن (أو أُغلقت هذا الإطار — فلا يُغلق الزرّ نفسه الخزانة تحتها).</summary>
    public static bool Shown => (instance != null && instance.open) || Time.frameCount <= closedFrame + 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaAchievementsPage") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaAchievementsPage>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        closedFrame = -10;
    }

    private bool open;
    private int cursor;
    private float repeatAt;
    private Vector2Int held;

    private Canvas canvas;
    private CanvasGroup group;
    private GameObject card, hint;
    private TextMeshProUGUI title, detail;
    private readonly Image[] fills = new Image[12];
    private readonly TextMeshProUGUI[] letters = new TextMeshProUGUI[12];
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[12];
    private RectTransform ring;

    private void Update()
    {
        if (!ChromaWardrobe.IsOpen)
        {
            if (open) Hide();
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        Build();
        if (!canvas.enabled) canvas.enabled = true;
        hint.SetActive(!open);

        if (!open)
        {
            if (TogglePressed()) Show();
            return;
        }

        if (TogglePressed() || BackPressed()) { Hide(); return; }
        Navigate();
    }

    private void Show()
    {
        open = true;
        card.SetActive(true);
        Refresh();
        ChromaSfx.Play("Skin_Open", 0.6f);
    }

    private void Hide()
    {
        open = false;
        closedFrame = Time.frameCount;
        if (card != null) card.SetActive(false);
        ChromaSfx.Play("Skin_Close", 0.5f);
    }

    private void Navigate()
    {
        Vector2Int dir = Direction();
        if (dir == Vector2Int.zero) { held = dir; return; }
        if (dir == held && Time.unscaledTime < repeatAt) return;
        repeatAt = Time.unscaledTime + (dir == held ? 0.13f : 0.38f);
        held = dir;

        int col = cursor % Columns, row = cursor / Columns;
        col = (col + dir.x + Columns) % Columns;
        row = (row - dir.y + Rows) % Rows;
        cursor = row * Columns + col;
        ChromaSfx.Play("Skin_Move", 0.5f);
        Refresh();
    }

    private static Vector2Int Direction()
    {
        int x = 0, y = 0;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) x--;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) x++;
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed) y++;
            if (kb.downArrowKey.isPressed || kb.sKey.isPressed) y--;
        }
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 s = pad.leftStick.ReadValue();
            if (pad.dpad.left.isPressed || s.x < -0.55f) x--;
            if (pad.dpad.right.isPressed || s.x > 0.55f) x++;
            if (pad.dpad.up.isPressed || s.y > 0.55f) y++;
            if (pad.dpad.down.isPressed || s.y < -0.55f) y--;
        }
        return new Vector2Int(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
    }

    private static bool TogglePressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
    }

    private static bool BackPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame));
    }

    private void Refresh()
    {
        Color gold = ChromaStyle.Get().gold, ink = ChromaWardrobeArt.Ink;
        title.text = "ACHIEVEMENTS   " + ChromaAchievements.Unlocked + " / " + ChromaAchievements.All.Length;
        for (int i = 0; i < ChromaAchievements.All.Length && i < 12; i++)
        {
            bool has = ChromaAchievements.Has(ChromaAchievements.All[i].id);
            fills[i].color = has ? gold : new Color(0.72f, 0.72f, 0.72f);
            letters[i].color = has ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            names[i].color = has ? ink : new Color(ink.r, ink.g, ink.b, 0.45f);
        }
        ChromaAchievements.Def d = ChromaAchievements.All[cursor];
        detail.text = d.name + "  -  " + d.detail + (ChromaAchievements.Has(d.id) ? "" : "   (LOCKED)");
        ring.anchoredPosition = Tile(cursor) + new Vector2(0f, 20f);
    }

    private static Vector2 Tile(int i) =>
        new Vector2(((i % Columns) - (Columns - 1) * 0.5f) * TileW, 150f - (i / Columns) * TileH);

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "Achievements", SortingOrder, out group);
        Color ink = ChromaWardrobeArt.Ink, paper = ChromaWardrobeArt.Paper;

        // تلميح الفتح أعلى الوسط ما دامت الخزانة مفتوحة
        var hintText = ChromaWardrobeArt.NewText(canvas.transform, "Hint", false, 30f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(hintText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(700f, 44f));
        hintText.text = "Q  /  Y      ACHIEVEMENTS";
        hint = hintText.gameObject;

        var cardRect = ChromaWardrobeArt.NewRect(canvas.transform, "Card");
        ChromaWardrobeArt.Place(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));
        ChromaWardrobeArt.Card(cardRect, 3f);
        card = cardRect.gameObject;

        title = ChromaWardrobeArt.NewText(cardRect, "Title", true, 60f, ink, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(CardW - 80f, 80f));

        ring = ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(cardRect, "Ring", ChromaWardrobeArt.Ring, ink),
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 124f));

        for (int i = 0; i < ChromaAchievements.All.Length && i < 12; i++)
        {
            Vector2 at = Tile(i);
            ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(cardRect, "Rim" + i, ChromaWardrobeArt.Disc, ink),
                                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at + new Vector2(0f, 20f), new Vector2(104f, 104f));
            fills[i] = ChromaWardrobeArt.NewImage(cardRect, "Fill" + i, ChromaWardrobeArt.Disc, Color.white);
            ChromaWardrobeArt.Place(fills[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at + new Vector2(0f, 20f), new Vector2(90f, 90f));
            letters[i] = ChromaWardrobeArt.NewText(cardRect, "Letter" + i, true, 58f, Color.white, TextAlignmentOptions.Center);
            ChromaWardrobeArt.Place(letters[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at + new Vector2(0f, 20f), new Vector2(90f, 90f));
            letters[i].text = ChromaAchievements.All[i].letter;
            names[i] = ChromaWardrobeArt.NewText(cardRect, "Name" + i, false, 22f, ink, TextAlignmentOptions.Center);
            ChromaWardrobeArt.Place(names[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at + new Vector2(0f, -52f), new Vector2(TileW - 10f, 30f));
            names[i].text = ChromaAchievements.All[i].name;
            names[i].enableAutoSizing = true;
            names[i].fontSizeMin = 14f;
            names[i].fontSizeMax = 22f;
        }

        detail = ChromaWardrobeArt.NewText(cardRect, "Detail", false, 30f, ink, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(detail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(CardW - 80f, 44f));
        detail.enableAutoSizing = true;
        detail.fontSizeMin = 18f;
        detail.fontSizeMax = 30f;

        var foot = ChromaWardrobeArt.NewText(cardRect, "Footer", false, 22f, new Color(ink.r, ink.g, ink.b, 0.6f), TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(foot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(CardW - 80f, 30f));
        foot.text = "BROWSE: ARROWS / STICK      BACK: Q / Y / CIRCLE";

        card.SetActive(false);
    }
}

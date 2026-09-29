using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// يُعلن الأزياء: <b>لافتةٌ</b> تنزل من أعلى الوسط لحظة يُفتح زيّ، و<b>شارةٌ</b> صغيرة
/// أعلى اليمين تذكّر بزرّ الخزانة.
///
/// <list type="bullet">
/// <item>اللافتة تسمع <see cref="ChromaBank.Gained"/>: كل حدٍّ تجاوزه المجموع للتوّ زيٌّ
/// جديد، وتصطفّ إن فُتح أكثر من واحد دفعةً — واحدةٌ بعد الأخرى لا فوق بعض.</item>
/// <item>الشارة ستّ ثوانٍ في أوّل كل سين لعب وبعد كل لافتة، تحت عدّاد القطرات (الذي
/// يسكن فوق ١٩٠ بكسل من الأعلى)، ثم تذوب.</item>
/// </list>
///
/// <b>لا تحجب شيئًا ولا توقف شيئًا</b>: لا تبتلع نقرة، ولا تمسّ الزمن. وتختفي — وعدّادها
/// واقف — حين لا يصحّ الكلام: قائمة أو انترو أو شاشة تحميل أو كريديت
/// (<see cref="ChromaEvents.Quiet"/>)، أو بلا لاعب، أو اللعبة موقوفة بغيرنا (لوحة
/// الإيقاف ترسم تحت هذا الكانفس)، أو الخزانة مفتوحة. فلا يفوت اللاعبَ إعلانٌ ولا
/// يظهر فوق ما لا يخصّه.
/// </summary>
[DisallowMultipleComponent]
public class ChromaSkinToast : MonoBehaviour
{
    private const int SortingOrder = 60;

    private const float BannerIn = 0.45f, BannerHold = 3.4f, BannerOut = 0.32f;
    private const float BannerShown = -26f, BannerHidden = 220f;
    private const float ConfettiAt = 0.3f;

    private const float ChipIn = 0.3f, ChipHold = 6f, ChipOut = 0.6f;
    private const float ChipRight = -36f;
    private const float ChipTop = -200f;     // تحت عدّاد القطرات: هو فوق ١٩٠ بكسل من الأعلى

    private readonly Queue<int> pending = new Queue<int>();

    private Canvas canvas;
    private RectTransform root;
    private bool built, failed;

    private RectTransform banner, medal, sparkle;
    private Image medalFill;
    private TextMeshProUGUI medalLetter, nameText, nameShadow;
    private ChromaWardrobePrompt bannerKey;
    private ChromaSkin bannerSkin;
    private float bannerAt = -1f;

    private RectTransform chip;
    private CanvasGroup chipGroup;
    private ChromaWardrobePrompt chipKey;
    private float chipAt = -1f;
    private bool chipPending = true;

    private ChromaWardrobeConfetti confetti;

    private void OnEnable()
    {
        ChromaBank.Gained += OnGained;
        InputScheme.Changed += OnSchemeChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ChromaBank.Gained -= OnGained;
        InputScheme.Changed -= OnSchemeChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>كل حدٍّ تجاوزه المجموع بهذه القطرات زيٌّ فُتح الآن.</summary>
    private void OnGained(int amount, Vector3 at)
    {
        int after = ChromaBank.Lifetime;
        int before = after - amount;
        for (int i = 1; i < ChromaSkins.Count; i++)
        {
            int need = ChromaSkins.Get(i).Threshold;
            if (before < need && need <= after) pending.Enqueue(i);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) chipPending = true;
    }

    private void OnSchemeChanged()
    {
        if (!built) return;
        bannerKey.Refresh();
        chipKey.Refresh();
    }

    private void Update()
    {
        bool mayTalk = !ChromaEvents.Quiet && ChromaSkinWearer.HasPlayer && Time.timeScale > 0f &&
                       !ChromaWardrobe.IsOpen;
        if (!mayTalk)
        {
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        if (bannerAt < 0f && pending.Count > 0 && Build()) StartBanner(pending.Dequeue());
        if (chipPending && Build())
        {
            chipPending = false;
            StartChip();
        }

        if (!built) return;

        bool visible = bannerAt >= 0f || chipAt >= 0f || confetti.Busy;
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;

        // الشاشة قد تتوقّف لحظة (تحميل، تهنيقة): لا نقفز فوق نصف الحركة
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        AnimateBanner(dt);
        AnimateChip(dt);
        confetti.Tick(dt);
    }

    // ---------- اللافتة ----------

    private void StartBanner(int skin)
    {
        bannerSkin = ChromaSkins.Get(skin);
        Color accent = bannerSkin.AccentAt(Time.unscaledTime);

        nameText.text = bannerSkin.Name;
        nameShadow.text = bannerSkin.Name;
        nameText.color = accent;
        medalFill.color = accent;
        medalLetter.text = bannerSkin.Name.Substring(0, 1);
        bannerKey.Refresh();

        banner.anchoredPosition = new Vector2(0f, BannerHidden);
        medal.localScale = Vector3.zero;
        banner.gameObject.SetActive(true);
        bannerAt = 0f;

        ChromaSfx.Play("Skin_Unlock", 0.9f);
        PadRumble.Tick();
        StartChip();
    }

    private void AnimateBanner(float dt)
    {
        if (bannerAt < 0f) return;

        float before = bannerAt;
        bannerAt += dt;
        float t = bannerAt;

        float y;
        if (t < BannerIn) y = Mathf.LerpUnclamped(BannerHidden, BannerShown, ChromaWardrobeArt.BackOut(t / BannerIn));
        else if (t < BannerIn + BannerHold) y = BannerShown + Mathf.Sin((t - BannerIn) * 2.2f) * 3f;
        else if (t < BannerIn + BannerHold + BannerOut)
        {
            float k = (t - BannerIn - BannerHold) / BannerOut;
            y = Mathf.Lerp(BannerShown, BannerHidden, k * k);
        }
        else
        {
            bannerAt = -1f;
            banner.gameObject.SetActive(false);
            return;
        }

        banner.anchoredPosition = new Vector2(0f, y);
        medal.localScale = Vector3.one * ChromaWardrobeArt.BackOut((t - 0.12f) / 0.35f);
        if (sparkle != null) sparkle.localRotation = Quaternion.Euler(0f, 0f, -t * 40f);

        if (bannerSkin.Rainbow)
        {
            Color accent = bannerSkin.AccentAt(Time.unscaledTime);
            nameText.color = accent;
            medalFill.color = accent;
        }

        if (before < ConfettiAt && t >= ConfettiAt)
            confetti.Burst(root.InverseTransformPoint(medal.position), 26, 1f);
    }

    // ---------- الشارة ----------

    private void StartChip()
    {
        chipKey.Refresh();
        chip.gameObject.SetActive(true);
        // الشارة ظاهرةٌ أصلًا: تبقى ظاهرةً ويبدأ عدّها من جديد، بلا ومضة
        chipAt = chipAt >= ChipIn && chipAt < ChipIn + ChipHold ? ChipIn : 0f;
    }

    private void AnimateChip(float dt)
    {
        if (chipAt < 0f) return;

        chipAt += dt;
        float t = chipAt;

        float alpha;
        if (t < ChipIn) alpha = t / ChipIn;
        else if (t < ChipIn + ChipHold) alpha = 1f;
        else if (t < ChipIn + ChipHold + ChipOut) alpha = 1f - (t - ChipIn - ChipHold) / ChipOut;
        else
        {
            chipAt = -1f;
            chip.gameObject.SetActive(false);
            return;
        }

        float enter = 1f - Mathf.Clamp01(t / ChipIn);
        chipGroup.alpha = alpha;
        chip.anchoredPosition = new Vector2(ChipRight + enter * enter * 70f, ChipTop);
    }

    // ---------- البناء ----------

    /// <summary>يُبنى عند أوّل حاجة لا عند التركيب: من لا يبلغ زيًّا ولا سين لعب لا يدفع ثمنه.</summary>
    private bool Build()
    {
        if (built) return true;
        if (failed) return false;

        try
        {
            canvas = ChromaWardrobeArt.NewCanvas(transform, "ChromaSkinToast", SortingOrder, out _);
            root = (RectTransform)canvas.transform;
            BuildBanner();
            BuildChip();
            confetti = new ChromaWardrobeConfetti(root, 28);
            built = true;
        }
        catch (System.Exception e)
        {
            failed = true;
            Debug.LogWarning($"[ChromaSkinToast] ما قدرت أبني اللافتة — الأزياء تعمل بلا إعلان. {e.Message}", this);
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
        }
        return built;
    }

    private void BuildBanner()
    {
        Color ink = ChromaWardrobeArt.Ink;

        banner = ChromaWardrobeArt.NewRect(root, "Banner");
        ChromaWardrobeArt.Place(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                new Vector2(0f, BannerHidden), new Vector2(780f, 176f));

        ChromaWardrobeArt.Card(banner, 0.8f);

        medal = ChromaWardrobeArt.NewRect(banner, "Medal");
        ChromaWardrobeArt.Place(medal, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(96f, 0f),
                                new Vector2(130f, 130f));

        var star = Resources.Load<Texture2D>("Chroma/Fx/Sparkle");
        if (star != null)
        {
            var burst = ChromaWardrobeArt.NewRect(medal, "Sparkle").gameObject.AddComponent<RawImage>();
            burst.texture = star;
            burst.color = new Color(1f, 0.92f, 0.6f, 0.95f);
            burst.raycastTarget = false;
            sparkle = ChromaWardrobeArt.Place(burst, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                              Vector2.zero, new Vector2(240f, 240f));
        }

        ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(medal, "Rim", ChromaWardrobeArt.Disc, ink));
        medalFill = ChromaWardrobeArt.NewImage(medal, "Fill", ChromaWardrobeArt.Disc, Color.white);
        ChromaWardrobeArt.Place(medalFill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(112f, 112f));
        medalLetter = ChromaWardrobeArt.NewText(medal, "Letter", true, 80f, Color.white, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Stretch(medalLetter);

        TextMeshProUGUI header = ChromaWardrobeArt.NewText(banner, "Header", false, 28f,
                                                           new Color(ink.r, ink.g, ink.b, 0.7f),
                                                           TextAlignmentOptions.Left);
        header.text = "NEW SKIN UNLOCKED";
        ChromaWardrobeArt.Place(header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(188f, -20f),
                                new Vector2(560f, 36f));

        nameShadow = ChromaWardrobeArt.NewText(banner, "NameShadow", true, 72f, ink, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(nameShadow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(192f, -59f),
                                new Vector2(560f, 74f));
        nameText = ChromaWardrobeArt.NewText(banner, "Name", true, 72f, Color.white, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(nameText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(188f, -54f),
                                new Vector2(560f, 74f));

        RectTransform hint = ChromaWardrobeArt.NewRow(banner, "Hint", 12f);
        ChromaWardrobeArt.Place(hint, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(188f, -148f),
                                new Vector2(10f, 36f));
        bannerKey = new ChromaWardrobePrompt(hint, 34f, ChromaWardrobePrompt.Pad.Select, "TAB");
        TextMeshProUGUI hintText = ChromaWardrobeArt.NewText(hint, "Text", false, 24f,
                                                             new Color(ink.r, ink.g, ink.b, 0.75f),
                                                             TextAlignmentOptions.Left);
        hintText.text = "TO OPEN THE WARDROBE";
        hintText.rectTransform.sizeDelta = new Vector2(10f, 36f);

        banner.gameObject.SetActive(false);
    }

    private void BuildChip()
    {
        chip = ChromaWardrobeArt.NewRow(root, "WardrobeChip", 12f);
        ChromaWardrobeArt.Place(chip, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(ChipRight, ChipTop),
                                new Vector2(10f, 56f));

        var layout = chip.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 20, 8, 8);

        Color ink = ChromaWardrobeArt.Ink;
        Image pill = chip.gameObject.AddComponent<Image>();
        pill.sprite = ChromaWardrobeArt.Round;
        pill.type = Image.Type.Sliced;
        pill.color = new Color(ink.r, ink.g, ink.b, 0.62f);
        pill.raycastTarget = false;

        chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
        chipGroup.blocksRaycasts = false;
        chipGroup.interactable = false;

        chipKey = new ChromaWardrobePrompt(chip, 40f, ChromaWardrobePrompt.Pad.Select, "TAB");
        TextMeshProUGUI label = ChromaWardrobeArt.NewText(chip, "Text", false, 30f, ChromaWardrobeArt.Paper,
                                                          TextAlignmentOptions.Left);
        label.text = "WARDROBE";
        label.rectTransform.sizeDelta = new Vector2(10f, 40f);

        chip.gameObject.SetActive(false);
    }
}

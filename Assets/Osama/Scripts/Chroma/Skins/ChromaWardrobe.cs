using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <b>خزانة الأزياء</b>: لوحةٌ على جانب، واللاعب نفسه في الجانب الآخر يلبس ما تتصفّحه حيًّا
/// — ولو كان مقفلًا، بعلامة «معاينة». الإغلاق يُرجع الملبوس، إلا إن لُبس المعروض. اللوحة
/// على اليسار، إلا إن كان اللاعب في يسار الشاشة (لقطة ثابتة، حافّة غرفة) فتنتقل لليمين.
///
/// تُفتح بـTab أو زرّ الاختيار في اليد (Share/View): لا يستعملهما شيءٌ أثناء اللعب — لا
/// خريطتا الإدخال ولا سكربت (Tab في القائمة الرئيسية وحدها). وتوقف الزمن ما دامت مفتوحة
/// وترجعه كما كان بالضبط.
///
/// <b>اللبس بـEnter/Space أو الإكس، لا بـE ولا المربّع</b>: هذان زرّا التفاعل، والتفاعلات
/// في العالم تقرؤهما من الجهاز مباشرةً (<see cref="InteractInput"/>، ورافعات سلطان في ستيم
/// بـ<c>Input</c> القديم) ولا يوقفها الزمن الموقوف — فكانت ضغطة E هنا تشدّ رافعةً أو
/// تدخل بوابةً خلف الخزانة. أما القفز (Space/الإكس) والانحناء (الدائرة) فلا تصل اللاعب
/// إلا عبر <see cref="PlayerInput"/>، وهذا يُسكَت ما دامت الخزانة مفتوحة ويعود كما كان —
/// فلا يقفز اللاعب ولا ينحني لحظة تُغلق.
///
/// <b>وتُغلق وحدها</b> — وترجع الزمن — إن حُمّل سين، أو مات اللاعب، أو قاده مشهد، أو صمتت
/// اللعبة (شاشة تحميل، كريديت)، أو بدأت لوحة تحذير (<see cref="WarningCard"/>). وإن فتح
/// اللاعب لوحة الإيقاف (ESC، لعلي) تختفي في الحال <b>ولا تمسّ الزمن</b>: اللوحة تملكه الآن
/// وترجعه هي حين تُغلق. والكشف كما في <see cref="PauseMenuFix"/>: كانفس
/// <c>PauseMenuManager</c> مفعّل. وOptions في اليد كـESC: تختفي، وتُفتح اللوحة بالضغطة نفسها.
///
/// كل الحركة بالوقت الحقيقي، والكانفس لا يبتلع نقرة.
/// </summary>
[DisallowMultipleComponent]
public class ChromaWardrobe : MonoBehaviour
{
    private const int SortingOrder = 70;
    private const string PauseMenuName = "PauseMenuManager";

    private const float PanelWidth = 640f, PanelHeight = 900f, PanelLeft = 56f;
    private const float OpenSeconds = 0.34f, CloseSeconds = 0.2f;
    private const float ShadeWidth = 0.62f;

    /// <summary>
    /// اللاعب يسار هذا من عرض الشاشة: اللوحة لليمين. على اليسار تمتدّ إلى ٣٦٪ من العرض في
    /// ١٦:٩ (٤٢٪ في ٤:٣) وتعتيمها أبعد، فتغطّيه أو تعتمه.
    /// </summary>
    private const float DockRightBelow = 0.45f;

    /// <summary>ركن عدّاد القطرات أعلى اليمين (فوق ١٩٠ من الأعلى): اللوحة على اليمين تنزل تحته.</summary>
    private const float HudCorner = 190f;

    private const float RepeatDelay = 0.38f, RepeatEvery = 0.13f;
    private const float StickOn = 0.55f, StickOff = 0.35f;

    // مواضع البطاقة من مركزها
    private const float CardY = 30f, NameY = 176f, ArrowX = 272f, TaglineY = 94f, SwatchY = 14f, StateY = -122f;
    private const float SwatchSpacing = 70f;
    private const float TabsY = 176f, TabSpacing = 84f;
    private const float BarWidth = 440f, BarHeight = 26f;
    private const float FooterWidth = 580f;

    /// <summary>كل زيٍّ نغمة من سلّمٍ خماسيّ — التصفّح لحنٌ صغير لا نقرةٌ مكرّرة.</summary>
    private static readonly float[] Notes = { 0f, 2f, 4f, 7f, 9f, 12f };

    private static readonly Color LockedGrey = new Color(0.72f, 0.71f, 0.69f);

    /// <summary>
    /// دور لوحة التحذير (<see cref="WarningCard"/>): تمسكه لحظة تبدأ، ثم تنتظر تأخيرها ولقطة
    /// تغبيشها، وبعدها فقط تحفظ الزمن وتوقفه. حقلٌ خاصّ بها يُقرأ بالانعكاس — ملفها ليس من
    /// هذه الميزة — وإن تغيّر اسمه قالها <see cref="Build"/> مرّة وبقيت الخزانة بلا هذا الحرس.
    /// </summary>
    private static readonly FieldInfo WarningCardTurn =
        typeof(WarningCard).GetField("active", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>الخزانة مفتوحة الآن (والزمن موقوفٌ لها).</summary>
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    // ---------- الحال ----------

    private float previousTimeScale = 1f;
    private readonly List<PlayerInput> muted = new List<PlayerInput>();
    private GameObject previousSelection;

    private int cursor;
    private int heldDirection;
    private float repeatAt;
    private bool stickHeld;

    private MonoBehaviour pauseMenu;
    private Canvas pauseCanvas;
    private float nextPauseScan;

    // ---------- الواجهة ----------

    private Canvas canvas;
    private RectTransform root;
    private bool built, failed;

    private Image shade;
    private RectTransform panel;
    private bool onRight;
    private Image accentBar;
    private TextMeshProUGUI drops;

    private RectTransform card;
    private CanvasGroup cardGroup;
    private RectTransform nameRoot;
    private TextMeshProUGUI nameText, nameShadow, tagline;
    private RectTransform arrowLeft, arrowRight;
    private RectTransform[] swatches;
    private Image[] swatchFills;

    private RectTransform stamp;
    private CanvasGroup stampGroup;
    private Image stampLine, stampFill;
    private TextMeshProUGUI stampText;
    private RectTransform equipRow;
    private RectTransform lockedGroup, padlock;
    private Image barFill;
    private TextMeshProUGUI progress;

    private RectTransform[] tabs;
    private Image[] tabFills;
    private GameObject[] tabLocks, tabDots;
    private RectTransform tabRing;

    private RectTransform footer;
    private ChromaWardrobePrompt equipKey, browseKey, equipHintKey, closeKey;

    private RectTransform watermark;
    private CanvasGroup watermarkGroup;
    private TextMeshProUGUI watermarkNote;
    private TextMeshProUGUI[] watermarkTexts;

    private ChromaWardrobeConfetti confetti;

    // ---------- الحركة ----------

    private float openK;
    private float cardShift, cardAlpha = 1f, nameScale = 1f, shownAt;
    private float stampAt = -10f, stampBump = 1f, shakeAt = -10f;
    private float leftNudge = 1f, rightNudge = 1f;
    private float ringX, barK, barTarget, watermarkK;
    private float[] tabScale;
    private Color accent = Color.white;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaSkins.Changed += OnSkinsChanged;
        ChromaBank.Gained += OnGained;
        InputScheme.Changed += RefreshPrompts;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaSkins.Changed -= OnSkinsChanged;
        ChromaBank.Gained -= OnGained;
        InputScheme.Changed -= RefreshPrompts;
        Close(true, true, false);
    }

    /// <summary>كائننا يُهدم مع الخروج من اللعب: رسومه المولّدة تُحرَّر معه.</summary>
    private void OnDestroy() => ChromaWardrobeArt.Release();

    /// <summary>سينٌ جديد: لا خزانة فوقه، والزمن يرجع كما كان، ولوحة إيقافه تُبحث من جديد.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOpen) Close(true, true, false);
        else Hide();

        pauseMenu = null;
        pauseCanvas = null;
        nextPauseScan = 0f;
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (!StayOpen(out bool restoreTime, out bool instant)) Close(restoreTime, instant, false);
            else if (PausePressed()) Close(true, true, false);
            else if (ClosePressed()) Close(true, false, true);
            else
            {
                Browse();
                TryEquip();
            }
        }
        else if (OpenPressed() && CanOpen())
        {
            Open();
        }

        if (built && canvas.enabled) Animate(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
    }

    // ---------- الفتح والإغلاق ----------

    /// <summary>
    /// لا تُفتح فوق غيرها: لا في صمت اللعبة، ولا بلا لاعبٍ حيّ في يده، ولا والزمن موقوفٌ
    /// بغيرها (إيقاف، لوحة تحذير)، ولا في ضغطة إنقاذ اللاعب العالق — ولا ولوحة تحذيرٍ بدأت
    /// ولم توقف الزمن بعد: لو فُتحت في مهلتها لحفظت اللوحةُ صفرنا ورجّعته بعد أن تُغلق،
    /// فتبقى اللعبة واقفة بلا شيء على الشاشة.
    /// </summary>
    private bool CanOpen() =>
        !ChromaEvents.Quiet && !LoadingOverlay.IsBusy && ChromaSkinWearer.PlayerReady &&
        Time.timeScale > 0f && !StuckRescue.SuppressPause && !PauseMenuOpen() && !WarningCardUp();

    /// <summary>
    /// هل تبقى مفتوحة؟ وإن لا: هل ترجع الزمن، وهل تختفي فورًا.
    ///
    /// لوحة الإيقاف ترسم تحت هذا الكانفس، فتختفي الخزانة في الحال لا بانزلاق فوقها.
    /// ومن غيّر الزمن ونحن مفتوحون صار يملكه: لا نكتب فوقه قيمةً قديمة. ولوحة تحذيرٍ بدأت
    /// نرجع لها الزمن في الحال — قبل أن تحفظه بعد مهلتها — ونختفي قبل لقطة تغبيشها.
    /// </summary>
    private bool StayOpen(out bool restoreTime, out bool instant)
    {
        restoreTime = false;
        instant = false;

        if (PauseMenuOpen())
        {
            instant = true;
            return false;
        }

        if (Time.timeScale != 0f) return false;

        restoreTime = true;
        if (WarningCardUp())
        {
            instant = true;
            return false;
        }

        return !ChromaEvents.Quiet && ChromaSkinWearer.PlayerReady;
    }

    private void Open()
    {
        if (!Build()) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsOpen = true;
        ChromaHud.ForceVisible = true;    // رصيدك ظاهرٌ وأنت تختار، واللعبة موقوفة
        Mute();

        cursor = ChromaSkins.Equipped;
        ChromaSkins.Preview = cursor;
        heldDirection = BrowseDirection();   // مفتاح مشيٍ ما زال مضغوطًا لا يتصفّح
        repeatAt = Time.unscaledTime + RepeatDelay;

        Dock(ChromaSkinWearer.TryViewportX(out float playerX) && playerX < DockRightBelow);
        canvas.enabled = true;
        RefreshPrompts();
        RefreshDrops();
        Show(0);
        ringX = TabX(cursor);
        // الألوان والختم ينطّون بعد أن تستقرّ اللوحة، لا وهي تنزلق
        shownAt = Time.unscaledTime + 0.12f;
        stampAt = Time.unscaledTime + 0.16f;
        ChromaSfx.Play("Skin_Open", 0.8f);
    }

    private void Close(bool restoreTime, bool instant, bool byPlayer)
    {
        if (!IsOpen) return;
        IsOpen = false;
        ChromaHud.ForceVisible = false;

        if (restoreTime && Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        Unmute();
        ChromaSkins.Preview = -1;

        if (instant) Hide();
        if (byPlayer) ChromaSfx.Play("Skin_Close", 0.75f);
    }

    private void Hide()
    {
        openK = 0f;
        if (!built || canvas == null) return;   // الخروج من اللعب يهدم الكانفس مع كائننا

        confetti.Clear();
        canvas.enabled = false;
    }

    /// <summary>
    /// اللوحة في الجهة البعيدة عن اللاعب، وتعتيمها يذوب من حافّتها نحوه، و«معاينة» في الركن
    /// السفلي من جهته. تُختار مرّة عند الفتح: الزمن موقوف والكاميرا واقفة.
    /// </summary>
    private void Dock(bool right)
    {
        if (right != onRight) openK = 0f;   // أُغلقت من جهةٍ وتُفتح من الأخرى: تنزلق من جديد لا تقفز
        onRight = right;

        float side = right ? 1f : 0f;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(side, 0.5f);

        // صورة التعتيم تذوب نحو اليمين: على اليمين تُقلب
        RectTransform dim = shade.rectTransform;
        dim.anchorMin = new Vector2(right ? 1f - ShadeWidth : 0f, 0f);
        dim.anchorMax = new Vector2(right ? 1f : ShadeWidth, 1f);
        dim.localScale = new Vector3(right ? -1f : 1f, 1f, 1f);

        watermark.anchorMin = watermark.anchorMax = watermark.pivot = new Vector2(1f - side, 0f);
        watermark.anchoredPosition = new Vector2(right ? 70f : -70f, 60f);
        TextAlignmentOptions align = right ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        foreach (TextMeshProUGUI text in watermarkTexts) text.alignment = align;
    }

    /// <summary>
    /// يُسكت إدخال اللاعب (<see cref="PlayerInput"/>) ويُفرغ تحديد الواجهة: الإكس والمسافة
    /// والدائرة أزرار الخزانة الآن، لا قفزٌ ولا انحناءٌ ولا نقرٌ على زرٍّ منسيّ خلفها.
    /// ويُحفظ ما أُسكت بعينه، فلا يُشغَّل عند الإغلاق ما كان صامتًا قبلنا.
    /// </summary>
    private void Mute()
    {
        muted.Clear();
        var players = PlayerInput.all;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerInput input = players[i];
            if (input == null || !input.inputIsActive) continue;

            input.DeactivateInput();
            muted.Add(input);
        }

        EventSystem events = EventSystem.current;
        previousSelection = events != null ? events.currentSelectedGameObject : null;
        if (previousSelection != null) events.SetSelectedGameObject(null);
    }

    /// <summary>
    /// يعيد ما أسكتناه كما كان. من أُطفئ أثناءها (موتٌ يطفئ سكربتات اللاعب) لا نشغّله:
    /// يعود وحده حين يُشغَّل. والزرّ الذي كان محدّدًا يُعاد إن لم يحدّد أحدٌ غيره.
    /// </summary>
    private void Unmute()
    {
        foreach (PlayerInput input in muted)
            if (input != null && input.isActiveAndEnabled && !input.inputIsActive) input.ActivateInput();
        muted.Clear();

        EventSystem events = EventSystem.current;
        if (events != null && previousSelection != null && previousSelection.activeInHierarchy &&
            events.currentSelectedGameObject == null)
            events.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    /// <summary>
    /// لوحة إيقاف علي مفتوحة؟ تُعرف بكانفسها كما يعرفها <see cref="PauseMenuFix"/>، وتُبحث
    /// بالاسم مرّة في السين (ومرّة في الثانية على الأكثر حيث لا لوحة).
    /// </summary>
    private bool PauseMenuOpen()
    {
        if (pauseMenu == null && Time.unscaledTime >= nextPauseScan)
        {
            nextPauseScan = Time.unscaledTime + 1f;
            pauseCanvas = null;
            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (script == null || script.GetType().Name != PauseMenuName) continue;

                pauseMenu = script;
                pauseCanvas = script.GetComponent<Canvas>();
                break;
            }
        }

        return pauseMenu != null && pauseCanvas != null && pauseCanvas.enabled && pauseMenu.isActiveAndEnabled;
    }

    /// <summary>
    /// لوحة تحذير ماسكةٌ دورها: من بدئها حتى تختفي، لا من ظهورها فقط. وكائنها المطفأ أوقف
    /// روتينها معه، فدورها عالقٌ لا لوحة — لا يمنع الخزانة بقيّة السين.
    /// </summary>
    private static bool WarningCardUp()
    {
        if (WarningCardTurn == null) return false;

        var card = WarningCardTurn.GetValue(null) as WarningCard;
        return card != null && card.gameObject.activeInHierarchy;
    }

    // ---------- الإدخال ----------

    private static bool TabPressed()
    {
        Keyboard keyboard = Keyboard.current;
        // Alt+Tab يبدّل النوافذ، لا يفتح خزانة
        return keyboard != null && keyboard.tabKey.wasPressedThisFrame && !keyboard.altKey.isPressed;
    }

    private static bool OpenPressed()
    {
        Gamepad pad = Gamepad.current;
        return TabPressed() || (pad != null && pad.selectButton.wasPressedThisFrame);
    }

    private static bool ClosePressed()
    {
        Gamepad pad = Gamepad.current;
        return TabPressed() ||
               (pad != null && (pad.selectButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame));
    }

    /// <summary>
    /// Options في اليد كـESC في الكيبورد: تختفي الخزانة وترجع الزمن، فيفتح
    /// <see cref="PauseMenuFix"/> لوحة الإيقاف بالضغطة نفسها — يقرؤها في LateUpdate، بعدنا.
    /// ومع المثلث مضغوطًا هي تركيبة الإنقاذ (<see cref="StuckRescue"/>) لا إيقاف.
    /// </summary>
    private static bool PausePressed()
    {
        Gamepad pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame && !pad.buttonNorth.isPressed;
    }

    private static bool EquipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                                 keyboard.spaceKey.wasPressedThisFrame))
            return true;

        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonSouth.wasPressedThisFrame;
    }

    /// <summary>
    /// يسار/يمين من كل مصدر: الأسهم وA/D، والأسهم في اليد وكتفاها، والعصا — بعتبتين كي لا
    /// ترتجف عند الحدّ.
    /// </summary>
    private int BrowseDirection()
    {
        int direction = 0;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction--;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction++;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.left.isPressed || pad.leftShoulder.isPressed) direction--;
            if (pad.dpad.right.isPressed || pad.rightShoulder.isPressed) direction++;

            float x = pad.leftStick.ReadValue().x;
            stickHeld = Mathf.Abs(x) > (stickHeld ? StickOff : StickOn);
            if (stickHeld) direction += x < 0f ? -1 : 1;
        }

        return Mathf.Clamp(direction, -1, 1);
    }

    /// <summary>خطوةٌ مع الضغطة، ثم تكرارٌ بعد مهلة ما دام مضغوطًا.</summary>
    private void Browse()
    {
        int direction = BrowseDirection();
        if (direction != heldDirection)
        {
            heldDirection = direction;
            if (direction == 0) return;

            Step(direction);
            repeatAt = Time.unscaledTime + RepeatDelay;
            return;
        }

        if (direction != 0 && Time.unscaledTime >= repeatAt)
        {
            Step(direction);
            repeatAt = Time.unscaledTime + RepeatEvery;
        }
    }

    private void Step(int direction)
    {
        int count = ChromaSkins.Count;
        cursor = (cursor + direction + count) % count;
        ChromaSkins.Preview = cursor;

        if (direction < 0) leftNudge = 1.4f;
        else rightNudge = 1.4f;

        Show(direction);
        ChromaSfx.Play("Skin_Move", 0.7f, ChromaSfx.Semitones(Notes[cursor % Notes.Length]));
    }

    private void TryEquip()
    {
        if (!EquipPressed()) return;

        if (!ChromaSkins.IsUnlocked(cursor))
        {
            shakeAt = Time.unscaledTime;
            ChromaSfx.Play("Skin_Locked", 0.85f);
            PadRumble.Play(0.3f, 0f, 0.12f);
            return;
        }

        if (cursor == ChromaSkins.Equipped)
        {
            stampBump = 1.2f;   // ملبوسٌ أصلًا: هزّة الختم تقول «نعم، هذا هو»
            return;
        }

        ChromaSkins.Equip(cursor);
        stampAt = Time.unscaledTime;
        confetti.Burst(root.InverseTransformPoint(stamp.position), 30, 1.1f);
        ChromaSfx.Play("Skin_Equip", 0.9f);
        PadRumble.Pickup();
        ChromaSkinWearer.Celebrate();
    }

    // ---------- المحتوى ----------

    private void OnSkinsChanged()
    {
        if (!IsOpen || !built) return;
        RefreshState(false);
        RefreshTabs();
    }

    /// <summary>قطراتٌ وصلت والخزانة مفتوحة (مفتاح التجربة): العدّاد والأقفال تتحدّث حيّة.</summary>
    private void OnGained(int amount, Vector3 at)
    {
        if (!IsOpen || !built) return;
        RefreshDrops();
        RefreshState(false);
        RefreshTabs();
    }

    private void RefreshPrompts()
    {
        if (!built) return;
        equipKey.Refresh();
        browseKey.Refresh();
        equipHintKey.Refresh();
        closeKey.Refresh();
    }

    private void RefreshDrops() => drops.SetText("{0:0}", ChromaBank.Lifetime);

    /// <summary>يعرض الزيّ تحت المؤشّر. <paramref name="direction"/> = من أين يدخل (٠ = بلا حركة).</summary>
    private void Show(int direction)
    {
        ChromaSkin skin = ChromaSkins.Get(cursor);
        nameText.text = skin.Name;
        nameShadow.text = skin.Name;
        tagline.text = skin.Tagline;

        int shown = Mathf.Min(skin.Swatches.Length, swatches.Length);
        for (int i = 0; i < swatches.Length; i++)
        {
            bool on = i < shown;
            swatches[i].gameObject.SetActive(on);
            if (!on) continue;

            swatchFills[i].color = skin.Swatches[i];
            swatches[i].anchoredPosition = new Vector2((i - (shown - 1) * 0.5f) * SwatchSpacing, SwatchY);
        }

        RefreshState(true);
        RefreshTabs();

        shownAt = Time.unscaledTime;
        if (direction == 0) return;

        cardShift = direction * 90f;
        cardAlpha = 0.1f;
        nameScale = 1.16f;
        stampBump = 1.15f;
    }

    /// <summary>ملبوس، أو «البس»، أو مقفلٌ بما بقي له. <paramref name="fresh"/> = زيٌّ جديد يملأ شريطه من الصفر.</summary>
    private void RefreshState(bool fresh)
    {
        ChromaSkin skin = ChromaSkins.Get(cursor);
        bool unlocked = ChromaSkins.IsUnlocked(cursor);
        bool equipped = unlocked && cursor == ChromaSkins.Equipped;

        stamp.gameObject.SetActive(equipped);
        equipRow.gameObject.SetActive(unlocked && !equipped);
        lockedGroup.gameObject.SetActive(!unlocked);
        watermarkNote.text = unlocked ? "NOT EQUIPPED" : "LOCKED";

        if (unlocked) return;

        int have = ChromaBank.Lifetime;
        progress.SetText("{0:0} / {1:0} DROPS", Mathf.Min(have, skin.Threshold), skin.Threshold);
        barTarget = Mathf.Clamp01((float)have / Mathf.Max(1, skin.Threshold));
        if (fresh) barK = 0f;
    }

    private void RefreshTabs()
    {
        int equipped = ChromaSkins.Equipped;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool unlocked = ChromaSkins.IsUnlocked(i);
            tabFills[i].color = unlocked ? ChromaSkins.Get(i).AccentAt(Time.unscaledTime) : LockedGrey;
            tabLocks[i].SetActive(!unlocked);
            tabDots[i].SetActive(i == equipped);
        }
    }

    private static float TabX(int index) => (index - (ChromaSkins.Count - 1) * 0.5f) * TabSpacing;

    // ---------- الحركة ----------

    private void Animate(float dt)
    {
        float now = Time.unscaledTime;

        // تنزلق من جهتها بنطّة، وتخرج أسرع ممّا دخلت
        openK = Mathf.MoveTowards(openK, IsOpen ? 1f : 0f, dt / (IsOpen ? OpenSeconds : CloseSeconds));
        if (!IsOpen && openK <= 0f)
        {
            Hide();
            return;
        }

        float slide = IsOpen ? ChromaWardrobeArt.BackOut(openK) : openK * (2f - openK);
        // ٢١:٩ أقصر من اللوحة فتصغر لتسعها. وقبل أوّل تخطيطٍ للكانفس ارتفاعه صفر: لا نقلبها.
        // وعلى اليمين تنزل تحت ركن عدّاد القطرات فلا تغطّيه
        float reserve = onRight ? HudCorner : 0f;
        float tall = root.rect.height;
        float fit = tall > 200f ? Mathf.Clamp((tall - 40f - reserve) / PanelHeight, 0.5f, 1f) : 1f;
        panel.localScale = new Vector3(fit, fit, 1f);
        float x = Mathf.LerpUnclamped(-(PanelWidth * fit + 60f), PanelLeft, slide);
        panel.anchoredPosition = new Vector2(onRight ? -x : x, -reserve * 0.5f);

        Color ink = ChromaWardrobeArt.Ink;
        shade.color = new Color(ink.r, ink.g, ink.b, 0.6f * Mathf.Clamp01(openK));

        ChromaSkin skin = ChromaSkins.Get(cursor);
        accent = ChromaWardrobeArt.Damp(accent, skin.AccentAt(now), 12f, dt);
        nameText.color = accent;
        accentBar.color = accent;
        barFill.color = accent;
        stampLine.color = accent;
        stampText.color = accent;
        stampFill.color = new Color(accent.r, accent.g, accent.b, 0.14f);

        // البطاقة: تدخل من جهة التصفّح، والاسم ينطّ، والألوان تتتابع
        cardShift = ChromaWardrobeArt.Damp(cardShift, 0f, 16f, dt);
        cardAlpha = ChromaWardrobeArt.Damp(cardAlpha, 1f, 14f, dt);
        float shake = 0f, since = now - shakeAt;
        if (since < 0.4f) shake = Mathf.Sin(since * 55f) * 16f * (1f - since / 0.4f);
        card.anchoredPosition = new Vector2(cardShift + shake, CardY);
        cardGroup.alpha = cardAlpha;

        nameScale = ChromaWardrobeArt.Damp(nameScale, 1f, 14f, dt);
        nameRoot.localScale = new Vector3(nameScale, nameScale, 1f);

        for (int i = 0; i < swatches.Length; i++)
        {
            if (!swatches[i].gameObject.activeSelf) continue;
            float pop = ChromaWardrobeArt.BackOut((now - shownAt - i * 0.045f) / 0.24f);
            swatches[i].localScale = new Vector3(pop, pop, 1f);
        }

        leftNudge = ChromaWardrobeArt.Damp(leftNudge, 1f, 12f, dt);
        rightNudge = ChromaWardrobeArt.Damp(rightNudge, 1f, 12f, dt);
        float bob = Mathf.Sin(now * 4f) * 5f;
        arrowLeft.anchoredPosition = new Vector2(-ArrowX - bob, NameY);
        arrowLeft.localScale = new Vector3(-leftNudge, leftNudge, 1f);
        arrowRight.anchoredPosition = new Vector2(ArrowX + bob, NameY);
        arrowRight.localScale = new Vector3(rightNudge, rightNudge, 1f);

        if (stamp.gameObject.activeSelf)
        {
            float k = Mathf.Clamp01((now - stampAt) / 0.3f);
            stampBump = ChromaWardrobeArt.Damp(stampBump, 1f, 12f, dt);
            float size = Mathf.LerpUnclamped(2.1f, 1f, ChromaWardrobeArt.BackOut(k)) * stampBump;
            stamp.localScale = new Vector3(size, size, 1f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-18f, -6f, k));
            stampGroup.alpha = Mathf.Clamp01(k * 3f);
        }

        if (lockedGroup.gameObject.activeSelf)
        {
            padlock.localRotation = Quaternion.Euler(0f, 0f,
                since < 0.4f ? Mathf.Sin(since * 40f) * 18f * (1f - since / 0.4f) : 0f);
            barK = ChromaWardrobeArt.Damp(barK, barTarget, 7f, dt);
            float width = BarWidth * barK;
            barFill.enabled = width > 2f;
            barFill.rectTransform.sizeDelta = new Vector2(width, BarHeight);
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            tabScale[i] = ChromaWardrobeArt.Damp(tabScale[i], i == cursor ? 1.22f : 1f, 16f, dt);
            tabs[i].localScale = new Vector3(tabScale[i], tabScale[i], 1f);
            ChromaSkin tab = ChromaSkins.Get(i);
            if (tab.Rainbow && ChromaSkins.IsUnlocked(i)) tabFills[i].color = tab.AccentAt(now);
        }
        ringX = ChromaWardrobeArt.Damp(ringX, TabX(cursor), 20f, dt);
        tabRing.anchoredPosition = new Vector2(ringX, 0f);

        // سطر الأزرار يصغر إن طال بخطٍّ عريض — لا يخرج من اللوحة أبدًا
        float wide = footer.rect.width;
        float squeeze = wide > FooterWidth ? FooterWidth / wide : 1f;
        footer.localScale = new Vector3(squeeze, squeeze, 1f);

        bool preview = cursor != ChromaSkins.Equipped;
        watermarkK = ChromaWardrobeArt.Damp(watermarkK, preview ? 1f : 0f, 10f, dt);
        watermarkGroup.alpha = watermarkK * Mathf.Clamp01(openK) * (0.8f + 0.2f * Mathf.Sin(now * 2.6f));

        confetti.Tick(dt);
    }

    // ---------- البناء ----------

    /// <summary>تُبنى عند أوّل فتح، مرّة في الجلسة، وتعيش مع هذا الكائن بين السينات.</summary>
    private bool Build()
    {
        if (built) return true;
        if (failed) return false;

        try
        {
            canvas = ChromaWardrobeArt.NewCanvas(transform, "ChromaWardrobe", SortingOrder, out _);
            root = (RectTransform)canvas.transform;
            Color ink = ChromaWardrobeArt.Ink;

            shade = ChromaWardrobeArt.NewImage(root, "Shade", ChromaWardrobeArt.Fade, ink);
            ChromaWardrobeArt.Stretch(shade).anchorMax = new Vector2(ShadeWidth, 1f);

            BuildWatermark(ink);

            panel = ChromaWardrobeArt.NewRect(root, "Panel");
            ChromaWardrobeArt.Place(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-800f, 0f),
                                    new Vector2(PanelWidth, PanelHeight));
            ChromaWardrobeArt.Card(panel, 0.7f);

            BuildHeader(ink);
            BuildCard(ink);
            BuildTabs(ink);
            BuildFooter(ink);

            confetti = new ChromaWardrobeConfetti(root, 36);
            built = true;

            if (WarningCardTurn == null)
                Debug.LogWarning("[ChromaWardrobe] ما لقيت WarningCard.active — لا أرى لوحة التحذير قبل أن توقف الزمن.", this);
        }
        catch (System.Exception e)
        {
            failed = true;
            Debug.LogWarning($"[ChromaWardrobe] ما قدرت أبني الخزانة — تبقى مغلقة. {e.Message}", this);
            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
        }
        return built;
    }

    private void BuildHeader(Color ink)
    {
        TextMeshProUGUI title = ChromaWardrobeArt.NewText(panel, "Title", true, 56f, ink, TextAlignmentOptions.Left);
        title.text = "WARDROBE";
        ChromaWardrobeArt.Place(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -26f),
                                new Vector2(380f, 72f));

        RectTransform counter = ChromaWardrobeArt.NewRow(panel, "Drops", 8f);
        ChromaWardrobeArt.Place(counter, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-36f, -62f),
                                new Vector2(10f, 48f));

        var fill = Resources.Load<Sprite>("Chroma/Fx/DropFill");
        var outline = Resources.Load<Sprite>("Chroma/Fx/DropInk");
        if (fill != null)
        {
            RectTransform icon = ChromaWardrobeArt.NewRect(counter, "Drop");
            icon.sizeDelta = new Vector2(44f, 44f);
            var size = icon.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 44f;
            size.preferredHeight = 44f;
            ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(icon, "Fill", fill, ChromaStyle.Get().gold));
            if (outline != null) ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(icon, "Ink", outline, Color.white));
        }

        drops = ChromaWardrobeArt.NewText(counter, "Count", false, 44f, ink, TextAlignmentOptions.Right);
        drops.rectTransform.sizeDelta = new Vector2(10f, 48f);

        accentBar = ChromaWardrobeArt.NewImage(panel, "Accent", ChromaWardrobeArt.Round, ink, true);
        accentBar.pixelsPerUnitMultiplier = 5f;
        ChromaWardrobeArt.Place(accentBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -116f),
                                new Vector2(PanelWidth - 80f, 10f));
    }

    private void BuildCard(Color ink)
    {
        Vector2 middle = new Vector2(0.5f, 0.5f);

        card = ChromaWardrobeArt.NewRect(panel, "Card");
        ChromaWardrobeArt.Place(card, middle, middle, new Vector2(0f, CardY), new Vector2(580f, 540f));
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        nameRoot = ChromaWardrobeArt.NewRect(card, "Name");
        ChromaWardrobeArt.Place(nameRoot, middle, middle, new Vector2(0f, NameY), new Vector2(470f, 120f));
        nameShadow = ChromaWardrobeArt.NewText(nameRoot, "Shadow", true, 100f, ink, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Stretch(nameShadow, new Vector2(5f, -6f));
        nameText = ChromaWardrobeArt.NewText(nameRoot, "Text", true, 100f, Color.white, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Stretch(nameText);
        FitText(nameShadow, 60f, 100f);
        FitText(nameText, 60f, 100f);

        arrowLeft = Arrow(ink);
        arrowRight = Arrow(ink);

        tagline = ChromaWardrobeArt.NewText(card, "Tagline", false, 34f, new Color(ink.r, ink.g, ink.b, 0.8f),
                                            TextAlignmentOptions.Center);
        tagline.textWrappingMode = TextWrappingModes.Normal;
        FitText(tagline, 24f, 34f);
        ChromaWardrobeArt.Place(tagline, middle, middle, new Vector2(0f, TaglineY), new Vector2(520f, 80f));

        int most = 0;
        for (int i = 0; i < ChromaSkins.Count; i++) most = Mathf.Max(most, ChromaSkins.Get(i).Swatches.Length);
        swatches = new RectTransform[most];
        swatchFills = new Image[most];
        for (int i = 0; i < most; i++)
        {
            RectTransform swatch = ChromaWardrobeArt.NewRect(card, "Swatch");
            ChromaWardrobeArt.Place(swatch, middle, middle, new Vector2(0f, SwatchY), new Vector2(60f, 60f));
            ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(swatch, "Rim", ChromaWardrobeArt.Disc, ink));
            swatchFills[i] = ChromaWardrobeArt.NewImage(swatch, "Fill", ChromaWardrobeArt.Disc, Color.white);
            ChromaWardrobeArt.Place(swatchFills[i], middle, middle, Vector2.zero, new Vector2(48f, 48f));
            swatches[i] = swatch;
        }

        BuildStamp(middle);
        BuildEquip(ink, middle);
        BuildLocked(ink, middle);
    }

    private RectTransform Arrow(Color ink)
    {
        Image arrow = ChromaWardrobeArt.NewImage(card, "Arrow", ChromaWardrobeArt.Chevron,
                                                 new Color(ink.r, ink.g, ink.b, 0.85f));
        return ChromaWardrobeArt.Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(0f, NameY), new Vector2(56f, 56f));
    }

    private static void FitText(TextMeshProUGUI text, float min, float max)
    {
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
    }

    /// <summary>ختمٌ مائل بلون الزيّ: «هذا ملبوس».</summary>
    private void BuildStamp(Vector2 middle)
    {
        stamp = ChromaWardrobeArt.NewRect(card, "Equipped");
        ChromaWardrobeArt.Place(stamp, middle, middle, new Vector2(0f, StateY), new Vector2(380f, 104f));
        stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
        stampGroup.blocksRaycasts = false;
        stampGroup.interactable = false;

        stampFill = ChromaWardrobeArt.NewImage(stamp, "Fill", ChromaWardrobeArt.Round, Color.white, true);
        stampFill.pixelsPerUnitMultiplier = 0.9f;
        ChromaWardrobeArt.Stretch(stampFill);
        stampLine = ChromaWardrobeArt.NewImage(stamp, "Line", ChromaWardrobeArt.RoundLine, Color.white, true);
        stampLine.pixelsPerUnitMultiplier = 0.6f;
        ChromaWardrobeArt.Stretch(stampLine);
        stampText = ChromaWardrobeArt.NewText(stamp, "Text", true, 62f, Color.white, TextAlignmentOptions.Center);
        stampText.text = "EQUIPPED";
        ChromaWardrobeArt.Stretch(stampText);
    }

    private void BuildEquip(Color ink, Vector2 middle)
    {
        equipRow = ChromaWardrobeArt.NewRow(card, "Equip", 16f);
        ChromaWardrobeArt.Place(equipRow, middle, middle, new Vector2(0f, StateY), new Vector2(10f, 60f));
        equipKey = new ChromaWardrobePrompt(equipRow, 56f, ChromaWardrobePrompt.Pad.Cross, "ENTER");
        TextMeshProUGUI label = ChromaWardrobeArt.NewText(equipRow, "Text", false, 46f, ink, TextAlignmentOptions.Left);
        label.text = "EQUIP";
        label.rectTransform.sizeDelta = new Vector2(10f, 60f);
    }

    private void BuildLocked(Color ink, Vector2 middle)
    {
        lockedGroup = ChromaWardrobeArt.NewRect(card, "Locked");
        ChromaWardrobeArt.Place(lockedGroup, middle, middle, new Vector2(0f, StateY), new Vector2(520f, 190f));

        RectTransform head = ChromaWardrobeArt.NewRow(lockedGroup, "Head", 12f);
        ChromaWardrobeArt.Place(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(10f, 64f));
        Image lockIcon = ChromaWardrobeArt.NewImage(head, "Padlock", ChromaWardrobeArt.Padlock, ink);
        padlock = lockIcon.rectTransform;
        padlock.sizeDelta = new Vector2(54f, 54f);
        var lockSize = lockIcon.gameObject.AddComponent<LayoutElement>();
        lockSize.preferredWidth = 54f;
        lockSize.preferredHeight = 54f;
        TextMeshProUGUI word = ChromaWardrobeArt.NewText(head, "Text", true, 56f, ink, TextAlignmentOptions.Left);
        word.text = "LOCKED";
        word.rectTransform.sizeDelta = new Vector2(10f, 64f);

        Image track = ChromaWardrobeArt.NewImage(lockedGroup, "Track", ChromaWardrobeArt.Round,
                                                 new Color(ink.r, ink.g, ink.b, 0.14f), true);
        track.pixelsPerUnitMultiplier = ChromaWardrobeArt.RoundBorder / (BarHeight * 0.5f);
        ChromaWardrobeArt.Place(track, middle, middle, new Vector2(0f, -8f), new Vector2(BarWidth, BarHeight));

        barFill = ChromaWardrobeArt.NewImage(track.transform, "Fill", ChromaWardrobeArt.Round, Color.white, true);
        barFill.pixelsPerUnitMultiplier = track.pixelsPerUnitMultiplier;
        ChromaWardrobeArt.Place(barFill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                                new Vector2(0f, BarHeight));

        progress = ChromaWardrobeArt.NewText(lockedGroup, "Progress", false, 32f,
                                             new Color(ink.r, ink.g, ink.b, 0.8f), TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(progress, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                                new Vector2(520f, 44f));
    }

    /// <summary>ستّ دوائر بألوان الأزياء، والمقفل رماديٌّ بقفل، ونقطةٌ تحت الملبوس، وحلقةٌ تتبع المؤشّر.</summary>
    private void BuildTabs(Color ink)
    {
        Vector2 middle = new Vector2(0.5f, 0.5f);
        RectTransform row = ChromaWardrobeArt.NewRect(panel, "Tabs");
        ChromaWardrobeArt.Place(row, new Vector2(0.5f, 0f), middle, new Vector2(0f, TabsY), new Vector2(560f, 110f));

        Image ring = ChromaWardrobeArt.NewImage(row, "Ring", ChromaWardrobeArt.Ring, ink);
        tabRing = ChromaWardrobeArt.Place(ring, middle, middle, Vector2.zero, new Vector2(96f, 96f));

        int count = ChromaSkins.Count;
        tabs = new RectTransform[count];
        tabFills = new Image[count];
        tabLocks = new GameObject[count];
        tabDots = new GameObject[count];
        tabScale = new float[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform tab = ChromaWardrobeArt.NewRect(row, "Tab");
            ChromaWardrobeArt.Place(tab, middle, middle, new Vector2(TabX(i), 0f), new Vector2(72f, 72f));
            ChromaWardrobeArt.Stretch(ChromaWardrobeArt.NewImage(tab, "Rim", ChromaWardrobeArt.Disc, ink));

            tabFills[i] = ChromaWardrobeArt.NewImage(tab, "Fill", ChromaWardrobeArt.Disc, Color.white);
            ChromaWardrobeArt.Place(tabFills[i], middle, middle, Vector2.zero, new Vector2(60f, 60f));

            Image lockIcon = ChromaWardrobeArt.NewImage(tab, "Lock", ChromaWardrobeArt.Padlock,
                                                        new Color(ink.r, ink.g, ink.b, 0.75f));
            ChromaWardrobeArt.Place(lockIcon, middle, middle, Vector2.zero, new Vector2(36f, 36f));
            tabLocks[i] = lockIcon.gameObject;

            Image dot = ChromaWardrobeArt.NewImage(tab, "Worn", ChromaWardrobeArt.Disc, ink);
            ChromaWardrobeArt.Place(dot, new Vector2(0.5f, 0f), middle, new Vector2(0f, -24f), new Vector2(14f, 14f));
            tabDots[i] = dot.gameObject;

            tabs[i] = tab;
            tabScale[i] = 1f;
        }
    }

    /// <summary>أزرار الخزانة بجهاز اللاعب: تصفّح، لبس، إغلاق.</summary>
    private void BuildFooter(Color ink)
    {
        footer = ChromaWardrobeArt.NewRow(panel, "Footer", 26f);
        ChromaWardrobeArt.Place(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 62f),
                                new Vector2(10f, 44f));
        Color soft = new Color(ink.r, ink.g, ink.b, 0.75f);

        browseKey = Hint(footer, "BROWSE", soft, ChromaWardrobePrompt.Pad.DPad, "<", ">");
        equipHintKey = Hint(footer, "EQUIP", soft, ChromaWardrobePrompt.Pad.Cross, "ENTER");
        closeKey = Hint(footer, "CLOSE", soft, ChromaWardrobePrompt.Pad.Circle, "TAB");
    }

    /// <summary>زرٌّ ثم نصّه، صفًّا صغيرًا داخل صفّ الأزرار.</summary>
    private static ChromaWardrobePrompt Hint(RectTransform parent, string label, Color color,
                                             ChromaWardrobePrompt.Pad pad, params string[] keys)
    {
        RectTransform group = ChromaWardrobeArt.NewRow(parent, label, 8f, false);
        group.sizeDelta = new Vector2(10f, 44f);

        var prompt = new ChromaWardrobePrompt(group, 36f, pad, keys);
        TextMeshProUGUI text = ChromaWardrobeArt.NewText(group, "Text", false, 26f, color, TextAlignmentOptions.Left);
        text.text = label;
        text.rectTransform.sizeDelta = new Vector2(10f, 44f);
        return prompt;
    }

    /// <summary>«معاينة» في الركن السفلي من جهة اللاعب (<see cref="Dock"/>)، فوقه لا فوق اللوحة، حين يُعرض غير الملبوس.</summary>
    private void BuildWatermark(Color ink)
    {
        watermark = ChromaWardrobeArt.NewRect(root, "Preview");
        ChromaWardrobeArt.Place(watermark, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 60f),
                                new Vector2(720f, 210f));
        watermark.localRotation = Quaternion.Euler(0f, 0f, 4f);
        watermarkGroup = watermark.gameObject.AddComponent<CanvasGroup>();
        watermarkGroup.blocksRaycasts = false;
        watermarkGroup.interactable = false;
        watermarkGroup.alpha = 0f;

        TextMeshProUGUI shadow = ChromaWardrobeArt.NewText(watermark, "Shadow", true, 132f,
                                                           new Color(ink.r, ink.g, ink.b, 0.45f),
                                                           TextAlignmentOptions.Right);
        shadow.text = "PREVIEW";
        ChromaWardrobeArt.Place(shadow, Vector2.one, Vector2.one, new Vector2(5f, -6f), new Vector2(720f, 140f));

        TextMeshProUGUI word = ChromaWardrobeArt.NewText(watermark, "Word", true, 132f, Color.white,
                                                         TextAlignmentOptions.Right);
        word.text = "PREVIEW";
        ChromaWardrobeArt.Place(word, Vector2.one, Vector2.one, Vector2.zero, new Vector2(720f, 140f));

        // بعرض الكلمة إلا هامشين: يلتصق بطرفها أيًّا كانت المحاذاة
        watermarkNote = ChromaWardrobeArt.NewText(watermark, "Note", false, 36f, Color.white, TextAlignmentOptions.Right);
        ChromaWardrobeArt.Place(watermarkNote, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f),
                                new Vector2(704f, 50f));

        watermarkTexts = new[] { shadow, word, watermarkNote };
    }
}

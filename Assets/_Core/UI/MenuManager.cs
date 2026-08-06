using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// مدير القوائم: يحفظ ترتيب اللوحات المفتوحة (Stack) فيرجع ESC / زر B خطوة واحدة للخلف،
/// ويحدّد الزر الأول تلقائيًا حتى يعمل الكنترولر، ويعيد التحديد لو ضاع بعد نقرة ماوس.
/// ضعه على كائن فارغ اسمه "MenuManager" في كل مشهد فيه واجهة.
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("اللوحات")]
    [Tooltip("لوحة البداية (القائمة الرئيسية). اتركها فارغة في مشاهد اللعب.")]
    [SerializeField] private UIPanel rootPanel;

    [Tooltip("لوحات تُطفأ إجباريًا عند بدء المشهد (Settings / Credits / Pause)")]
    [SerializeField] private UIPanel[] closeOnStart;

    [Header("المؤشر")]
    [Tooltip("يُظهر مؤشر الماوس عند فتح لوحة ويخفيه عند إغلاق الكل (فعّله في مشاهد اللعب فقط)")]
    [SerializeField] private bool manageCursor = false;

    [Header("أحداث")]
    [Tooltip("يُستدعى عند ضغط ESC / B / Start ولا توجد لوحة مفتوحة — اربطه بفتح لوحة الإيقاف")]
    public UnityEvent onCancelAtRoot;

    private readonly List<UIPanel> stack = new List<UIPanel>();
    private float cachedTimeScale = 1f;
    private bool lastInputWasPointer;

    /// <summary>اللوحة المفتوحة حاليًا في الأعلى، أو null إذا لا شيء مفتوح.</summary>
    public UIPanel Current => stack.Count > 0 ? stack[stack.Count - 1] : null;

    public bool IsAnyPanelOpen => stack.Count > 0;

    /// <summary>
    /// هل آخر إدخال كان بالماوس؟ يستخدمه UISelectOnHover ليقرر متى يُلغي التحديد.
    /// حين يقود الماوس لا نفرض تحديدًا على أي زر — وإلا بقي زر بارز بلا سبب.
    /// </summary>
    public static bool PointerIsDriving => Instance == null || Instance.lastInputWasPointer;

    /// <summary>
    /// حقيقي أثناء تحديد يفرضه فتح/إغلاق لوحة. يقرأه UISelectOnHover فلا يشغّل صوت
    /// التحويم — وإلا سمعت نغمة زر عند بدء المشهد وعند كل فتح لوحة بلا سبب.
    /// </summary>
    public static bool SelectionIsSilent { get; private set; }

    private void Awake()
    {
        Instance = this;
        cachedTimeScale = 1f;

        // بلا كنترولر موصول نفترض الماوس، فلا يُنوَّر زر تلقائيًا عند بدء المشهد
#if ENABLE_INPUT_SYSTEM
        lastInputWasPointer = Gamepad.current == null;
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // قبل أي إخفاء: نلتقط الحجم الطبيعي للأزرار وهي لم تُلمس بعد.
        // لا نستطيع الاعتماد على Awake الخاص باللوحة لأن اللوحة المطفأة في السين لا يعمل Awake لها.
        if (rootPanel != null) rootPanel.CaptureBaseline();
        if (closeOnStart != null)
        {
            foreach (var panel in closeOnStart)
                if (panel != null) panel.CaptureBaseline();
        }

        if (closeOnStart != null)
        {
            foreach (var panel in closeOnStart)
                if (panel != null) panel.SetVisible(false);
        }

        // المشهد قد يكون جاء من لوحة إيقاف أوقفت الزمن
        Time.timeScale = 1f;

        if (rootPanel != null)
            Open(rootPanel);
        else
            ApplyCursor();
    }

    private void Update()
    {
        TrackActiveDevice();

        if (WasBackPressed())
            HandleBack();
        else if (WasStartPressed() && !IsAnyPanelOpen)
            onCancelAtRoot?.Invoke();

        KeepSelectionAlive();
        DropStaleMouseSelection();
    }

    // ---------- التنقل ----------

    /// <summary>يفتح لوحة ويضعها فوق اللوحة الحالية.</summary>
    public void Open(UIPanel panel)
    {
        if (panel == null || Current == panel) return;

        // اللوحة موجودة أصلًا تحت في الترتيب (نقرة مزدوجة، أو ربط الزر مرتين):
        // نرجع إليها بدل تكرارها — التكرار كان يجعل زر الرجوع يحتاج ضغطتين.
        int existing = stack.IndexOf(panel);
        if (existing >= 0)
        {
            for (int i = stack.Count - 1; i > existing; i--)
            {
                stack[i].SetVisible(false);
                stack[i].RaiseClosed();
                stack.RemoveAt(i);
            }

            panel.SetVisible(true);
            panel.ResetVisualStates();
            Select(panel.LastSelected != null && panel.LastSelected.activeInHierarchy
                ? panel.LastSelected
                : panel.FirstSelected);

            ApplyTimeScale();
            ApplyCursor();
            return;
        }

        UIPanel previous = Current;
        if (previous != null)
        {
            previous.LastSelected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (panel.HidePrevious)
                previous.SetVisible(false);
        }

        stack.Add(panel);
        panel.SetVisible(true);
        panel.ResetVisualStates();
        panel.RaiseOpened();

        // أول زر في اللوحة، أو الزر الذي كنا عليه آخر مرة
        Select(panel.LastSelected != null && panel.LastSelected.activeInHierarchy
            ? panel.LastSelected
            : panel.FirstSelected);

        ApplyTimeScale();
        ApplyCursor();
    }

    /// <summary>يرجع خطوة واحدة للخلف. اربطه بأزرار Back في الـ Inspector.</summary>
    public void Back()
    {
        if (stack.Count == 0) return;

        UIPanel top = Current;
        if (top.BlockBack || top == rootPanel) return;

        stack.RemoveAt(stack.Count - 1);
        top.SetVisible(false);
        top.RaiseClosed();

        UIPanel previous = Current;
        if (previous != null)
        {
            previous.SetVisible(true);
            previous.ResetVisualStates();
            Select(previous.LastSelected != null && previous.LastSelected.activeInHierarchy
                ? previous.LastSelected
                : previous.FirstSelected);
        }

        ApplyTimeScale();
        ApplyCursor();
    }

    /// <summary>يغلق كل اللوحات ويعود للعب (يُستخدم لزر Resume).</summary>
    public void CloseAll()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            if (stack[i] == rootPanel) break;

            stack[i].SetVisible(false);
            stack[i].RaiseClosed();
            stack.RemoveAt(i);
        }

        UIPanel previous = Current;
        if (previous != null)
        {
            previous.SetVisible(true);
            previous.ResetVisualStates();
            Select(previous.FirstSelected);
        }
        else
        {
            ForceSelect(null);
        }

        ApplyTimeScale();
        ApplyCursor();
    }

    /// <summary>
    /// تُنادى من UIPanel حين تُطفأ اللوحة بـ`SetActive` من خارج هذا المدير.
    /// نُسقطها هي وكل ما فوقها من الترتيب حتى يبقى الترتيب مطابقًا لما يراه اللاعب.
    /// </summary>
    public void NotifyPanelClosedExternally(UIPanel panel)
    {
        int index = stack.IndexOf(panel);
        if (index < 0) return;

        stack.RemoveRange(index, stack.Count - index);
        ApplyTimeScale();
        ApplyCursor();
    }

    private void HandleBack()
    {
        UIPanel top = Current;
        if (top == null || top == rootPanel || top.BlockBack)
            onCancelAtRoot?.Invoke();
        else
            Back();
    }

    // ---------- تحديد الأزرار (الكنترولر) ----------

    /// <summary>
    /// يحدد الزر للكنترولر/الكيبورد فقط. إن كان اللاعب يستخدم الماوس نترك التحديد
    /// فارغًا — لأن فرضه كان يترك زرًا بارزًا (منتفخًا) والمؤشر بعيد عنه تمامًا،
    /// وهذا بالضبط ما يظهر عند الرجوع من الإعدادات.
    /// </summary>
    private void Select(GameObject target)
    {
        if (lastInputWasPointer)
        {
            ForceSelect(null);
            return;
        }

        ForceSelect(target);
    }

    private static void ForceSelect(GameObject target)
    {
        if (EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject == target) return; // وإلا أعدنا الصوت بلا داعٍ

        SelectionIsSilent = true;
        EventSystem.current.SetSelectedGameObject(null);
        if (target != null)
            EventSystem.current.SetSelectedGameObject(target);
        SelectionIsSilent = false;
    }

    /// <summary>
    /// نقرة الماوس على فراغ تمسح التحديد، فيتوقف الكنترولر عن العمل.
    /// هنا نُعيد التحديد لأول زر بمجرد أن يلمس اللاعب الكنترولر مرة أخرى.
    /// </summary>
    private void KeepSelectionAlive()
    {
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy) return;

        UIPanel top = Current;
        if (top == null || !WasNavigatePressed()) return;

        ForceSelect(top.LastSelected != null && top.LastSelected.activeInHierarchy
            ? top.LastSelected
            : top.FirstSelected);
    }

    /// <summary>يتابع الجهاز المستخدم فعلًا هذه اللحظة: ماوس أم كنترولر/كيبورد.</summary>
    private void TrackActiveDevice()
    {
        if (WasNonPointerInput())
            lastInputWasPointer = false;
        else if (WasPointerInput())
            lastInputWasPointer = true;
    }

    /// <summary>
    /// في وضع الماوس لا يبقى محدَّدًا إلا ما تحت المؤشر فعلًا.
    /// بدون هذا يظل زر منوّرًا بلا سبب: الزر الأول يُحدَّد تلقائيًا عند بدء المشهد،
    /// والزر المنقور يبقى محدَّدًا بعد أن يبتعد المؤشر عنه.
    /// </summary>
    private void DropStaleMouseSelection()
    {
        if (!lastInputWasPointer || EventSystem.current == null) return;
        if (IsPointerHeld()) return;   // سحب سلايدر: المؤشر يخرج والعنصر ما زال قيد الاستخدام

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected != UISelectOnHover.Hovered)
            ForceSelect(null);
    }

    // ---------- الحالة ----------

    private void ApplyTimeScale()
    {
        bool shouldPause = false;
        foreach (var panel in stack)
        {
            if (panel.PauseGameWhileOpen) { shouldPause = true; break; }
        }

        if (shouldPause)
        {
            if (Time.timeScale != 0f) cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        }
    }

    private void ApplyCursor()
    {
        if (!manageCursor) return;

        bool showCursor = IsAnyPanelOpen;
        Cursor.visible = showCursor;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // ---------- الإدخال ----------

    /// <summary>ESC على الكيبورد، أو B / Circle على الكنترولر.</summary>
    private static bool WasBackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
    }

    /// <summary>زر Start / Options على الكنترولر.</summary>
    private static bool WasStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
    }

    /// <summary>أي حركة تنقّل (عصا/أسهم) — تُستخدم لإعادة التحديد المفقود فقط.</summary>
    private static bool WasNavigatePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            if (gamepad.dpad.ReadValue().sqrMagnitude > 0.25f) return true;
            if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f) return true;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            return keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame;
        }
        return false;
#else
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }

    /// <summary>أي إدخال من الكنترولر أو الكيبورد — يعني أن اللاعب ترك الماوس.</summary>
    private static bool WasNonPointerInput()
    {
        if (WasNavigatePressed()) return true;

#if ENABLE_INPUT_SYSTEM
        var gamepad = Gamepad.current;
        if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame
            || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.startButton.wasPressedThisFrame)) return true;

        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame
            || keyboard.escapeKey.wasPressedThisFrame
            || keyboard.tabKey.wasPressedThisFrame)) return true;

        return false;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
    }

    /// <summary>زر الماوس مضغوط الآن (سحب جارٍ).</summary>
    private static bool IsPointerHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    /// <summary>حركة الماوس أو ضغط أزراره.</summary>
    private static bool WasPointerInput()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return false;
        if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) return true;
        return mouse.delta.ReadValue().sqrMagnitude > 1f;
#else
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) return true;
        return Mathf.Abs(Input.GetAxisRaw("Mouse X")) > 0.01f || Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.01f;
#endif
    }
}

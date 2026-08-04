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

    /// <summary>اللوحة المفتوحة حاليًا في الأعلى، أو null إذا لا شيء مفتوح.</summary>
    public UIPanel Current => stack.Count > 0 ? stack[stack.Count - 1] : null;

    public bool IsAnyPanelOpen => stack.Count > 0;

    private void Awake()
    {
        Instance = this;
        cachedTimeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
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
        if (WasBackPressed())
            HandleBack();
        else if (WasStartPressed() && !IsAnyPanelOpen)
            onCancelAtRoot?.Invoke();

        KeepSelectionAlive();
    }

    // ---------- التنقل ----------

    /// <summary>يفتح لوحة ويضعها فوق اللوحة الحالية.</summary>
    public void Open(UIPanel panel)
    {
        if (panel == null || Current == panel) return;

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
            Select(previous.FirstSelected);
        }
        else
        {
            Select(null);
        }

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

    private void Select(GameObject target)
    {
        if (EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        if (target != null)
            EventSystem.current.SetSelectedGameObject(target);
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

        Select(top.LastSelected != null && top.LastSelected.activeInHierarchy
            ? top.LastSelected
            : top.FirstSelected);
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
}

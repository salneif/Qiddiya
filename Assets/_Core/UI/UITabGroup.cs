using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// تبويبات الإعدادات (General / Controls) مع دعم أزرار L1 و R1 على الكنترولر —
/// نفس الأيقونات الموجودة في تصميم لوحة الإعدادات.
/// ضعه على كائن الإعدادات الأب، وعبّئ التبويبات بالترتيب.
/// </summary>
public class UITabGroup : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        [Tooltip("محتوى التبويب الذي يظهر عند اختياره")]
        public GameObject content;

        [Tooltip("زر/عنوان التبويب (اختياري) — يُستخدم لتمييز التبويب النشط")]
        public GameObject header;

        [Tooltip("أول عنصر يُحدَّد داخل التبويب عند فتحه — لازم للكنترولر")]
        public GameObject firstSelected;
    }

    [SerializeField] private Tab[] tabs;
    [SerializeField] private int startIndex = 0;

    [Header("تمييز التبويب النشط")]
    [SerializeField] private Color activeHeaderColor = Color.white;
    [SerializeField] private Color inactiveHeaderColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("الأصوات")]
    [SerializeField] private AudioClip switchClip;

    private int currentIndex = -1;

    public int CurrentIndex => currentIndex;

    private void OnEnable()
    {
        if (tabs == null || tabs.Length == 0) return;

        SelectTab(Mathf.Clamp(startIndex, 0, tabs.Length - 1), false);
    }

    private void Update()
    {
        if (WasPressed(shoulderRight: false)) Previous();
        else if (WasPressed(shoulderRight: true)) Next();
    }

    /// <summary>اربطه بزر L1 في الواجهة.</summary>
    public void Previous() => Cycle(-1);

    /// <summary>اربطه بزر R1 في الواجهة.</summary>
    public void Next() => Cycle(1);

    private void Cycle(int direction)
    {
        if (tabs == null || tabs.Length <= 1) return;

        int next = (currentIndex + direction + tabs.Length) % tabs.Length;
        SelectTab(next, true);
    }

    /// <summary>اربطه بأزرار التبويبات مباشرة (مرّر رقم التبويب من الـ Inspector).</summary>
    public void SelectTab(int index) => SelectTab(index, true);

    private void SelectTab(int index, bool playSound)
    {
        if (tabs == null || tabs.Length == 0) return;

        index = Mathf.Clamp(index, 0, tabs.Length - 1);
        if (index == currentIndex) return;

        currentIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == index;

            if (tabs[i].content != null)
                tabs[i].content.SetActive(active);

            TintHeader(tabs[i].header, active);
        }

        GameObject target = tabs[index].firstSelected;
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);

        if (playSound && switchClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(switchClip);
    }

    private void TintHeader(GameObject header, bool active)
    {
        if (header == null) return;

        // Graphic يغطي Image و TextMeshProUGUI معًا
        var graphic = header.GetComponent<UnityEngine.UI.Graphic>();
        if (graphic != null)
            graphic.color = active ? activeHeaderColor : inactiveHeaderColor;
    }

    private static bool WasPressed(bool shoulderRight)
    {
#if ENABLE_INPUT_SYSTEM
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            var button = shoulderRight ? gamepad.rightShoulder : gamepad.leftShoulder;
            if (button.wasPressedThisFrame) return true;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null)
            return shoulderRight ? keyboard.eKey.wasPressedThisFrame : keyboard.qKey.wasPressedThisFrame;

        return false;
#else
        return shoulderRight
            ? Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.E)
            : Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.Q);
#endif
    }
}

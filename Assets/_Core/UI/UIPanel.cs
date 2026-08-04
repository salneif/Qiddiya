using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// لوحة واجهة واحدة (Main-Menu / Settings_UI / Credits-UI / Pause_Holder).
/// تُركّب على الكائن الجذر للّوحة. لا تفتحها بنفسك — استخدم MenuManager.Open/Back
/// حتى يبقى ترتيب الرجوع وتحديد أزرار الكنترولر صحيحًا.
/// </summary>
public class UIPanel : MonoBehaviour
{
    [Header("الكنترولر")]
    [Tooltip("الزر الذي يُحدَّد تلقائيًا عند فتح اللوحة — ضروري ليعمل الكنترولر")]
    [SerializeField] private GameObject firstSelected;

    [Header("السلوك")]
    [Tooltip("يخفي اللوحة السابقة عند فتح هذه (أطفئه للّوحات التي تُعرض فوق غيرها)")]
    [SerializeField] private bool hidePrevious = true;

    [Tooltip("يوقف الزمن أثناء فتح اللوحة (للـ Pause فقط)")]
    [SerializeField] private bool pauseGameWhileOpen = false;

    [Tooltip("يمنع الرجوع بـ ESC / زر B من هذه اللوحة (للوحة الجذر مثل القائمة الرئيسية)")]
    [SerializeField] private bool blockBack = false;

    [Header("أحداث")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    /// <summary>آخر زر كان محددًا قبل مغادرة هذه اللوحة — يُستعاد عند الرجوع إليها.</summary>
    public GameObject LastSelected { get; set; }

    public GameObject FirstSelected => firstSelected;
    public bool HidePrevious => hidePrevious;
    public bool PauseGameWhileOpen => pauseGameWhileOpen;
    public bool BlockBack => blockBack;

    /// <summary>تُستدعى من MenuManager فقط.</summary>
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void RaiseOpened() => onOpened?.Invoke();
    public void RaiseClosed() => onClosed?.Invoke();

    /// <summary>اربطه بزر Back في الـ Inspector، أو استخدم MenuManager.Instance.Back().</summary>
    public void CloseSelf()
    {
        if (MenuManager.Instance != null)
            MenuManager.Instance.Back();
        else
            SetVisible(false);
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// بوابة تنفتح/تنغلق بالانزلاق بنعومة — تُفتح عبر Open() (اربطها بحدث الرافعة).
/// حُطّها على كائن البوابة نفسه.
/// </summary>
public class Gate : MonoBehaviour
{
    [Header("الحركة")]
    [Tooltip("إزاحة البوابة عند الفتح (مثلاً لأعلى Y = 4)")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
    [Tooltip("سرعة الفتح/الإغلاق (متر/ثانية)")]
    [SerializeField] private float speed = 3f;
    [Tooltip("تبدأ مفتوحة؟")]
    [SerializeField] private bool startOpen = false;

    [Header("أحداث")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Vector3 closedPos, openPos;
    private bool isOpen;
    private bool wasOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        closedPos = transform.position;
        openPos = closedPos + openOffset;
        isOpen = wasOpen = startOpen;
        transform.position = startOpen ? openPos : closedPos;
    }

    private void Update()
    {
        Vector3 target = isOpen ? openPos : closedPos;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // إطلاق حدث الوصول مرة واحدة
        if (isOpen != wasOpen && transform.position == target)
        {
            wasOpen = isOpen;
            if (isOpen) onOpened?.Invoke(); else onClosed?.Invoke();
        }
    }

    public void Open()  => isOpen = true;
    public void Close() => isOpen = false;
    public void Toggle() => isOpen = !isOpen;
}

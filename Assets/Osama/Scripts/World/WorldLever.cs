using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// رافعة (Lever) تفاعلية — يقترب منها اللاعب ويضغط زر التفاعل فتُسحب وتطلق حدثًا
/// (فتح بوابة مثلاً). يدور مقبضها بصريًا عند التشغيل.
///
/// نظام قابل لإعادة الاستخدام: اربط onActivated بأي شيء (بوابة، جسر، ضوء...).
///
/// لجعل الرافعة تختفي عند حمل العلم: ضع عليها وسم "LightObject" فيتكفّل
/// WorldFlagDirector بإخفائها/إظهارها مع حالة العلم (لا حاجة لكود إضافي).
/// تعمل بالمسافة (لا تحتاج كولايدر تريغر) فلا تتعارض مع كولايدر مجسم الرافعة.
///
/// (الاسم WorldLever وليس Lever لتفادي التعارض مع Sultan/.../Lever.cs)
/// </summary>
public class WorldLever : MonoBehaviour
{
    [Header("التفاعل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("أقصى مسافة يقدر اللاعب يشغّل منها الرافعة (متر)")]
    [SerializeField] private float interactRange = 2.5f;
    [Tooltip("زر التشغيل")]
    [SerializeField] private Key interactKey = Key.E;
    [Tooltip("تبديل (يشتغل/يطفى) أو مرة واحدة تبقى مسحوبة")]
    [SerializeField] private bool toggle = false;

    [Header("مقبض الرافعة (بصري)")]
    [Tooltip("محوّل المقبض الذي يدور — اتركه فارغًا إن لم ترد حركة")]
    [SerializeField] private Transform handle;
    [Tooltip("دوران المقبض عند السحب (Euler محلي)")]
    [SerializeField] private Vector3 pulledLocalEuler = new Vector3(-45f, 0f, 0f);
    [SerializeField] private float handleSpeed = 8f;

    [Header("أحداث")]
    [Tooltip("عند سحب الرافعة (افتح البوابة هنا)")]
    public UnityEvent onActivated;
    [Tooltip("عند إرجاعها (وضع التبديل فقط)")]
    public UnityEvent onDeactivated;

    private Transform player;
    private bool isOn;
    private Quaternion restRot, pulledRot;

    /// <summary>هل الرافعة مسحوبة الآن؟</summary>
    public bool IsOn => isOn;

    private void Awake()
    {
        if (handle != null)
        {
            restRot = handle.localRotation;
            pulledRot = restRot * Quaternion.Euler(pulledLocalEuler);
        }
    }

    private void Update()
    {
        // حركة المقبض الناعمة
        if (handle != null)
            handle.localRotation = Quaternion.Slerp(handle.localRotation,
                isOn ? pulledRot : restRot, Time.deltaTime * handleSpeed);

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        bool pressed = interactKey != Key.None && Keyboard.current != null &&
                       Keyboard.current[interactKey].wasPressedThisFrame;

        if (pressed && Vector3.Distance(player.position, transform.position) <= interactRange)
            Activate();
    }

    /// <summary>يشغّل الرافعة (قابل للربط بحدث أيضًا).</summary>
    public void Activate()
    {
        if (toggle) isOn = !isOn;
        else { if (isOn) return; isOn = true; }

        if (isOn) onActivated?.Invoke();
        else onDeactivated?.Invoke();
    }

    /// <summary>يعيد الرافعة لوضعها الأصلي بلا إطلاق حدث.</summary>
    public void ResetLever() => isOn = false;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

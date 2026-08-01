using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// تريغر مشروط بحالة العلم: لا ينطلق إلا إذا كان اللاعب <b>حاملًا العلم</b>
/// (أو تاركًا له، حسب <see cref="requireHeld"/>).
///
/// مصمَّم لمشاهد الكمين: يمرّ اللاعب من هنا وهو ذاهب للعلم فلا يحدث شيء،
/// ويمرّ راجعًا وهو حامله فينفجر الباب ويخرج الوحش.
///
/// يفحص في OnTriggerStay لا OnTriggerEnter — فلو كان اللاعب واقفًا داخل المنطقة
/// لحظة التقاطه العلم، ينطلق فورًا بدل أن ينتظر خروجه ودخوله من جديد.
///
/// التركيب: كائن فيه Collider (Is Trigger) يغطي عرض الممر + هذا السكربت.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlagTrigger : MonoBehaviour
{
    [Header("الشرط")]
    [Tooltip("العلم المطلوب")]
    [SerializeField] private FlagItem flag;
    [Tooltip("مفعّل: ينطلق فقط والعلم <b>محمول</b>. مطفي: ينطلق فقط والعلم متروك.")]
    [SerializeField] private bool requireHeld = true;

    [Header("الشروط العامة")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("ينطلق مرة واحدة ثم يتعطّل")]
    [SerializeField] private bool triggerOnce = true;

    [Header("أحداث")]
    [Tooltip("عند تحقّق الشرط — اربطه بـ BurstDoor.Burst")]
    public UnityEvent onTriggered;

    /// <summary>هل انطلق من قبل؟</summary>
    public bool HasFired { get; private set; }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (HasFired && triggerOnce) return;
        if (flag == null || !other.CompareTag(playerTag)) return;

        // الشرط غير متحقق → لا ينطلق ولا يُستهلك، فيبقى مسلّحًا لمروره القادم
        if (flag.IsHeld != requireHeld) return;

        HasFired = true;
        onTriggered?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

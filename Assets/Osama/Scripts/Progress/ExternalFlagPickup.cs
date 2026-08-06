using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// يسجّل علمًا **من نظام آخر** في <see cref="GameProgress"/> — لسينات لا تستخدم
/// <see cref="FlagItem"/>.
///
/// حالته النموذجية: مرحلة التوايلايت، فيها علم علي (<c>A_Flag</c>) يلصق نفسه
/// باللاعب ويشغّل المرحلة الأخيرة للبوس. لا نريد استبداله — نريد فقط أن يعرف
/// نظام التقدّم أن اللاعب صار حاملًا علم هذي المرحلة، ليظهر على رأسه في الهب
/// ويسمح له <see cref="LevelPortal"/> بالخروج.
///
/// <b>التركيب:</b> حُطّه على <b>نفس كائن العلم الخارجي</b> — يتشارك معه نفس
/// الكولايدر، فيلتقط نفس اللمسة بلا أي تعديل على سكربت صاحبه.
///
/// لا يعتمد على أي صنف خارجي عمدًا: المستودع مشترك، وأي مرجع لصنف زميل
/// يكسر البناء للجميع لو أعاد تسميته.
///
/// <b>وهذا السين لا يحتاج</b> `InteractorManager` ولا `WorldBWController` ولا
/// `WorldInteractor` — العالم يبقى ملوّنًا كما هو.
/// </summary>
public class ExternalFlagPickup : MonoBehaviour
{
    [Header("الهوية")]
    [Tooltip("أي علم من الثلاثة يمثّله هذا الكائن")]
    [SerializeField] private FlagId flagId = FlagId.Twilight;

    [Header("الشروط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("أحداث")]
    [Tooltip("لحظة تسجيل العلم — صوت، مؤثر، فتح باب...")]
    public UnityEvent onRegistered;

    /// <summary>هل سُجّل العلم؟</summary>
    public bool HasRegistered { get; private set; }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (HasRegistered || !other.CompareTag(playerTag)) return;
        Register();
    }

    /// <summary>يسجّل العلم يدويًا — اربطه بأي حدث إن لم يكن اللمس مناسبًا.</summary>
    public void Register()
    {
        if (HasRegistered) return;
        HasRegistered = true;

        GameProgress.Instance.CarryFlag(flagId);
        onRegistered?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

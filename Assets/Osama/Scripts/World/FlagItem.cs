using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// العلم القابل للحمل (بأسلوب CTF): يلمسه اللاعب فيلتصق فيه ويُحمل فوقه،
/// ولا يمكن تركه إلا في مقبس مخصص (<see cref="FlagSocket"/>) — لا رمي حر.
///
/// عند الالتقاط/الوضع يطلق أحداثًا يستخدمها <see cref="WorldFlagDirector"/>
/// لقلب العالم أبيض/أسود وتغيير الموسيقى وفتح البوابات... إلخ.
///
/// حُط على العلم: Collider (Is Trigger) يغطيه + هذا السكربت.
/// وإذا أردت دائرة ملوّنة تمشي مع العلم: أضف عليه WorldInteractor أيضًا.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlagItem : MonoBehaviour
{
    [Header("الالتقاط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("إزاحة العلم عن مركز اللاعب أثناء الحمل (فوق الرأس مثل CTF)")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 1.8f, -0.15f);

    [Header("أحداث")]
    [Tooltip("عند التقاط العلم (افتح بوابة/شغّل مؤثر...)")]
    public UnityEvent onPickedUp;
    [Tooltip("عند وضع العلم في مقبسه")]
    public UnityEvent onPlaced;

    /// <summary>أحداث برمجية يستخدمها WorldFlagDirector.</summary>
    public event Action PickedUp;
    public event Action Placed;

    /// <summary>هل العلم محمول الآن؟</summary>
    public bool IsHeld { get; private set; }

    private Collider pickupCollider;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsHeld || !other.CompareTag(playerTag)) return;
        PickUp(other.transform);
    }

    private void PickUp(Transform player)
    {
        IsHeld = true;
        pickupCollider.enabled = false; // لا يُلتقط مرتين ولا يعيق الحركة

        transform.SetParent(player);
        transform.localPosition = holdOffset;
        transform.localRotation = Quaternion.identity;

        onPickedUp?.Invoke();
        PickedUp?.Invoke();
    }

    /// <summary>
    /// يضع العلم في نقطة المقبس — ينادى حصرًا من FlagSocket
    /// (لا توجد طريقة أخرى لترك العلم).
    /// </summary>
    public void PlaceAt(Transform point)
    {
        if (!IsHeld) return;

        IsHeld = false;
        transform.SetParent(null);
        transform.SetPositionAndRotation(point.position, point.rotation);
        pickupCollider.enabled = true; // يمكن التقاطه من جديد

        onPlaced?.Invoke();
        Placed?.Invoke();
    }
}

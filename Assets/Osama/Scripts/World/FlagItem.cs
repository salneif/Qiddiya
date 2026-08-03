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

    [Header("العودة عند إعادة الضبط")]
    [Tooltip("نقطة عودة العلم عند الموت — عادة مقبسه. " +
             "اتركها فارغة ليعود لموضعه الذي بدأت عليه اللعبة.")]
    [SerializeField] private Transform returnPoint;
    [Tooltip("يعود تلقائيًا لحظة موت حامله، بلا حاجة لربطه بـ On Respawn. " +
             "بدونه يبقى العلم في يد اللاعب بعد البعث فيظل العالم في حالة 'العلم محمول' " +
             "— الفئران مختفية والأبواب مفتوحة.")]
    [SerializeField] private bool returnOnHolderDeath = true;

    [Header("الصوت")]
    [Tooltip("مصدر الصوت — يُلتقط تلقائيًا من نفس كائن العلم إذا تُرك فارغًا")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت التقاط العلم")]
    [SerializeField] private AudioClip pickupSound;
    [Tooltip("صوت غرس العلم في مقبسه")]
    [SerializeField] private AudioClip placeSound;

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
    private Vector3 startPosition;
    private Quaternion startRotation;
    private PlayerKillable holderKillable;   // حامل العلم الحالي، لمراقبة موته

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    /// <summary>
    /// يعيد العلم لنقطة عودته ويفلته من يد اللاعب — اربطه بـ PlayerKillable.onRespawn.
    /// لا يطلق أحداث الوضع إلا إن كان محمولًا فعلًا، حتى لا يُعاد ضبط العالم
    /// في كل موتة والعلم أصلًا في مكانه.
    /// </summary>
    public void ReturnHome()
    {
        bool wasHeld = IsHeld;
        IsHeld = false;

        transform.SetParent(null);
        transform.SetPositionAndRotation(
            returnPoint != null ? returnPoint.position : startPosition,
            returnPoint != null ? returnPoint.rotation : startRotation);

        pickupCollider.enabled = true;

        if (!wasHeld) return;
        Play(placeSound);
        onPlaced?.Invoke();
        Placed?.Invoke();
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsHeld || !other.CompareTag(playerTag)) return;
        PickUp(other.transform);
    }

    /// <summary>
    /// يعيد العلم لحظة موت حامله. بلا هذا يبقى العلم في يده بعد البعث،
    /// فيظل العالم كله في حالة "العلم محمول" رغم أن اللاعب عاد لنقطة البداية.
    /// </summary>
    private void Update()
    {
        if (!IsHeld || !returnOnHolderDeath) return;
        if (holderKillable != null && holderKillable.IsDead) ReturnHome();
    }

    private void PickUp(Transform player)
    {
        IsHeld = true;
        holderKillable = player.GetComponentInParent<PlayerKillable>();
        pickupCollider.enabled = false; // لا يُلتقط مرتين ولا يعيق الحركة

        transform.SetParent(player);
        transform.localPosition = holdOffset;
        transform.localRotation = Quaternion.identity;

        Play(pickupSound);
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

        Play(placeSound);
        onPlaced?.Invoke();
        Placed?.Invoke();
    }
}

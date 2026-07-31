using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// باب ينفجر بالفيزياء ويطير من مكانه، ويكشف ما خلفه (المهرج/الغرفة).
///
/// لماذا الفيزياء بدل الأنميشن: الانفجار يطلع مختلفًا كل مرة بدل تكرار ممل،
/// ويصطدم بالجدران وبالأرض واقعيًا، ولا يحتاج أي مقطع أنميشن — فتصنعه بالكامل
/// من الإعدادات في دقيقة.
///
/// التركيب:
///  - على مجسم الباب: Collider صلب + Rigidbody (يُضبط Kinematic تلقائيًا).
///  - كائن المهرج/الغرفة يبدأ <b>مطفأً</b> ويوضع في <see cref="revealOnBurst"/>.
///  - اربط <see cref="Burst"/> بحدث تريغر على طريق الرجوع
///    (WorldChangeTrigger مع Lamp Switch = None يعمل كتريغر عام).
/// </summary>
public class BurstDoor : MonoBehaviour
{
    [Header("الانفجار")]
    [Tooltip("جسم الباب — يُلتقط تلقائيًا من نفس الكائن")]
    [SerializeField] private Rigidbody doorBody;
    [Tooltip("اتجاه الاندفاع (محلي) — عادة للأمام نحو اللاعب")]
    [SerializeField] private Vector3 burstDirection = Vector3.forward;
    [Tooltip("قوة الاندفاع")]
    [SerializeField] private float burstForce = 14f;
    [Tooltip("قوة اللف العشوائي — تجعل الباب يتقلّب في الهواء")]
    [SerializeField] private float burstTorque = 10f;
    [Tooltip("ارتفاع الاندفاع لأعلى — يمنعه من الانزلاق على الأرض فقط")]
    [SerializeField] private float upwardLift = 0.35f;

    [Header("ما يظهر بعد الانفجار")]
    [Tooltip("المهرج والغرفة — كائنات تبدأ مطفأة وتُشغَّل عند الانفجار")]
    [SerializeField] private GameObject[] revealOnBurst;
    [Tooltip("تأخير ظهورها بعد طيران الباب (ثواني) — لحظة الترقّب قبل أن يخرج")]
    [SerializeField] private float revealDelay = 0.35f;

    [Header("لمسات")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت تحطّم الباب")]
    [SerializeField] private AudioClip burstSound;
    [SerializeField] private CameraShake cameraShake;
    [Tooltip("مؤثر غبار/شظايا يظهر لحظة الانفجار (اختياري)")]
    [SerializeField] private GameObject burstVfx;

    [Header("التنظيف")]
    [Tooltip("حذف الباب بعد هذه المدة (ثواني) حتى لا يعيق طريق الرجوع. 0 = يبقى")]
    [SerializeField] private float destroyAfter = 8f;

    [Header("أحداث")]
    [Tooltip("لحظة الانفجار")]
    public UnityEvent onBurst;

    /// <summary>هل انفجر الباب؟</summary>
    public bool HasBurst { get; private set; }

    private void Awake()
    {
        if (doorBody == null) doorBody = GetComponent<Rigidbody>();

        // يبقى ثابتًا كجدار حتى لحظة الانفجار
        if (doorBody != null) doorBody.isKinematic = true;

        SetRevealed(false);
        if (burstVfx != null) burstVfx.SetActive(false);
    }

    /// <summary>يفجّر الباب — اربطه بحدث التريغر.</summary>
    public void Burst()
    {
        if (HasBurst) return;
        HasBurst = true;

        if (doorBody != null)
        {
            doorBody.isKinematic = false;

            Vector3 dir = transform.TransformDirection(burstDirection.normalized);
            dir = (dir + Vector3.up * upwardLift).normalized;

            doorBody.AddForce(dir * burstForce, ForceMode.VelocityChange);
            doorBody.AddTorque(Random.insideUnitSphere * burstTorque, ForceMode.VelocityChange);
        }

        if (burstSound != null && audioSource != null) audioSource.PlayOneShot(burstSound);
        if (cameraShake != null) cameraShake.Shake();
        if (burstVfx != null) burstVfx.SetActive(true);

        onBurst?.Invoke();

        StartCoroutine(RevealRoutine());
        if (destroyAfter > 0f) Destroy(gameObject, destroyAfter);
    }

    private IEnumerator RevealRoutine()
    {
        // لحظة صمت قصيرة بعد التحطّم قبل أن يخرج — الترقّب أهم من الخروج نفسه
        if (revealDelay > 0f) yield return new WaitForSeconds(revealDelay);
        SetRevealed(true);
    }

    private void SetRevealed(bool on)
    {
        if (revealOnBurst == null) return;
        foreach (var go in revealOnBurst)
            if (go != null) go.SetActive(on);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Vector3 dir = transform.TransformDirection(burstDirection.normalized);
        Gizmos.DrawRay(transform.position, dir * 3f);
    }
}

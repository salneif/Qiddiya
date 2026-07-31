using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// بندقية تُطلق من فوهتها: ومضة نار + صوت + ارتداد للخلف، مع شعاع اختياري
/// يقتل من يقف في مساره.
///
/// تعمل تلقائيًا بفاصل زمني (خطر إيقاعي مثل الفاس)، أو يدويًا عبر <see cref="Fire"/>
/// إذا ربطتها برافعة أو دوّاسة.
///
/// <b>حيلة الومضة:</b> مؤثرات النار الجاهزة مصمَّمة لنار مشتعلة مستمرة، فتبدو غلطًا
/// كومضة فوهة. لذلك يصغّرها <see cref="flashScale"/> ويحذفها بعد
/// <see cref="flashDuration"/> (جزء من عُشر ثانية) — فتتحول أي نار عادية إلى
/// ومضة بارود مقنعة بلا صنع مؤثر جديد.
/// </summary>
public class MuzzleGun : MonoBehaviour
{
    [Header("الفوهة")]
    [Tooltip("نقطة خروج الطلقة — كائن فارغ عند طرف الماسورة، محوره Z للأمام")]
    [SerializeField] private Transform muzzlePoint;

    [Header("ومضة النار")]
    [Tooltip("مؤثر النار — يُنسخ عند الفوهة ويُحذف فورًا")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [Tooltip("تصغير المؤثر ليناسب فوهة بندقية بدل نار كبيرة")]
    [SerializeField] private float flashScale = 0.25f;
    [Tooltip("مدة بقاء الومضة (ثواني) — قصيرة جدًا، الومضة الحقيقية أقل من عُشر ثانية")]
    [SerializeField] private float flashDuration = 0.08f;

    [Header("الإطلاق")]
    [Tooltip("يطلق تلقائيًا بفاصل زمني. أطفئه إذا كانت رافعة أو دوّاسة تشغّله")]
    [SerializeField] private bool autoFire = true;
    [Tooltip("الفاصل بين طلقتين (ثواني)")]
    [SerializeField] private float fireInterval = 2.5f;
    [Tooltip("تأخير البداية — نوّعه بين البنادق حتى لا تطلق كلها في نفس اللحظة")]
    [SerializeField] private float startDelay = 0f;

    [Header("الشعاع (اختياري)")]
    [Tooltip("يطلق شعاعًا يقتل اللاعب إن كان في مساره. أطفئه لتكون البندقية ديكورًا")]
    [SerializeField] private bool damaging = false;
    [Tooltip("مدى الشعاع (متر)")]
    [SerializeField] private float range = 25f;
    [Tooltip("الطبقات التي يصطدم بها الشعاع — ضع فيها اللاعب والجدران")]
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("الارتداد")]
    [Tooltip("مجسم البندقية الذي يرتد — اتركه فارغًا بلا ارتداد")]
    [SerializeField] private Transform gunVisual;
    [Tooltip("مسافة الارتداد للخلف (متر)")]
    [SerializeField] private float recoilDistance = 0.08f;
    [Tooltip("زمن الرجوع لوضعه")]
    [SerializeField] private float recoilRecover = 0.18f;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shotSound;
    [Tooltip("تفاوت طبقة الصوت — يمنع الطلقات المتكررة من أن تبدو آلية")]
    [Range(0f, 0.4f)]
    [SerializeField] private float pitchVariation = 0.1f;

    [Header("أحداث")]
    [Tooltip("عند كل طلقة")]
    public UnityEvent onFired;

    private float nextFireTime;
    private Vector3 gunRest;
    private Coroutine recoilRoutine;

    private Transform Muzzle => muzzlePoint != null ? muzzlePoint : transform;

    private void Awake()
    {
        if (gunVisual != null) gunRest = gunVisual.localPosition;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        nextFireTime = Time.time + startDelay;
    }

    private void Update()
    {
        if (!autoFire) return;
        if (Time.time < nextFireTime) return;

        nextFireTime = Time.time + Mathf.Max(0.05f, fireInterval);
        Fire();
    }

    /// <summary>يطلق طلقة — اربطه برافعة أو دوّاسة إذا أطفأت الإطلاق التلقائي.</summary>
    public void Fire()
    {
        SpawnFlash();
        PlayShot();

        if (gunVisual != null)
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(RecoilRoutine());
        }

        if (damaging) FireRay();

        onFired?.Invoke();
    }

    private void SpawnFlash()
    {
        if (muzzleFlashPrefab == null) return;

        var flash = Instantiate(muzzleFlashPrefab, Muzzle.position, Muzzle.rotation, Muzzle);
        flash.transform.localScale = Vector3.one * flashScale;
        Destroy(flash, flashDuration);
    }

    private void PlayShot()
    {
        if (shotSound == null || audioSource == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(shotSound);
    }

    private void FireRay()
    {
        if (!Physics.Raycast(Muzzle.position, Muzzle.forward, out var hit, range,
                             hitMask, QueryTriggerInteraction.Ignore))
            return;

        if (!hit.collider.CompareTag(playerTag)) return;

        var killable = hit.collider.GetComponentInParent<PlayerKillable>();
        if (killable != null && !killable.IsDead) killable.Kill();
    }

    private IEnumerator RecoilRoutine()
    {
        // الارتداد فوري ثم رجوع تدريجي — هذا التباين هو ما يعطي إحساس الطلقة
        gunVisual.localPosition = gunRest + Vector3.back * recoilDistance;

        float t = 0f;
        while (t < recoilRecover)
        {
            t += Time.deltaTime;
            float k = recoilRecover > 0f ? Mathf.Clamp01(t / recoilRecover) : 1f;
            gunVisual.localPosition = Vector3.Lerp(gunRest + Vector3.back * recoilDistance,
                                                   gunRest, k);
            yield return null;
        }

        gunVisual.localPosition = gunRest;
        recoilRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        var m = Muzzle;
        Gizmos.color = damaging ? new Color(1f, 0.3f, 0.2f, 0.9f)
                                : new Color(1f, 0.85f, 0.3f, 0.7f);
        Gizmos.DrawRay(m.position, m.forward * (damaging ? range : 2f));
        Gizmos.DrawWireSphere(m.position, 0.06f);
    }
}

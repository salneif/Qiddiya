using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// صندوق المهرج المفاجئ — يقفز في وجه اللاعب بصوت مفزع، <b>بلا أي ضرر</b>.
///
/// فلسفته: الممر مليء بما يقتل أصلًا، فلا داعي لقاتل آخر. هذا يفزع اللاعب
/// في لحظة التزامه بالعبور فيخطئ التوقيت — <b>والفاس هو الذي يقتله</b>.
/// الصندوق لم يمسّه، لكنه صنع الخطأ. وهذا أرقى من كولايدر قاتل ثالث.
///
/// ولأنه غير مؤذٍ فهو <b>لا يحتاج تحذيرًا مسبقًا</b> — المفاجأة كلها هي الفكرة.
/// (لو خليته يقتل لاحقًا عبر onPopped، فعّل وقت التحذير حتى يبقى عادلًا.)
///
/// التركيب: على كائن الصندوق، واربط <see cref="popTarget"/> بمجسم المهرج
/// الذي يقفز لأعلى.
/// </summary>
public class JumpScareBox : MonoBehaviour
{
    [Header("التفعيل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("المسافة التي يقفز عندها (متر) — اجعلها قريبة ليكون الفزع في الوجه")]
    [SerializeField] private float triggerRange = 2.2f;
    [Tooltip("مهلة قبل أن يستطيع القفز مرة أخرى (ثواني). " +
             "اتركها معقولة ليعمل بعد الموت والريسبون.")]
    [SerializeField] private float rearmDelay = 3f;

    [Header("القفزة")]
    [Tooltip("مجسم المهرج الذي ينط لأعلى")]
    [SerializeField] private Transform popTarget;
    [Tooltip("مقدار القفزة واتجاهها (محلي). يُستخدم طوله فقط عند تفعيل الاندفاع نحو اللاعب.")]
    [SerializeField] private Vector3 popOffset = new Vector3(0f, 0f, 1.2f);
    [Tooltip("ينقضّ نحو اللاعب أفقيًا بدل الاتجاه الثابت — أفزع بكثير لأنه يجي في وجهك " +
             "مهما كان دوران الصندوق أو من أي جهة جئت")]
    [SerializeField] private bool popTowardPlayer = true;
    [Tooltip("يستدير ليواجه اللاعب لحظة القفزة")]
    [SerializeField] private bool facePlayer = true;
    [Tooltip("زمن الخروج (ثواني) — اجعله قصيرًا جدًا، السرعة هي مصدر الفزع")]
    [SerializeField] private float popTime = 0.07f;
    [Tooltip("كم يبقى بالخارج قبل أن ينكمش")]
    [SerializeField] private float stayTime = 1.3f;
    [Tooltip("زمن الرجوع للصندوق — أبطأ من الخروج")]
    [SerializeField] private float retractTime = 0.7f;
    [Tooltip("ارتداد نابضي عند الخروج")]
    [SerializeField]
    private AnimationCurve popCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.7f, 1.15f), new Keyframe(1f, 1f));

    [Header("الدوران أثناء الخروج")]
    [Tooltip("لفّة عشوائية تعطي إحساس النابض المنفلت (درجات في الثانية)")]
    [SerializeField] private float spinSpeed = 220f;

    [Header("لمسات")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت القفزة — حادّ ومفاجئ")]
    [SerializeField] private AudioClip popSound;
    [Tooltip("اهتزاز الكاميرا لحظة القفزة")]
    [SerializeField] private CameraShake cameraShake;

    [Header("أحداث")]
    [Tooltip("لحظة القفزة — اتركه فارغًا ليبقى مجرد فزع بلا ضرر")]
    public UnityEvent onPopped;

    /// <summary>هل هو خارج الصندوق الآن؟</summary>
    public bool IsOut { get; private set; }

    private Transform player;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float nextAllowedTime;

    private void Awake()
    {
        if (popTarget != null)
        {
            restPosition = popTarget.localPosition;
            restRotation = popTarget.localRotation;
        }
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (IsOut || Time.time < nextAllowedTime) return;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        if (Vector3.Distance(player.position, transform.position) <= triggerRange)
            Pop();
    }

    /// <summary>يطلق القفزة يدويًا — اربطه بتريغر أو حدث آخر إذا أردت.</summary>
    public void Pop()
    {
        if (IsOut || popTarget == null) return;
        StartCoroutine(PopRoutine());
    }

    private IEnumerator PopRoutine()
    {
        IsOut = true;

        if (popSound != null && audioSource != null) audioSource.PlayOneShot(popSound);
        if (cameraShake != null) cameraShake.Shake();
        onPopped?.Invoke();

        Vector3 outPosition = restPosition + ResolvePopOffset();

        // الخروج: سريع جدًا مع ارتداد نابضي
        yield return Move(restPosition, outPosition, popTime, true);

        // يبقى بالخارج يترنّح
        float t = 0f;
        while (t < stayTime)
        {
            t += Time.deltaTime;
            popTarget.Rotate(Vector3.up, spinSpeed * 0.25f * Time.deltaTime, Space.Self);
            yield return null;
        }

        // الرجوع: أبطأ، فيبدو كأنه ينسحب للظلام
        yield return Move(outPosition, restPosition, retractTime, false);

        popTarget.localPosition = restPosition;
        popTarget.localRotation = restRotation;

        IsOut = false;
        nextAllowedTime = Time.time + rearmDelay;
    }

    /// <summary>
    /// اتجاه الاندفاع: نحو اللاعب أفقيًا إن كان مفعّلًا، وإلا الإزاحة الثابتة.
    /// يُحسب لحظة القفزة لا مسبقًا، فينقضّ من أي جهة جاء منها اللاعب.
    /// </summary>
    private Vector3 ResolvePopOffset()
    {
        if (!popTowardPlayer || player == null) return popOffset;

        Vector3 toPlayer = player.position - popTarget.position;
        toPlayer.y = 0f; // أفقي بحت — الانقضاض للأمام لا للأعلى
        if (toPlayer.sqrMagnitude < 0.0001f) return popOffset;

        Vector3 dir = toPlayer.normalized;

        if (facePlayer)
            popTarget.rotation = Quaternion.LookRotation(dir, Vector3.up);

        // نحوّل الاتجاه لفضاء الأب لأن الحركة تتم على localPosition
        Vector3 local = popTarget.parent != null
            ? popTarget.parent.InverseTransformDirection(dir)
            : dir;

        return local * popOffset.magnitude;
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float duration, bool spin)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            popTarget.localPosition = Vector3.LerpUnclamped(from, to, popCurve.Evaluate(k));

            if (spin) popTarget.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
            yield return null;
        }
        popTarget.localPosition = to;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        if (popTarget == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(popTarget.position, popTarget.position + popTarget.TransformVector(popOffset));
    }
}

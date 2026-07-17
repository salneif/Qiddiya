using UnityEngine;

/// <summary>
/// عدو مربوط بحالة العلم بأسلوب Control (Hiss المشوّه):
///  - العلم في الأرض (غير محمول) → الشكل الصلب، يطارد اللاعب ويقتله بالتلامس.
///  - اللاعب حامل العلم → يتحوّل لشكل مشوّه/شبحي (VFX، غير مؤذٍ) ويتراجع مبتعدًا.
/// يتبدّل تلقائيًا مع أحداث FlagItem (PickedUp / Placed).
///
/// حركة مباشرة بسيطة (بدون NavMesh) مناسبة للانزلاق الشبحي؛ يمكن الترقية لـ
/// NavMeshAgent لاحقًا للتنقّل حول العوائق.
/// </summary>
public class FlagGlitchEnemy : MonoBehaviour
{
    public enum State { Hunting, Retreating }

    [Header("الربط بالعلم")]
    [SerializeField] private FlagItem flag;

    [Header("الهدف")]
    [SerializeField] private string playerTag = "Player";

    [Header("الحركة")]
    [Tooltip("سرعة المطاردة (العلم بالأرض)")]
    [SerializeField] private float huntSpeed = 2.5f;
    [Tooltip("سرعة التراجع (اللاعب حامل العلم)")]
    [SerializeField] private float retreatSpeed = 4f;
    [Tooltip("المسافة التي يبتعدها ثم يقف عندها أثناء التراجع (متر)")]
    [SerializeField] private float retreatDistance = 18f;
    [Tooltip("مسافة التلامس التي يقتل عندها اللاعب (متر)")]
    [SerializeField] private float contactRange = 1.3f;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("تثبيت ارتفاع العدو على مستوى الأرض (يمنعه من الطيران)")]
    [SerializeField] private bool lockToGroundY = true;

    [Header("الأنميشن")]
    [SerializeField] private Animator animator;
    [Tooltip("اسم بارامتر Float للسرعة في الأنيميتور (0 = وقوف، >0 = مشي)")]
    [SerializeField] private string speedParam = "Speed";

    [Header("الأشكال البصرية")]
    [Tooltip("الشكل الصلب (يطارد ويؤذي)")]
    [SerializeField] private GameObject solidForm;
    [Tooltip("الشكل المشوّه/الشبح (متراجع، غير مؤذٍ، بس باين فيه شي)")]
    [SerializeField] private GameObject glitchForm;
    [Tooltip("مؤثر VFX يظهر مع الشكل المشوّه (تشويه/جسيمات)")]
    [SerializeField] private GameObject glitchVfx;

    [Header("الضرر")]
    [Tooltip("يقتل اللاعب عند التلامس أثناء المطاردة")]
    [SerializeField] private bool killOnContact = true;

    private Transform player;
    private State state;
    private int speedHash;
    private bool hasSpeedParam;
    private float groundY;

    /// <summary>هل العدو في وضع المطاردة (صلب ومؤذٍ)؟</summary>
    public bool IsHunting => state == State.Hunting;

    private void Awake()
    {
        groundY = transform.position.y;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheSpeedParam();
    }

    private void OnEnable()
    {
        if (flag != null)
        {
            flag.PickedUp += OnFlagTaken;
            flag.Placed += OnFlagDown;
        }
    }

    private void OnDisable()
    {
        if (flag != null)
        {
            flag.PickedUp -= OnFlagTaken;
            flag.Placed -= OnFlagDown;
        }
    }

    private void Start()
    {
        // الحالة الابتدائية حسب العلم (بالأرض = مطاردة)
        SetState(flag != null && flag.IsHeld ? State.Retreating : State.Hunting);
    }

    private void OnFlagTaken() => SetState(State.Retreating);
    private void OnFlagDown() => SetState(State.Hunting);

    private void SetState(State s)
    {
        state = s;
        bool hunting = s == State.Hunting;
        if (solidForm != null) solidForm.SetActive(hunting);
        if (glitchForm != null) glitchForm.SetActive(!hunting);
        if (glitchVfx != null) glitchVfx.SetActive(!hunting);
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        float speed = state == State.Hunting ? Hunt() : Retreat();
        if (hasSpeedParam) animator.SetFloat(speedHash, speed);
    }

    private float Hunt()
    {
        Vector3 to = Flatten(player.position - transform.position);
        float dist = to.magnitude;

        if (dist <= contactRange)
        {
            if (killOnContact)
            {
                var k = player.GetComponentInParent<PlayerKillable>();
                if (k != null && !k.IsDead) k.Kill();
            }
            return 0f;
        }

        Move(to.normalized, huntSpeed);
        return huntSpeed;
    }

    private float Retreat()
    {
        Vector3 away = Flatten(transform.position - player.position);
        if (away.magnitude >= retreatDistance) return 0f; // بعيد كفاية → يقف

        Move(away.normalized, retreatSpeed);
        return retreatSpeed;
    }

    private void Move(Vector3 dir, float speed)
    {
        transform.position += dir * speed * Time.deltaTime;
        if (lockToGroundY)
        {
            Vector3 p = transform.position;
            p.y = groundY;
            transform.position = p;
        }
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
        }
    }

    private Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    private void CacheSpeedParam()
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
            if (p.name == speedParam && p.type == AnimatorControllerParameterType.Float)
            {
                speedHash = Animator.StringToHash(speedParam);
                hasSpeedParam = true;
                return;
            }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRange);
        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}

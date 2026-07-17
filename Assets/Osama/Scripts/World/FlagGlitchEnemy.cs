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

    [Header("الاختفاء والظهور (Blink) — طابع الرعب")]
    [Tooltip("يومض (يختفي ويقفز) بدل المشي المستمر أثناء المطاردة")]
    [SerializeField] private bool blinkMovement = true;
    [Tooltip("مسافة القفزة الواحدة نحو اللاعب (متر)")]
    [SerializeField] private float blinkStep = 3f;
    [Tooltip("فترة الانتظار الظاهر قبل كل ومضة (ثواني)")]
    [SerializeField] private float blinkInterval = 1.2f;
    [Tooltip("مدة الاختفاء أثناء الومضة (ثواني)")]
    [SerializeField] private float blinkHidden = 0.25f;
    [Tooltip("VFX يظهر لحظة الاختفاء ولحظة الظهور (اختياري)")]
    [SerializeField] private GameObject blinkVfx;
    [Tooltip("صوت الومضة (اختياري)")]
    [SerializeField] private AudioSource blinkAudio;
    [SerializeField] private AudioClip blinkSound;

    [Header("الأنميشن")]
    [SerializeField] private Animator animator;
    [Tooltip("اسم بارامتر Float للسرعة في الأنيميتور (0 = وقوف، >0 = مشي)")]
    [SerializeField] private string speedParam = "Speed";

    [Header("قلتش المشية (حالة الشبح)")]
    [Tooltip("في حالة الشبح، المشية تشتغل لكن متقطّعة كأنها تشويه فيديو")]
    [SerializeField] private bool glitchAnimation = true;
    [Tooltip("مدة تشغيل الأنميشن قبل التجمّد (عشوائي بين X و Y ثانية)")]
    [SerializeField] private Vector2 glitchOnTime = new Vector2(0.05f, 0.16f);
    [Tooltip("مدة التجمّد (عشوائي بين X و Y ثانية)")]
    [SerializeField] private Vector2 glitchFreezeTime = new Vector2(0.03f, 0.12f);
    [Tooltip("اهتزاز مكاني بصري عند كل قفزة قلتش (متر)")]
    [SerializeField] private float glitchJitter = 0.08f;

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
    private float blinkTimer;
    private bool blinking;
    private float glitchTimer;
    private bool glitchFrozen;
    private Vector3 glitchBaseLocalPos;

    /// <summary>هل العدو في وضع المطاردة (صلب ومؤذٍ)؟</summary>
    public bool IsHunting => state == State.Hunting;

    private void Awake()
    {
        groundY = transform.position.y;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (glitchForm != null) glitchBaseLocalPos = glitchForm.transform.localPosition;
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
        // إيقاف أي ومضة جارية حتى لا تعيد تفعيل الشكل الخطأ
        StopAllCoroutines();
        blinking = false;
        blinkTimer = 0f;

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

        // يواجه اللاعب دائمًا حتى وهو واقف بين الومضات
        FaceDir(to.normalized);

        if (blinkMovement)
        {
            if (!blinking)
            {
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkInterval)
                {
                    blinkTimer = 0f;
                    StartCoroutine(DoBlink(to.normalized, dist));
                }
            }
            return 0f; // لا مشية ظاهرة — يظهر واقفًا ثم يومض
        }

        Move(to.normalized, huntSpeed);
        return huntSpeed;
    }

    private System.Collections.IEnumerator DoBlink(Vector3 dir, float distToPlayer)
    {
        blinking = true;
        PlayBlink();

        // يختفي
        if (solidForm != null) solidForm.SetActive(false);
        yield return new WaitForSeconds(blinkHidden);

        // يقفز نحو اللاعب (بدون تجاوزه)
        float step = Mathf.Min(blinkStep, Mathf.Max(0f, distToPlayer - contactRange * 0.9f));
        transform.position += dir * step;
        if (lockToGroundY)
        {
            Vector3 p = transform.position; p.y = groundY; transform.position = p;
        }

        // يظهر فجأة
        if (solidForm != null) solidForm.SetActive(true);
        PlayBlink();
        blinking = false;
    }

    private void PlayBlink()
    {
        if (blinkVfx != null)
        {
            blinkVfx.transform.position = transform.position;
            blinkVfx.SetActive(false);
            blinkVfx.SetActive(true); // إعادة تشغيل المؤثر
        }
        if (blinkSound != null && blinkAudio != null)
            blinkAudio.PlayOneShot(blinkSound);
    }

    private void FaceDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
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

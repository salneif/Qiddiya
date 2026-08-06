using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// يحرّك الكائن بين نقطتين رواحًا وجيّة بسرعة ثابتة، مع وقفة عند كل طرف.
///
/// استخدامه الأساسي في اللعبة: <b>ملجأ متحرّك</b> — تركّب عليه <see cref="SafeZone"/>
/// وكشافًا و<see cref="WorldInteractor"/>، فيمشي ببطء عبر الغرفة المظلمة ويمشي اللاعب
/// معه داخل ضوئه. الوحش لا يقدر يدخل المنطقة، فيصير العبور "مواكبة" بدل جري.
/// (SafeZone تقرأ موقعها كل إطار، فالمنطقة تتحرّك مع الكائن بلا أي كود إضافي.)
///
/// يصلح كذلك لأي منصّة متحرّكة أو عائق يروح ويجي.
///
/// التشغيل: اربط <see cref="Play"/> / <see cref="Stop"/> / <see cref="Toggle"/>
/// بـ WorldLever.onActivated أو PuzzleButton.onPressed.
/// </summary>
public class PingPongMover : MonoBehaviour
{
    [Header("المسار")]
    [Tooltip("نقطة البداية — إن تُركت فارغة يُستخدم موضع الكائن عند التشغيل")]
    [SerializeField] private Transform pointA;
    [Tooltip("نقطة النهاية — إن تُركت فارغة تُحسب من الإزاحة أدناه")]
    [SerializeField] private Transform pointB;
    [Tooltip("يُستخدم فقط عند ترك نقطة النهاية فارغة: إزاحة عن نقطة البداية")]
    [SerializeField] private Vector3 offset = new Vector3(10f, 0f, 0f);

    [Header("الحركة")]
    [Tooltip("السرعة (متر/ثانية) — صغّرها للأجسام الثقيلة")]
    [SerializeField] private float speed = 1.5f;
    [Tooltip("مدة الوقوف عند كل طرف (ثواني) — تعطي اللاعب فرصة يلحق أو يستعد")]
    [SerializeField] private float pauseAtEnds = 1f;
    [Tooltip("تباطؤ ناعم عند الطرفين بدل توقف مفاجئ — يعطي إحساس الثقل")]
    [SerializeField] private bool smoothEase = true;

    [Header("التشغيل")]
    [Tooltip("يبدأ متحرّكًا؟ أطفئه إذا الرافعة هي اللي تشغّله")]
    [SerializeField] private bool startMoving = true;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت آلية مستمر — يعلو أثناء الحركة ويسكت عند الوقفات وعند الإيقاف")]
    [SerializeField] private AudioClip moveLoop;
    [Range(0f, 1f)]
    [SerializeField] private float moveVolume = 0.6f;

    [Header("أحداث")]
    [Tooltip("عند الوصول لنقطة البداية")]
    public UnityEvent onReachedA;
    [Tooltip("عند الوصول لنقطة النهاية")]
    public UnityEvent onReachedB;

    /// <summary>هل هو متحرّك الآن؟</summary>
    public bool IsMoving { get; private set; }

    private Vector3 a, b;
    private float t;          // 0 عند A، 1 عند B
    private int direction = 1;
    private float pauseTimer;

    private void Awake()
    {
        a = pointA != null ? pointA.position : transform.position;
        b = pointB != null ? pointB.position : a + offset;

        IsMoving = startMoving;
        transform.position = a;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null && moveLoop != null)
        {
            audioSource.clip = moveLoop;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();
        }
    }

    /// <summary>يشغّل الحركة — اربطه بحدث الرافعة.</summary>
    public void Play() => IsMoving = true;

    /// <summary>يوقف الحركة في مكانها.</summary>
    public void Stop() => IsMoving = false;

    /// <summary>يبدّل بين التشغيل والإيقاف.</summary>
    public void Toggle() => IsMoving = !IsMoving;

    private void Update()
    {
        bool movingNow = false;

        if (IsMoving)
        {
            if (pauseTimer > 0f) pauseTimer -= Time.deltaTime;
            else movingNow = Step();
        }

        UpdateSound(movingNow);
    }

    /// <summary>خطوة حركة واحدة. يُرجع true إذا تحرّك فعلًا هذا الإطار.</summary>
    private bool Step()
    {
        float distance = Vector3.Distance(a, b);
        if (distance < 0.001f) return false;

        // القسمة على المسافة تجعل السرعة بالمتر/ثانية مهما طال المسار
        t += direction * (speed / distance) * Time.deltaTime;

        if (t >= 1f)
        {
            t = 1f;
            direction = -1;
            pauseTimer = pauseAtEnds;
            onReachedB?.Invoke();
        }
        else if (t <= 0f)
        {
            t = 0f;
            direction = 1;
            pauseTimer = pauseAtEnds;
            onReachedA?.Invoke();
        }

        float k = smoothEase ? Mathf.SmoothStep(0f, 1f, t) : t;
        transform.position = Vector3.Lerp(a, b, k);
        return true;
    }

    /// <summary>
    /// يربط مستوى الصوت بالحركة الفعلية: يعلو أثناء السير ويخفت في الوقفات
    /// وعند الإيقاف — بدل تشغيل/إيقاف مفاجئ.
    /// </summary>
    private void UpdateSound(bool movingNow)
    {
        if (audioSource == null || moveLoop == null) return;

        // ضمان دوران الحلقة مهما تغيّر إعداد المصدر بعد Awake
        if (!audioSource.isPlaying)
        {
            audioSource.clip = moveLoop;
            audioSource.loop = true;
            audioSource.Play();
        }

        float wanted = movingNow ? moveVolume : 0f;
        audioSource.volume = Mathf.Lerp(audioSource.volume, wanted, Time.deltaTime * 8f);
    }

    private void OnDrawGizmosSelected()
    {
        // في وضع التحرير نحسب الطرفين من الإعدادات مباشرة لتظهر قبل التشغيل
        Vector3 from = pointA != null ? pointA.position : transform.position;
        Vector3 to = pointB != null ? pointB.position : from + offset;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.35f);
        Gizmos.DrawWireSphere(to, 0.35f);
    }
}

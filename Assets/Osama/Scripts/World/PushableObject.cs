using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// جسم يدفعه اللاعب يمينًا ويسارًا (عربة/فانوس/صندوق) بأسلوب Little Nightmares.
///
/// الاستخدام الأساسي: <b>لمبة متنقّلة</b> — تركّب عليه <see cref="SafeZone"/> وكشافًا
/// و<see cref="WorldInteractor"/>، فيصير اللاعب يدفع ملجأه معه عبر الغرفة المظلمة.
/// الوحش لا يدخل المنطقة، فالعبور يصير "دفع ومواكبة" بدل جري.
///
/// الحركة تتبع <b>مشية اللاعب نفسها</b> لا الأزرار: كل ما تحرّك اللاعب على المحور
/// وهو ماسك الجسم، يتحرك الجسم بنفس المقدار. فيشتغل مع أي متحكّم بلا ربط إدخال.
///
/// يعمل بالمسافة لا بالتريغر — حتى لا يتعارض مع كولايدر الجسم الصلب الذي يوقف اللاعب
/// (نفس أسلوب <see cref="WorldLever"/>).
/// </summary>
public class PushableObject : MonoBehaviour
{
    [Header("التفاعل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("أقصى مسافة يقدر اللاعب يمسك منها (متر)")]
    [SerializeField] private float grabRange = 2.5f;
    [Tooltip("زر المسك")]
    [SerializeField] private Key grabKey = Key.E;
    [Tooltip("مفعّل: يمسك ما دام الزر مضغوطًا. مطفي: ضغطة تمسك وضغطة تفلت.")]
    [SerializeField] private bool holdToGrab = true;

    [Header("الحركة")]
    [Tooltip("المحور الذي ينزلق عليه الجسم — عادة X للعبة الجانبية")]
    [SerializeField] private Vector3 moveAxis = Vector3.right;
    [Tooltip("أقصى مسافة للخلف عن موضع البداية (متر، رقم سالب)")]
    [SerializeField] private float minOffset = -10f;
    [Tooltip("أقصى مسافة للأمام عن موضع البداية (متر)")]
    [SerializeField] private float maxOffset = 10f;
    [Tooltip("نسبة سرعة الجسم لسرعة اللاعب. 1 = يمشي معه تمامًا، " +
             "أقل = ثقيل ويتأخر عنه فيحس اللاعب بوزنه.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float weightFactor = 1f;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت جرجرة مستمر أثناء الدفع — يعلو مع سرعة الحركة")]
    [SerializeField] private AudioClip dragLoop;
    [Range(0f, 1f)]
    [SerializeField] private float dragVolume = 0.6f;

    [Header("أحداث")]
    [Tooltip("عند مسك الجسم")]
    public UnityEvent onGrabbed;
    [Tooltip("عند إفلاته")]
    public UnityEvent onReleased;

    /// <summary>هل اللاعب ماسك الجسم الآن؟</summary>
    public bool IsGrabbed { get; private set; }

    private Transform player;
    private Vector3 startPosition;
    private float currentOffset;
    private float lastPlayerAlong;

    private Vector3 Axis => moveAxis.sqrMagnitude > 0.0001f ? moveAxis.normalized : Vector3.right;

    private void Awake()
    {
        startPosition = transform.position;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource != null && dragLoop != null)
        {
            audioSource.clip = dragLoop;
            audioSource.loop = true;
            audioSource.volume = 0f;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        UpdateGrabState();

        float moved = IsGrabbed ? FollowPlayer() : 0f;

        // صوت الجرجرة يعلو مع مقدار الحركة ويسكت عند الوقوف
        if (audioSource != null && dragLoop != null)
        {
            float target = Mathf.Approximately(Time.deltaTime, 0f)
                ? 0f
                : Mathf.Clamp01(Mathf.Abs(moved) / Time.deltaTime) * dragVolume;
            audioSource.volume = Mathf.Lerp(audioSource.volume, target, Time.deltaTime * 10f);
        }
    }

    private void UpdateGrabState()
    {
        bool inRange = Vector3.Distance(player.position, transform.position) <= grabRange;
        bool wasGrabbed = IsGrabbed;

        if (Keyboard.current == null || grabKey == Key.None)
        {
            IsGrabbed = false;
        }
        else if (holdToGrab)
        {
            IsGrabbed = inRange && Keyboard.current[grabKey].isPressed;
        }
        else if (Keyboard.current[grabKey].wasPressedThisFrame)
        {
            // ضغطة تمسك وضغطة تفلت — والإفلات مسموح حتى لو ابتعد
            if (IsGrabbed) IsGrabbed = false;
            else if (inRange) IsGrabbed = true;
        }

        if (IsGrabbed == wasGrabbed) return;

        if (IsGrabbed)
        {
            // نلتقط موضع اللاعب لحظة المسك حتى لا يقفز الجسم دفعة واحدة
            lastPlayerAlong = Vector3.Dot(player.position, Axis);
            onGrabbed?.Invoke();
        }
        else
        {
            onReleased?.Invoke();
        }
    }

    /// <summary>يحرّك الجسم بمقدار ما تحرّك اللاعب على المحور. يُرجع المسافة المطبَّقة فعلًا.</summary>
    private float FollowPlayer()
    {
        float playerAlong = Vector3.Dot(player.position, Axis);
        float delta = (playerAlong - lastPlayerAlong) * weightFactor;
        lastPlayerAlong = playerAlong;

        if (Mathf.Abs(delta) < 0.00001f) return 0f;

        float clamped = Mathf.Clamp(currentOffset + delta, minOffset, maxOffset);
        float applied = clamped - currentOffset;
        currentOffset = clamped;

        transform.position = startPosition + Axis * currentOffset;
        return applied;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? startPosition : transform.position;
        Vector3 axis = Axis;

        // مسار الانزلاق المسموح
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Vector3 from = origin + axis * minOffset;
        Vector3 to = origin + axis * maxOffset;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.3f);
        Gizmos.DrawWireSphere(to, 0.3f);

        // مدى المسك
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, grabRange);
    }
}

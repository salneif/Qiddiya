using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// لوحة تحكّم ثابتة تحرّك جسمًا بعيدًا يمينًا ويسارًا — يقف عندها اللاعب ويشوف
/// اللمبة (أو أي هدف) تتحرك أمامه.
///
/// الاستخدام: تركّب على الرافعة/المقبض، ويُربط <see cref="target"/> باللمبة التي
/// عليها <see cref="SafeZone"/> وكشاف و<see cref="WorldInteractor"/>. فيوجّه اللاعب
/// ملجأه من بعيد قبل أن يعبر، والوحش لا يدخل الضوء.
///
/// أثناء الإمساك بالتحكّم تتعطّل حركة اللاعب (عبر <see cref="disableWhileUsing"/>)،
/// ويصير زرّا اليمين واليسار يحرّكان الهدف بدل الشخصية — فلا يتعارض الإدخالان.
/// </summary>
public class RemoteSlideControl : MonoBehaviour
{
    /// <summary>شكل حركة المقبض.</summary>
    public enum HandleMode
    {
        [InspectorName("رافعة تميل")] Tilt,
        [InspectorName("مرفاع يلف")] Crank
    }

    [Header("التفاعل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("أقصى مسافة يقدر اللاعب يمسك منها التحكّم (متر)")]
    [SerializeField] private float interactRange = 2.5f;
    [Tooltip("زر الإمساك بالتحكّم وتركه")]
    [SerializeField] private Key interactKey = Key.E;
    [Tooltip("يفلت التحكّم غصبًا إذا ابتعد اللاعب أكثر من هذه المسافة (متر) — " +
             "يغطي الموت والريسبون والانتقال المفاجئ الذي لا يمر بزر الترك")]
    [SerializeField] private float autoReleaseDistance = 4f;

    [Header("الهدف المتحرّك")]
    [Tooltip("الجسم البعيد الذي يتحرك — اللمبة التي عليها SafeZone والكشاف")]
    [SerializeField] private Transform target;
    [Tooltip("المحور الذي ينزلق عليه الهدف — عادة X للعبة الجانبية")]
    [SerializeField] private Vector3 moveAxis = Vector3.right;
    [Tooltip("أقصى مسافة للخلف عن موضع الهدف الأصلي (رقم سالب)")]
    [SerializeField] private float minOffset = -10f;
    [Tooltip("أقصى مسافة للأمام عن موضع الهدف الأصلي")]
    [SerializeField] private float maxOffset = 10f;
    [Tooltip("سرعة انزلاق الهدف (متر/ثانية) — صغّرها ليحس اللاعب بثقله")]
    [SerializeField] private float moveSpeed = 2f;

    [Header("التحكّم أثناء الإمساك")]
    [Tooltip("سكربتات تتعطّل أثناء استخدام التحكّم: سكربت حركة اللاعب، وسكربت متابعة " +
             "الكاميرا. النوع MonoBehaviour عمدًا حتى لا يمكن وضع مكوّن Camera بالغلط " +
             "(تعطيله يطفئ الشاشة).")]
    [SerializeField] private MonoBehaviour[] disableWhileUsing;
    [SerializeField] private Key leftKey = Key.A;
    [SerializeField] private Key rightKey = Key.D;

    [Header("المقبض (بصري، اختياري)")]
    [Tooltip("محوّل المقبض")]
    [SerializeField] private Transform handle;
    [Tooltip("Tilt = رافعة تميل لجهة الحركة وترجع. Crank = مرفاع يلف باستمرار ما دمت تحرّك.")]
    [SerializeField] private HandleMode handleMode = HandleMode.Tilt;

    [Tooltip("[Tilt] أقصى ميلان للمقبض عند الدفع لجهة (Euler محلي)")]
    [SerializeField] private Vector3 maxTilt = new Vector3(0f, 0f, 25f);
    [SerializeField] private float handleSpeed = 8f;

    [Tooltip("[Crank] محور لفّ المرفاع المحلي")]
    [SerializeField] private Vector3 crankAxis = Vector3.forward;
    [Tooltip("[Crank] سرعة اللفّ (درجة/ثانية)")]
    [SerializeField] private float crankSpeed = 220f;

    [Header("صوت المرفاع (عند يدك)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت آلية المرفاع الذي تديره")]
    [SerializeField] private AudioClip moveLoop;
    [Range(0f, 1f)]
    [SerializeField] private float moveVolume = 0.6f;

    [Header("صوت السكة (عند اللمبة)")]
    [Tooltip("مصدر صوت على الهدف المتحرّك نفسه — صرير السكة فوقك. " +
             "منفصل عن صوت المرفاع، فتسمع الاثنين من مكانين مختلفين ويصير للآلة جسد.")]
    [SerializeField] private AudioSource targetAudioSource;
    [Tooltip("صوت انزلاق اللمبة على السكة")]
    [SerializeField] private AudioClip targetMoveLoop;
    [Range(0f, 1f)]
    [SerializeField] private float targetMoveVolume = 0.7f;

    [Header("أحداث")]
    [Tooltip("عند الإمساك بالتحكّم")]
    public UnityEvent onEngaged;
    [Tooltip("عند تركه")]
    public UnityEvent onReleased;

    /// <summary>هل اللاعب ماسك التحكّم الآن؟</summary>
    public bool IsEngaged { get; private set; }

    private Transform player;
    private PlayerKillable killable;
    private Vector3 targetStart;
    private float currentOffset;
    private Quaternion handleRest;

    /// <summary>عطّلنا حركة اللاعب ثم مات — ننتظر بعثه لنعيدها بدل أن نحرّره وهو ميت.</summary>
    private bool restorePending;

    private Vector3 Axis => moveAxis.sqrMagnitude > 0.0001f ? moveAxis.normalized : Vector3.right;

    private void Awake()
    {
        if (target != null) targetStart = target.position;
        if (handle != null) handleRest = handle.localRotation;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (audioSource != null && moveLoop != null)
        {
            audioSource.clip = moveLoop;
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
            killable = player.GetComponentInParent<PlayerKillable>();
        }

        UpdateAutoRelease();
        UpdateEngageState();

        float input = IsEngaged ? ReadDirection() : 0f;
        if (IsEngaged) MoveTarget(input);

        UpdateHandle(input);
        UpdateSound(input);
    }

    private void UpdateEngageState()
    {
        if (Keyboard.current == null || interactKey == Key.None) return;
        if (!Keyboard.current[interactKey].wasPressedThisFrame) return;

        if (IsEngaged)
        {
            SetEngaged(false);
        }
        else if (Vector3.Distance(player.position, transform.position) <= interactRange)
        {
            SetEngaged(true);
        }
    }

    /// <summary>
    /// يفلت التحكّم غصبًا عند الموت أو الابتعاد. مهم لأن الموت والريسبون ينقلان
    /// اللاعب بلا مروره بزر الترك، فيبقى التحكّم ممسوكًا وحركته معطّلة للأبد.
    /// </summary>
    private void UpdateAutoRelease()
    {
        if (IsEngaged)
        {
            bool dead = killable != null && killable.IsDead;
            bool tooFar = Vector3.Distance(player.position, transform.position) > autoReleaseDistance;

            if (dead)
            {
                // لا نعيد تفعيل حركته وهو ميت — PlayerKillable هو المتحكّم بها الآن،
                // ونؤجّل الإعادة حتى يُبعث
                IsEngaged = false;
                restorePending = true;
                onReleased?.Invoke();
            }
            else if (tooFar)
            {
                SetEngaged(false);
            }
        }

        // بُعث بعد ما مات وهو ماسك → نرجّع له حركته
        if (restorePending && (killable == null || !killable.IsDead))
        {
            restorePending = false;
            SetScriptsEnabled(true);
        }
    }

    private void SetEngaged(bool on)
    {
        IsEngaged = on;
        SetScriptsEnabled(!on);

        if (on) onEngaged?.Invoke();
        else onReleased?.Invoke();
    }

    /// <summary>تعطيل/تفعيل حركة اللاعب حتى لا يتنازع الإدخال مع تحريك الهدف.</summary>
    private void SetScriptsEnabled(bool enabled)
    {
        if (disableWhileUsing == null) return;

        foreach (var b in disableWhileUsing)
            if (b != null) b.enabled = enabled;
    }

    /// <summary>
    /// يعيد الهدف لموضع بدايته ويفلت التحكّم — اربطه بـ PlayerKillable.onRespawn.
    /// </summary>
    public void ResetTarget()
    {
        if (IsEngaged) SetEngaged(false);

        currentOffset = 0f;
        if (target != null) target.position = targetStart;
    }

    /// <summary>-1 يسار، +1 يمين، 0 وقوف.</summary>
    private float ReadDirection()
    {
        if (Keyboard.current == null) return 0f;
        float dir = 0f;
        if (leftKey != Key.None && Keyboard.current[leftKey].isPressed) dir -= 1f;
        if (rightKey != Key.None && Keyboard.current[rightKey].isPressed) dir += 1f;
        return dir;
    }

    private void MoveTarget(float direction)
    {
        if (target == null || Mathf.Approximately(direction, 0f)) return;

        currentOffset = Mathf.Clamp(currentOffset + direction * moveSpeed * Time.deltaTime,
                                    minOffset, maxOffset);
        target.position = targetStart + Axis * currentOffset;
    }

    private void UpdateHandle(float direction)
    {
        if (handle == null) return;

        if (handleMode == HandleMode.Crank)
        {
            // يلف ما دام اللاعب يحرّك، ويتجمّد فور توقفه — كمرفاع حقيقي.
            // لا يرجع لوضع البداية، فاللفّة تتراكم كما هو متوقع.
            if (!Mathf.Approximately(direction, 0f))
                handle.Rotate(crankAxis.normalized,
                              direction * crankSpeed * Time.deltaTime, Space.Self);
            return;
        }

        Quaternion wanted = handleRest * Quaternion.Euler(maxTilt * direction);
        handle.localRotation = Quaternion.Slerp(handle.localRotation, wanted,
                                                Time.deltaTime * handleSpeed);
    }

    private void UpdateSound(float direction)
    {
        // الصوت يشتغل فقط أثناء حركة فعلية (لا عند الدفع على الحد)
        bool atLimit = (direction < 0f && currentOffset <= minOffset) ||
                       (direction > 0f && currentOffset >= maxOffset);
        bool moving = !Mathf.Approximately(direction, 0f) && !atLimit;

        DriveLoop(audioSource, moveLoop, moving ? moveVolume : 0f);
        DriveLoop(targetAudioSource, targetMoveLoop, moving ? targetMoveVolume : 0f);
    }

    /// <summary>
    /// يدير حلقة صوت ويضبط مستواها بنعومة. يعيد التشغيل إن توقّفت لأي سبب،
    /// وإلا بقيت صامتة للأبد بعد أول توقف.
    /// </summary>
    private static void DriveLoop(AudioSource src, AudioClip clip, float wanted)
    {
        if (src == null || clip == null) return;

        if (!src.isPlaying)
        {
            src.clip = clip;
            src.loop = true;
            src.Play();
        }

        src.volume = Mathf.Lerp(src.volume, wanted, Time.deltaTime * 10f);
    }

    private void OnDrawGizmosSelected()
    {
        // مدى الإمساك
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (target == null) return;

        Vector3 origin = Application.isPlaying ? targetStart : target.position;
        Vector3 axis = Axis;

        // مسار الهدف
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Vector3 from = origin + axis * minOffset;
        Vector3 to = origin + axis * maxOffset;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.3f);
        Gizmos.DrawWireSphere(to, 0.3f);

        // الرابط بين التحكّم والهدف
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        Gizmos.DrawLine(transform.position, target.position);
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// عين الروبوت القاتلة بأسلوب Little Nightmares.
///
/// الفكرة: الخطر مرتبط بـ<b>اشتعال العيون فقط</b> (لا الأشرطة السفلية):
///  - عند توهّج العيون تُضاء كشافات (Spot Lights) من مكان العيون، فترمي ضوءًا
///    وظلالًا للأمام — هذا هو "الضل" التحذيري الذي يظهر على الجدران وعلى اللاعب،
///    فيعرف اللاعب أن العين ولّعت وأنه مكشوف.
///  - أثناء التوهّج يُفحص خط النظر (Line of Sight) من العيون نحو اللاعب:
///      • إن كان الطريق مكشوفًا (لا جدار حاجز) واللاعب داخل المدى/الزاوية → يُقتل.
///      • إن كان اللاعب "زابن" خلف جدار على طبقة العوائق → يُحجب الشعاع فلا يموت.
///
/// يُركّب على كائن الروبوت، ويُربط مع <see cref="RobotFurnaceChargeSequence"/>
/// الذي ينادي <see cref="BeginEyeGlow"/> / <see cref="TryKillPlayer"/> / <see cref="EndEyeGlow"/>
/// في التوقيت الصحيح من تسلسل الشحن.
/// </summary>
public class RobotEyeAttack : MonoBehaviour
{
    [Header("مصادر النظر (العيون)")]
    [Tooltip("نقاط انطلاق النظر — عادة محوّلات العيون eyeR / eyeL. يكفي أن ترى عينٌ واحدة اللاعب.")]
    [SerializeField] private Transform[] eyeOrigins;

    [Header("الهدف (اللاعب)")]
    [Tooltip("وسم كائن اللاعب للبحث التلقائي")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("محوّل اللاعب — إذا تُرك فارغًا يُبحث عنه تلقائيًا بالوسم")]
    [SerializeField] private Transform player;
    [Tooltip("ارتفاع نقطة التصويب على اللاعب من قدمه (متر) — الأفضل صدر/رأس حتى لا يحجبه الأرض")]
    [SerializeField] private float playerAimHeight = 1.0f;

    [Header("شروط الرؤية")]
    [Tooltip("طبقات الجدران/العوائق التي تحجب النظر. ⚠️ لا تضع طبقة اللاعب هنا.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("أقصى مدى للنظر القاتل (متر)")]
    [SerializeField] private float maxRange = 25f;
    [Tooltip("نصف زاوية مخروط الرؤية بالدرجات. 180 = يرى بكل الاتجاهات (يعتمد على خط النظر فقط). " +
             "قلّلها لجعله يرى للأمام فقط، واضبط \"اتجاه النظر\" عندها.")]
    [Range(1f, 180f)]
    [SerializeField] private float visionHalfAngle = 180f;
    [Tooltip("اتجاه نظر الروبوت للأمام (محلي). يُقاس مخروط الرؤية حوله عند تقليل الزاوية.")]
    [SerializeField] private Vector3 forwardAxis = Vector3.forward;

    [Header("كشافات العيون (الضل التحذيري)")]
    [Tooltip("كشافات Spot Light عند العيون تُضاء أثناء التوهّج وتُسقط الظلال. " +
             "فعّل عليها Shadows (Soft/Hard) حتى يظهر ظل اللاعب على الجدار.")]
    [SerializeField] private Light[] eyeLights;
    [Tooltip("شدة كشافات العيون أثناء التوهّج")]
    [SerializeField] private float eyeLightIntensity = 12f;

    [Header("عند القتل")]
    [Tooltip("يُستدعى مرة واحدة عند قتل اللاعب (اهتزاز كاميرا / صوت ذبح / VFX...)")]
    [SerializeField] private UnityEvent onPlayerKilled;

    [Header("تشخيص")]
    [Tooltip("رسم مخروط الرؤية وخطوط النظر في مشهد Scene عند اختيار الكائن")]
    [SerializeField] private bool drawGizmos = true;

    private PlayerKillable cachedKillable;
    private bool eyesGlowing;

    private Vector3 ForwardDir => transform.TransformDirection(
        forwardAxis.sqrMagnitude > 0.0001f ? forwardAxis.normalized : Vector3.forward);

    private void Awake()
    {
        ResolvePlayer();
        SetEyeLights(false);
    }

    private void ResolvePlayer()
    {
        if (player == null && !string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
        }
        if (player != null && cachedKillable == null)
            cachedKillable = player.GetComponentInParent<PlayerKillable>();
    }

    /// <summary>تُنادى عند بدء اشتعال العيون: تُضيء الكشافات فيظهر الضوء والظل التحذيري.</summary>
    public void BeginEyeGlow()
    {
        eyesGlowing = true;
        SetEyeLights(true);
    }

    /// <summary>تُنادى عند انطفاء العيون: تُطفئ الكشافات.</summary>
    public void EndEyeGlow()
    {
        eyesGlowing = false;
        SetEyeLights(false);
    }

    /// <summary>
    /// يفحص خط النظر الآن ويقتل اللاعب إذا كان مكشوفًا. يُرجع true إذا تمّ القتل فعلًا.
    /// آمن للنداء كل إطار (لن يقتل ميّتًا مرتين).
    /// </summary>
    public bool TryKillPlayer()
    {
        if (player == null) ResolvePlayer();
        if (player == null) return false;

        if (cachedKillable == null)
            cachedKillable = player.GetComponentInParent<PlayerKillable>();
        if (cachedKillable == null || cachedKillable.IsDead) return false;

        if (!HasLineOfSight()) return false;

        cachedKillable.Kill();
        onPlayerKilled?.Invoke();
        return true;
    }

    /// <summary>
    /// هل توجد رؤية مكشوفة من أي عين نحو اللاعب ضمن المدى والزاوية دون حاجز؟
    /// </summary>
    public bool HasLineOfSight()
    {
        if (player == null) return false;

        Vector3 target = player.position + Vector3.up * playerAimHeight;

        // فحص المدى والزاوية من مركز الروبوت
        Vector3 toTarget = target - transform.position;
        if (toTarget.magnitude > maxRange) return false;
        if (visionHalfAngle < 180f &&
            Vector3.Angle(ForwardDir, toTarget) > visionHalfAngle) return false;

        // يكفي أن تكون عينٌ واحدة مكشوفة الطريق نحو اللاعب
        var origins = HasEyeOrigins ? eyeOrigins : oneSelf;
        foreach (var eye in origins)
        {
            if (eye == null) continue;
            // إذا لم يعترض الطريقَ أي عائق بين العين واللاعب → الرؤية مكشوفة
            if (!Physics.Linecast(eye.position, target, obstacleMask, QueryTriggerInteraction.Ignore))
                return true;
        }
        return false;
    }

    private bool HasEyeOrigins => eyeOrigins != null && eyeOrigins.Length > 0;
    private Transform[] oneSelf => _selfArray ??= new[] { transform };
    private Transform[] _selfArray;

    private void SetEyeLights(bool on)
    {
        if (eyeLights == null) return;
        foreach (var l in eyeLights)
        {
            if (l == null) continue;
            l.enabled = on;
            if (on) l.intensity = eyeLightIntensity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 c = transform.position;
        Vector3 f = ForwardDir;

        // مخروط الرؤية
        Gizmos.color = eyesGlowing ? Color.red : new Color(1f, 0.6f, 0f, 0.7f);
        Gizmos.DrawRay(c, f * maxRange);
        if (visionHalfAngle < 180f)
        {
            Vector3 left = Quaternion.AngleAxis(-visionHalfAngle, Vector3.up) * f;
            Vector3 right = Quaternion.AngleAxis(visionHalfAngle, Vector3.up) * f;
            Gizmos.DrawRay(c, left * maxRange);
            Gizmos.DrawRay(c, right * maxRange);
        }

        // خطوط النظر الفعلية نحو اللاعب (أحمر = مكشوف/قاتل، رمادي = محجوب)
        if (player != null)
        {
            Vector3 target = player.position + Vector3.up * playerAimHeight;
            var origins = HasEyeOrigins ? eyeOrigins : new[] { transform };
            foreach (var eye in origins)
            {
                if (eye == null) continue;
                bool blocked = Physics.Linecast(eye.position, target, obstacleMask, QueryTriggerInteraction.Ignore);
                Gizmos.color = blocked ? Color.gray : Color.red;
                Gizmos.DrawLine(eye.position, target);
            }
        }
    }
}

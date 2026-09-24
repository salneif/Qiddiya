using UnityEngine;

/// <summary>
/// يخلّي اللاعب في منتصف المنصّة المتحرّكة ما دامت تتحرك — عشان ما يعلق في فتحة
/// ضيّقة فوقه وهو واقف على طرفها.
///
/// ما يمسك اللاعب ولا يلغي تحكّمه: يسحبه بهدوء للمركز فقط إذا ابتعد أكثر من
/// <see cref="keepRadius"/>، والسحب يتوقف تمامًا لحظة ما تقف المنصّة — فيمشي
/// وينزل منها بحرية عند وصولها.
///
/// التركيب: حُطّه على كائن المنصّة نفسها (أو كائن فارغ في مركزها ابنًا لها).
/// ما يحتاج ربط أي شيء — يلقى اللاعب بالوسم.
/// </summary>
[DisallowMultipleComponent]
public class PlatformRideAssist : MonoBehaviour
{
    [Header("اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("المنطقة")]
    [Tooltip("مركز المنصّة — اتركه فارغًا ليُستخدم هذا الكائن")]
    [SerializeField] private Transform center;
    [Tooltip("كولايدر يحدد متى يُعتبر اللاعب راكبًا (Is Trigger مفضّل). " +
             "اتركه فارغًا لتُستخدم الأرقام تحته.")]
    [SerializeField] private Collider rideZone;
    [Tooltip("نصف قطر المنصّة (متر) — يُستخدم إذا ما حددت كولايدر")]
    [SerializeField] private float zoneRadius = 2.5f;
    [Tooltip("ارتفاع المنطقة فوق المنصّة وتحتها (متر)")]
    [SerializeField] private float zoneHeight = 3f;

    [Header("التوسيط")]
    [Tooltip("أقصى ابتعاد مسموح عن المركز (متر) — أبعد منه يُسحب اللاعب للداخل بهدوء")]
    [SerializeField] private float keepRadius = 0.4f;
    [Tooltip("سرعة السحب للمركز (متر/ثانية) — صغّرها ليكون ألطف")]
    [SerializeField] private float pullSpeed = 3f;
    [Tooltip("لا يسحبه إلا والمنصّة تتحرك فعلًا. أطفئه ليبقى موسّطًا دائمًا " +
             "(⚠️ عندها ما يقدر يمشي لطرفها وهي واقفة).")]
    [SerializeField] private bool onlyWhileMoving = true;
    [Tooltip("أقل سرعة للمنصّة (متر/ثانية) تُحسب حركة")]
    [SerializeField] private float movingThreshold = 0.05f;

    private Transform player;
    private CharacterController controller;
    private Vector3 lastCenterPos;
    private bool hasLastCenterPos;
    private bool moving;

    private Transform Center => center != null ? center : transform;

    private void LateUpdate()
    {
        UpdateMoving();

        if (!ResolvePlayer()) return;
        if (!PlayerIsRiding()) return;
        if (onlyWhileMoving && !moving) return;

        Vector3 offset = Flatten(player.position - Center.position);
        float dist = offset.magnitude;
        if (dist <= keepRadius) return;

        // نسحبه للداخل بقدر التجاوز فقط، فلا يُدفع لمركز المنصّة قسرًا
        float step = Mathf.Min(pullSpeed * Time.deltaTime, dist - keepRadius);
        MovePlayer(-offset.normalized * step);
    }

    /// <summary>هل المنصّة تتحرك الآن؟ يُقاس من حركتها هي، فلا يحتاج سكربتها.</summary>
    private void UpdateMoving()
    {
        Vector3 now = Center.position;
        if (!hasLastCenterPos)
        {
            lastCenterPos = now;
            hasLastCenterPos = true;
            return;
        }

        float speed = Time.deltaTime > 0f ? (now - lastCenterPos).magnitude / Time.deltaTime : 0f;
        moving = speed >= movingThreshold;
        lastCenterPos = now;
    }

    private bool ResolvePlayer()
    {
        if (player != null) return true;

        var go = PlayerLocator.Find(playerTag);
        if (go == null) return false;

        player = go.transform;
        controller = player.GetComponentInParent<CharacterController>();
        return true;
    }

    /// <summary>هل اللاعب فوق المنصّة؟ بالكولايدر إن وُجد، وإلا بأسطوانة حول المركز.</summary>
    private bool PlayerIsRiding()
    {
        Vector3 p = player.position;

        if (rideZone != null)
        {
            Bounds b = rideZone.bounds;
            b.Expand(0.2f);
            return b.Contains(p);
        }

        Vector3 c = Center.position;
        if (Mathf.Abs(p.y - c.y) > zoneHeight) return false;
        return Flatten(p - c).sqrMagnitude <= zoneRadius * zoneRadius;
    }

    /// <summary>يحرّك اللاعب باحترام الاصطدامات إن كان عليه CharacterController.</summary>
    private void MovePlayer(Vector3 delta)
    {
        if (controller != null && controller.enabled)
        {
            controller.Move(delta);
            return;
        }
        player.position += delta;
    }

    private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    private void OnValidate()
    {
        zoneRadius = Mathf.Max(0.1f, zoneRadius);
        zoneHeight = Mathf.Max(0.1f, zoneHeight);
        keepRadius = Mathf.Max(0f, keepRadius);
        pullSpeed = Mathf.Max(0.1f, pullSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 c = Center.position;

        // الأصفر: منطقة الركوب • الأخضر: المدى المسموح قبل السحب
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
        if (rideZone != null) Gizmos.DrawWireCube(rideZone.bounds.center, rideZone.bounds.size);
        else Gizmos.DrawWireCube(c, new Vector3(zoneRadius * 2f, zoneHeight * 2f, zoneRadius * 2f));

        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(c, Quaternion.identity, new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, keepRadius);
        Gizmos.matrix = Matrix4x4.identity;
    }
}

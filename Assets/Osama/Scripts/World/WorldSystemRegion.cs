using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// منطقة تشغيل "نظام العالم" (الأبيض/الأسود + دائرة العلم) داخل سين عادي.
///
/// السين افتراضيًا ملوّن وطبيعي تمامًا. أول ما يدخل اللاعب هذه المنطقة يشتعل
/// النظام: العالم يصير أبيض/أسود وتشتغل كائنات المنطقة (المونستر، الأفخاخ...)،
/// وأول ما يطلع منها يرجع كل شي طبيعي وتنطفي كائناتها.
///
/// النظام "مطفي" = صفر تكلفة فعلًا: لما <c>_WorldBWAmount = 0</c> يخرج شيدر
/// الفولسكرين من أول سطر ويرجّع الصورة كما هي بلا أي معالجة.
///
/// ⚠️ يفحص موقع اللاعب كل إطار بدل أحداث التريغر — لأن الـ TP والموت والريسبون
/// تنقل اللاعب فجأة (وتعطّل الـ CharacterController أثناءها)، فأحداث
/// OnTriggerExit ممكن ما تنطلق أصلًا ويعلق العالم أبيض/أسود وأنت برّا المنطقة.
///
/// التركيب:
///  - كائن فيه Collider (Is Trigger) يغطّي حدود المنطقة + هذا السكربت.
///  - حط المونستر وأفخاخ المرحلة في "كائنات المنطقة" فتشتغل داخلها فقط.
///  - لا تحط العلم فيها — العلم يسافر مع اللاعب بين المناطق.
///  - ⚠️ أطفئ <c>Start Black And White</c> في WorldBWController، لأن السين
///    الحين يبدأ طبيعيًا والمنطقة هي اللي تشغّل النظام.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldSystemRegion : MonoBehaviour
{
    /// <summary>المنطقة التي يقف فيها اللاعب الآن (null = خارج كل المناطق).</summary>
    public static WorldSystemRegion Current { get; private set; }

    [Header("الهدف")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("نظام العالم")]
    [Tooltip("متحكّم الأبيض/الأسود — يُلتقط تلقائيًا من المشهد إذا تُرك فارغًا")]
    [SerializeField] private WorldBWController bw;
    [Tooltip("تحوّل فوري بلا تدرّج. أطفئه للانتقال الناعم حسب مدة WorldBWController")]
    [SerializeField] private bool instant = false;

    [Header("كائنات المنطقة")]
    [Tooltip("تشتغل داخل المنطقة فقط وتنطفي برّاها — المونستر، الأفخاخ، الأعمدة... " +
             "لا تحط العلم هنا لأنه يسافر مع اللاعب.")]
    [SerializeField] private GameObject[] activeInside;

    [Header("أحداث")]
    [Tooltip("عند دخول اللاعب المنطقة")]
    public UnityEvent onEntered;
    [Tooltip("عند خروجه منها")]
    public UnityEvent onExited;

    /// <summary>هل اللاعب داخل هذه المنطقة الآن؟</summary>
    public bool IsPlayerInside => Current == this;

    private Collider area;
    private Transform player;

    /// <summary>WorldBWController مؤجّل — لأن Instance ما ينضبط إلا في Awake الخاص به.</summary>
    private WorldBWController BW => bw != null ? bw : (bw = WorldBWController.Instance);

    private void Reset()
    {
        // كولايدر المنطقة لازم يكون تريغر وإلا صار جدارًا يوقف اللاعب
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        SetContentsActive(false); // المنطقة تبدأ مطفأة حتى يدخلها اللاعب
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        bool inside = Contains(player.position);

        if (inside && Current != this)
        {
            // انتقال مباشر من منطقة لأخرى بلا مرور بالحالة الطبيعية
            if (Current != null) Current.Exit();
            Current = this;
            Enter();
        }
        else if (!inside && Current == this)
        {
            Current = null;
            Exit();
        }
    }

    /// <summary>هل النقطة داخل حدود المنطقة؟ (يحترم دوران وحجم الكولايدر)</summary>
    public bool Contains(Vector3 point)
    {
        if (area == null) return false;
        // ClosestPoint يرجّع النقطة نفسها إذا كانت داخل الكولايدر
        return (area.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
    }

    private void Enter()
    {
        SetContentsActive(true);
        ApplyBW(true);
        onEntered?.Invoke();
    }

    private void Exit()
    {
        SetContentsActive(false);
        ApplyBW(false);
        onExited?.Invoke();
    }

    private void ApplyBW(bool on)
    {
        var controller = BW;
        if (controller == null) return;

        if (instant) controller.SetInstant(on);
        else controller.SetBlackAndWhite(on);
    }

    private void SetContentsActive(bool on)
    {
        if (activeInside == null) return;
        foreach (var go in activeInside)
            if (go != null) go.SetActive(on);
    }

    private void OnDrawGizmosSelected()
    {
        var c = area != null ? area : GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

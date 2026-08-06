using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// قاعدة العلم في الهب — نقطة الـ Capture: يزرع اللاعب العلم فيها فتنوّر جزيرته
/// ويُفتح باب المرحلة التالية.
///
/// حُطّه على نفس كائن <see cref="FlagSocket"/> (أو اربط المقبس يدويًا). هو يربط
/// نفسه بحدث المقبس تلقائيًا، فما تحتاج تربط شيئًا في الـ Inspector غير الخانات.
///
/// <b>يستعيد نفسه</b>: كل ما رجعت للهب بعد مرحلة ثانية، القواعد المزروعة سابقًا
/// تبدأ ودائرة جزيرتها متمددة وبابها مفتوح — بلا حركة ولا صوت (استعادة لا حدث).
///
/// التسلسل الكامل:
///   قاعدة ستيم  → تنوّر جزيرة ستيم   + تفتح باب التوايلايت
///   قاعدة تويلايت → تنوّر جزيرة تويلايت + تفتح باب السيرك
///   قاعدة السيرك  → تنوّر جزيرة السيرك  + <see cref="onAllFlagsPlanted"/> → نهاية اللعبة
/// </summary>
public class FlagBase : MonoBehaviour
{
    [Header("الهوية")]
    [Tooltip("العلم الذي تقبله هذي القاعدة")]
    [SerializeField] private FlagId flagId = FlagId.Steam;

    [Tooltip("مقبس العلم — يُلتقط تلقائيًا من نفس الكائن إذا تُرك فارغًا")]
    [SerializeField] private FlagSocket socket;

    [Header("دائرة الجزيرة")]
    [Tooltip("مؤثّر الدائرة فوق الجزيرة (InteractorRadiusAnimator على كائن فيه WorldInteractor). " +
             "تبدأ منكمشة ثم تكبر لحظة الزرع. ⚠️ اترك Expand On Start مطفيًا فيها.")]
    [SerializeField] private InteractorRadiusAnimator islandZone;

    [Header("باب المرحلة التالية")]
    [Tooltip("الباب الذي يُفتح عند زرع هذا العلم — اتركه فارغًا في آخر قاعدة")]
    [SerializeField] private Gate doorToOpen;

    [Header("العلم المزروع")]
    [Tooltip("يقفل العلم في القاعدة فلا يُلتقط مرة أخرى")]
    [SerializeField] private bool lockFlagAfterPlant = true;

    [Header("أحداث")]
    [Tooltip("لحظة زرع العلم — الصوت والمؤثرات والاهتزاز")]
    public UnityEvent onPlanted;
    [Tooltip("عند دخول الهب وهذا العلم مزروع من قبل — استعادة صامتة بلا مؤثرات")]
    public UnityEvent onRestored;
    [Tooltip("عند زرع آخر علم — اربطه بـ GameEndingSequence.Play")]
    public UnityEvent onAllFlagsPlanted;

    /// <summary>هوية علم هذي القاعدة.</summary>
    public FlagId Id => flagId;

    /// <summary>هل زُرع علمها؟</summary>
    public bool IsPlanted { get; private set; }

    private void Awake()
    {
        if (socket == null) socket = GetComponent<FlagSocket>();
    }

    private void OnEnable()
    {
        // الربط بالكود بدل الـ Inspector — قاعدة منسيّة الربط كانت ستفشل بصمت
        if (socket != null) socket.onFlagPlacedHere.AddListener(Plant);
    }

    private void OnDisable()
    {
        if (socket != null) socket.onFlagPlacedHere.RemoveListener(Plant);
    }

    private void Start()
    {
        if (socket == null)
            Debug.LogWarning($"[FlagBase] قاعدة {flagId} بلا FlagSocket — ما راح يُزرع فيها " +
                             $"علم أبدًا. حطّ FlagSocket على نفس الكائن أو اربطه.", this);

        if (GameProgress.Instance.IsPlanted(flagId))
        {
            IsPlanted = true;

            // استعادة فورية: لا حركة ولا صوت — اللاعب زرعه في زيارة سابقة
            if (islandZone != null) islandZone.ExpandInstant();
            if (doorToOpen != null) doorToOpen.OpenInstant();
            LockFlag();

            onRestored?.Invoke();
            return;
        }

        // لم يُزرع بعد: الجزيرة مطفأة والباب مقفول (نضمنها بدل الاعتماد على الضبط اليدوي)
        if (islandZone != null) islandZone.ShrinkInstant();
    }

    /// <summary>
    /// يزرع العلم — يُنادى تلقائيًا من حدث المقبس، وتقدر تناديه من أي حدث للاختبار.
    /// </summary>
    public void Plant()
    {
        if (IsPlanted) return;
        IsPlanted = true;

        GameProgress.Instance.PlantFlag(flagId);

        if (islandZone != null) islandZone.Expand();   // تكبر بنعومة — هذي اللحظة
        if (doorToOpen != null) doorToOpen.Open();
        LockFlag();

        onPlanted?.Invoke();

        if (GameProgress.Instance.AllPlanted) onAllFlagsPlanted?.Invoke();
    }

    /// <summary>
    /// اختبار سريع: كليك يمين على اسم المكوّن أثناء اللعب → يزرع العلم فورًا
    /// بلا ما تجيبه من مرحلته. لفحص سلسلة (تنوير الجزيرة + فتح الباب) وحدها.
    /// </summary>
    [ContextMenu("تجربة: ازرع هذا العلم الآن")]
    private void DebugPlant()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FlagBase] التجربة تعمل أثناء Play فقط.", this);
            return;
        }
        Plant();
    }

    private void LockFlag()
    {
        if (!lockFlagAfterPlant || socket == null || socket.Flag == null) return;
        socket.Flag.LockInPlace();
    }
}

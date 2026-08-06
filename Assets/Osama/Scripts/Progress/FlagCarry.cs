using System.Collections;
using UnityEngine;

/// <summary>
/// يجعل العلم "ينتقل مع اللاعب" بين السينات — حُطّه على كائن العلم نفسه
/// (مع <see cref="FlagItem"/> و <see cref="WorldInteractor"/> و <see cref="FlagZoneSize"/>).
///
/// كل سين فيه نسخته من العلم. هذا السكربت يقرأ <see cref="GameProgress"/> عند
/// تحميل السين ويضع النسخة في حالتها الصحيحة فورًا:
///
/// | حالة العلم في الذاكرة | ما يحدث لهذي النسخة |
/// |---|---|
/// | محمول | يلتصق على رأس اللاعب فورًا (كأنه دخل السين وهو حامله) |
/// | مزروع | يقف في <see cref="plantedPoint"/> مقفلًا، وإن لم تُحدَّد يختفي |
/// | لا هذا ولا ذاك | يبقى مكانه (سين مرحلته) أو يختفي (<see cref="IdleBehaviour"/>) |
///
/// <b>الضبط:</b>
///  - في سين المرحلة: نسخة واحدة بـ<c>Stay In Place</c> في مكان أخذ العلم.
///  - في الهب: ثلاث نسخ بـ<c>Hide Until Owned</c>، كل واحدة <c>Planted Point</c> = قاعدتها.
///  - في نسخ الهب أطفئ <c>Return On Holder Death</c> في FlagItem، وإلا رجع العلم
///    لموضعه الأول كل ما مات اللاعب وهو ماشٍ نحو القاعدة.
/// </summary>
[DefaultExecutionOrder(100)] // بعد FlagZoneSize.Start حتى لا يُعاد ضبط نصف القطر فوق حركته
[RequireComponent(typeof(FlagItem))]
public class FlagCarry : MonoBehaviour
{
    public enum IdleBehaviour
    {
        [InspectorName("يبقى مكانه (سين مرحلته)")]
        StayInPlace,
        [InspectorName("يختفي حتى يملكه اللاعب (نسخ الهب)")]
        HideUntilOwned,
    }

    [Header("الهوية")]
    [Tooltip("أي علم من الثلاثة هو هذا؟ كل نسخ العلم الواحد في كل السينات تأخذ نفس الهوية.")]
    [SerializeField] private FlagId flagId = FlagId.Steam;

    [Header("قبل أن يملكه اللاعب")]
    [Tooltip("سلوك النسخة إذا لم تكن محمولة ولا مزروعة")]
    [SerializeField] private IdleBehaviour whenNotOwned = IdleBehaviour.StayInPlace;

    [Header("بعد الزرع")]
    [Tooltip("موضع العلم وهو مزروع (نقطة القاعدة في الهب). " +
             "اتركها فارغة في سينات المراحل ليختفي العلم بعد أن يُزرع في الهب.")]
    [SerializeField] private Transform plantedPoint;

    [Header("اللاعب")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("مدة انتظار ظهور اللاعب في السين قبل الاستسلام (ثواني)")]
    [SerializeField] private float playerSearchTimeout = 2f;

    /// <summary>هوية هذا العلم.</summary>
    public FlagId Id => flagId;

    private FlagItem flag;
    private GameProgress progress;

    private void Awake()
    {
        flag = GetComponent<FlagItem>();
    }

    private void OnEnable()
    {
        if (flag == null) return;
        flag.PickedUp += HandlePickedUp;
        flag.Placed += HandlePlaced;
    }

    private void OnDisable()
    {
        if (flag == null) return;
        flag.PickedUp -= HandlePickedUp;
        flag.Placed -= HandlePlaced;
    }

    private void Start()
    {
        progress = GameProgress.Instance;

        // ١) مزروع أصلًا — يقف في قاعدته مقفلًا، أو يختفي إن كنّا في سين آخر
        if (progress.IsPlanted(flagId))
        {
            if (plantedPoint == null) { gameObject.SetActive(false); return; }

            transform.SetParent(null);
            transform.SetPositionAndRotation(plantedPoint.position, plantedPoint.rotation);
            flag.LockInPlace();
            return;
        }

        // ٢) محمول — يدخل السين وهو على رأس اللاعب
        if (progress.IsCarrying(flagId))
        {
            StartCoroutine(AttachWhenPlayerExists());
            return;
        }

        // ٣) لا محمول ولا مزروع
        if (whenNotOwned == IdleBehaviour.HideUntilOwned) gameObject.SetActive(false);
    }

    /// <summary>
    /// اللاعب قد لا يكون موجودًا في أول إطار (يُولَّد أو يُنقل بواسطة
    /// <see cref="PlayerSpawnRouter"/>)، فنبحث عنه عدة إطارات بدل مرة واحدة.
    /// </summary>
    private IEnumerator AttachWhenPlayerExists()
    {
        float t = 0f;
        while (t < playerSearchTimeout)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null)
            {
                flag.AttachTo(go.transform);
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[FlagCarry] العلم {flagId} محمول في الذاكرة لكن ما وُجد كائن " +
                         $"بوسم \"{playerTag}\" في السين — بقي العلم في مكانه.", this);
    }

    private void HandlePickedUp()
    {
        if (progress == null) progress = GameProgress.Instance;
        progress.CarryFlag(flagId);
    }

    private void HandlePlaced()
    {
        // يشمل الزرع في القاعدة والرجوع التلقائي عند الموت — في الحالتين
        // اللاعب لم يعد حاملًا. FlagBase هو من يسجّل "مزروع".
        if (progress == null) progress = GameProgress.Instance;
        progress.ClearCarried();
    }
}

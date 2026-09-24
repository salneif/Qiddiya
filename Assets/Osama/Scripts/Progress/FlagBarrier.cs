using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// حاجز غير مرئي يقفل مدخلًا حتى يُزرع علم معيّن — لباب يبقى مفتوحًا بشكله، لكن
/// اللاعب لا يعبره قبل أن يستحق ذلك.
///
/// يقرأ <see cref="GameProgress"/> بنفسه: يفتح لحظة الزرع، ويبقى مفتوحًا في كل
/// زيارة قادمة للهب بلا صوت ولا حركة (استعادة لا حدث). فلا تحتاج ربط أي شيء
/// بأحداث القاعدة.
///
/// التركيب: كائن فارغ عند المدخل + Collider (ليس Trigger) يسدّه + هذا السكربت.
/// بلا Mesh Renderer فيكون غير مرئي. حدّد <see cref="requirePlantedFlag"/> وكفى.
/// </summary>
public class FlagBarrier : MonoBehaviour
{
    [Header("الشرط")]
    [Tooltip("الحاجز يزول عندما يُزرع هذا العلم في الهب")]
    [SerializeField] private FlagId requirePlantedFlag = FlagId.Twilight;

    [Header("ما الذي يُزال؟")]
    [Tooltip("الكائنات التي تُطفأ عند الفتح (الكولايدر المانع، مؤثر، لافتة...). " +
             "اتركها فارغة لتُطفأ كولايدرات هذا الكائن نفسه.")]
    [SerializeField] private GameObject[] blockers;
    [Tooltip("كائنات تُشغَّل لحظة الفتح وتبقى شغّالة في كل زيارة بعدها — الأنميشن " +
             "والأضواء والمؤثرات التي تولع عند زرع العلم. أطفئها في المحرر لتبدأ مخفيّة.")]
    [SerializeField] private GameObject[] showWhenOpen;

    [Header("تلميح للاعب وهو مقفول")]
    [Tooltip("مصدر الصوت — يُلتقط تلقائيًا من نفس الكائن")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت لحظة الفتح (زُرع العلم فانفتح الطريق)")]
    [SerializeField] private AudioClip unblockSound;
    [Tooltip("صوت عند اقتراب اللاعب وهو ما زال مقفولًا — قعقعة قفل أو همهمة")]
    [SerializeField] private AudioClip blockedSound;
    [Tooltip("مسافة تشغيل صوت القفل (متر). صفر = بلا تلميح")]
    [SerializeField] private float hintDistance = 3f;
    [SerializeField] private string playerTag = "Player";

    [Header("أحداث")]
    [Tooltip("لحظة الفتح فعليًا — صوت، مؤثر، لمعان (HintGlow.StartHint)")]
    public UnityEvent onUnblocked;
    [Tooltip("دخلتَ الهب والعلم مزروع من زيارة سابقة — استعادة صامتة")]
    public UnityEvent onRestoredOpen;
    [Tooltip("اقترب اللاعب وهو مقفول — أظهر تلميحًا \"تحتاج العلم\"")]
    public UnityEvent onBlockedApproach;

    /// <summary>هل زال الحاجز؟</summary>
    public bool IsOpen { get; private set; }

    private Transform player;
    private bool playerNear;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable() => GameProgress.Changed += Refresh;

    private void OnDisable() => GameProgress.Changed -= Refresh;

    private void Start()
    {
        bool planted = GameProgress.Instance.IsPlanted(requirePlantedFlag);
        IsOpen = planted;
        SetBlockersActive(!planted);
        SetRevealedActive(planted);

        if (planted) onRestoredOpen?.Invoke();
    }

    /// <summary>يُنادى مع أي تغيّر في التقدّم — فيفتح لحظة الزرع بلا ربط أحداث.</summary>
    private void Refresh()
    {
        if (IsOpen || !GameProgress.Instance.IsPlanted(requirePlantedFlag)) return;

        IsOpen = true;
        SetBlockersActive(false);
        SetRevealedActive(true);

        if (unblockSound != null && audioSource != null) audioSource.PlayOneShot(unblockSound);
        onUnblocked?.Invoke();
    }

    private void Update()
    {
        if (IsOpen || hintDistance <= 0f) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        bool near = Vector3.Distance(player.position, transform.position) <= hintDistance;

        // مرة واحدة لكل اقتراب، لا كل إطار
        if (near && !playerNear)
        {
            if (blockedSound != null && audioSource != null) audioSource.PlayOneShot(blockedSound);
            onBlockedApproach?.Invoke();
        }
        playerNear = near;
    }

    /// <summary>يشغّل ما يُكشف عند الفتح — الأنميشن والأضواء المستقلة عن الباب.</summary>
    private void SetRevealedActive(bool value)
    {
        if (showWhenOpen == null) return;
        foreach (var go in showWhenOpen)
            if (go != null) go.SetActive(value);
    }

    private void SetBlockersActive(bool value)
    {
        if (blockers != null && blockers.Length > 0)
        {
            foreach (var go in blockers)
                if (go != null) go.SetActive(value);
            return;
        }

        // بلا قائمة: نطفئ كولايدرات هذا الكائن وأبنائه فقط، فيبقى السكربت شغّالًا
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = value;
    }

    private void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.color = Application.isPlaying && IsOpen
                ? new Color(0.3f, 1f, 0.4f, 0.35f)
                : new Color(1f, 0.35f, 0.3f, 0.35f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }

        if (hintDistance <= 0f) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, hintDistance);
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// بوابة تنفتح/تنغلق بنعومة — بالانزلاق أو بالدوران أو بالاثنين معًا.
/// تُفتح عبر Open() (اربطها بحدث الرافعة أو <see cref="FlagBase"/>).
///
/// <b>باب يدور على مفصلة:</b> الدوران يكون حول <b>مركز هذا الكائن</b>. فلو حطيت
/// السكربت على مجسّم الباب نفسه دار حول منتصفه كأنه يسبح. الصح:
///  1. كائن فارغ عند <b>حافة المفصلة</b>.
///  2. مجسّم الباب <b>ابنًا</b> له.
///  3. `Gate` على الكائن الفارغ لا على المجسّم.
/// </summary>
public class Gate : MonoBehaviour
{
    [Header("الحركة — انزلاق")]
    [Tooltip("إزاحة البوابة عند الفتح بفضاء العالم (مثلاً لأعلى Y = 4). صفر = بلا انزلاق.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
    [Tooltip("سرعة الانزلاق (متر/ثانية)")]
    [SerializeField] private float speed = 3f;

    [Header("الحركة — دوران")]
    [Tooltip("مقدار دوران الباب عند الفتح بالدرجات حول محاوره المحلية. " +
             "باب عادي يفتح للجانب: Y = 90 أو -90. صفر = بلا دوران.")]
    [SerializeField] private Vector3 openRotation = Vector3.zero;
    [Tooltip("سرعة الدوران (درجة/ثانية)")]
    [SerializeField] private float rotationSpeed = 120f;
    [Tooltip("تبدأ مفتوحة؟")]
    [SerializeField] private bool startOpen = false;

    [Header("الصوت")]
    [Tooltip("مصدر الصوت — يُلتقط تلقائيًا من نفس الكائن إذا تُرك فارغًا")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت لحظة بدء الفتح (صرير، ارتطام حجر...)")]
    [SerializeField] private AudioClip openSound;
    [Tooltip("صوت لحظة بدء الإغلاق")]
    [SerializeField] private AudioClip closeSound;
    [Tooltip("صوت متكرّر طوال الحركة، يسكت لحظة الوصول (احتكاك، سلاسل...). " +
             "⚠️ لو استخدمته خلّ صوت الفتح قصيرًا — سكوت الحركة يقطع ما قبله على نفس المصدر.")]
    [SerializeField] private AudioClip movingLoop;

    [Header("أحداث")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Vector3 closedPos, openPos;
    private Quaternion closedRot, openRot;
    private bool isOpen;
    private bool wasOpen;
    private bool initialized;
    private bool loopPlaying;

    public bool IsOpen => isOpen;

    private void Awake() => Initialize();

    /// <summary>
    /// حساب الموضعين. مفصولة عن Awake لأن <see cref="OpenInstant"/> قد تُنادى من
    /// Awake سكربت آخر — وترتيب الـ Awake غير مضمون، فبدونها كان openPos = صفر.
    /// </summary>
    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        closedPos = transform.position;
        openPos = closedPos + openOffset;

        closedRot = transform.rotation;
        openRot = closedRot * Quaternion.Euler(openRotation);

        isOpen = wasOpen = startOpen;
        transform.SetPositionAndRotation(startOpen ? openPos : closedPos,
                                         startOpen ? openRot : closedRot);
    }

    /// <summary>
    /// يفتحها فورًا بلا حركة ولا حدث — لاستعادة باب فُتح في زيارة سابقة
    /// (يستخدمها <see cref="FlagBase"/> عند العودة للهب).
    /// </summary>
    public void OpenInstant()
    {
        Initialize();
        isOpen = wasOpen = true;
        transform.SetPositionAndRotation(openPos, openRot);
    }

    private void Update()
    {
        Vector3 targetPos = isOpen ? openPos : closedPos;
        Quaternion targetRot = isOpen ? openRot : closedRot;

        transform.position = Vector3.MoveTowards(transform.position, targetPos,
                                                 speed * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
                                                      rotationSpeed * Time.deltaTime);

        // == على الكواتيرنيون مقارنة تقريبية، فنفحص الزاوية صراحةً
        bool arrived = transform.position == targetPos &&
                       Quaternion.Angle(transform.rotation, targetRot) < 0.01f;
        UpdateMovingSound(moving: !arrived);

        // إطلاق حدث الوصول مرة واحدة
        if (isOpen != wasOpen && arrived)
        {
            wasOpen = isOpen;
            if (isOpen) onOpened?.Invoke(); else onClosed?.Invoke();
        }
    }

    public void Open()
    {
        if (isOpen) return;   // بلا هذا يتكرر صوت الفتح كل مرة يُنادى الحدث
        isOpen = true;
        Play(openSound);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Play(closeSound);
    }

    public void Toggle()
    {
        if (isOpen) Close(); else Open();
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private void UpdateMovingSound(bool moving)
    {
        if (movingLoop == null || audioSource == null) return;

        if (moving && !loopPlaying)
        {
            audioSource.clip = movingLoop;
            audioSource.loop = true;
            audioSource.Play();
            loopPlaying = true;
        }
        else if (!moving && loopPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
            loopPlaying = false;
        }
    }

    /// <summary>
    /// يرسم موضع الباب وهو مفتوح — عشان تتأكد من اتجاه الفتح ومقداره
    /// <b>بلا ما تضغط Play</b>. الأصفر = مسار الحركة، الأخضر = مكان الباب مفتوحًا.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // في وضع اللعب نرسم من الوضع المغلق الأصلي لا من موضعه الحالي
        Vector3 basePos = Application.isPlaying ? closedPos : transform.position;
        Quaternion baseRot = Application.isPlaying ? closedRot : transform.rotation;

        Vector3 targetPos = basePos + openOffset;
        Quaternion targetRot = baseRot * Quaternion.Euler(openRotation);

        // الأصفر: نقطة الارتكاز (المفصلة) ومسار الانزلاق
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(basePos, 0.15f);
        if (openOffset != Vector3.zero) Gizmos.DrawLine(basePos, targetPos);

        // الأخضر: شكل الباب في وضع الفتح — بموضعه ودورانه معًا
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(targetPos, targetRot, transform.lossyScale) *
                        (transform.worldToLocalMatrix * rend.transform.localToWorldMatrix);
        Gizmos.DrawWireCube(rend.localBounds.center, rend.localBounds.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

#if UNITY_EDITOR
    /// <summary>
    /// معاينة في المحرر: كليك يمين على اسم المكوّن → "معاينة: افتح".
    /// تحرّك الباب فعليًا (مع دعم Ctrl+Z)، فارجعه بـ"معاينة: أغلق" قبل الحفظ —
    /// الموضع الذي تتركه فيه هو الموضع المغلق الذي يحفظه Awake.
    /// </summary>
    [ContextMenu("معاينة: افتح")]
    private void PreviewOpen()
    {
        if (Application.isPlaying) { Open(); return; }
        UnityEditor.Undo.RecordObject(transform, "Gate Preview Open");
        transform.position += openOffset;
        transform.rotation *= Quaternion.Euler(openRotation);
    }

    [ContextMenu("معاينة: أغلق")]
    private void PreviewClose()
    {
        if (Application.isPlaying) { Close(); return; }
        UnityEditor.Undo.RecordObject(transform, "Gate Preview Close");
        transform.rotation *= Quaternion.Inverse(Quaternion.Euler(openRotation));
        transform.position -= openOffset;
    }
#endif
}

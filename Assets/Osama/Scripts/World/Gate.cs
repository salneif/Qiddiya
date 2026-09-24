using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// بوابة تنفتح/تنغلق بنعومة — بالانزلاق أو بالدوران أو بالاثنين معًا.
/// تُفتح عبر Open() (اربطها بحدث الرافعة أو <see cref="FlagBase"/>).
///
/// <b>باب يدور على مفصلة:</b> اختر <see cref="hinge"/> = الحافة اليسرى أو اليمنى،
/// فيدور الباب حول حافته مثل باب حقيقي ولو كان السكربت على مجسّم الباب نفسه.
/// الحافة تُحسب تلقائيًا من حجم المجسّم، والخط الأصفر في نافذة Scene يوريك مكانها.
///
/// على "الوسط" (الافتراضي) يدور حول مركز الكائن كما كان سابقًا — فالأبواب القديمة
/// لا يتغيّر فيها شيء.
/// </summary>
public class Gate : MonoBehaviour
{
    /// <summary>حول أي خط يدور الباب.</summary>
    public enum Hinge
    {
        [InspectorName("الوسط (يلف حول مركزه)")] Center,
        [InspectorName("الحافة اليسرى")] Left,
        [InspectorName("الحافة اليمنى")] Right
    }

    [Header("الحركة — انزلاق")]
    [Tooltip("إزاحة البوابة عند الفتح بفضاء العالم (مثلاً لأعلى Y = 4). صفر = بلا انزلاق.")]
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);
    [Tooltip("سرعة الانزلاق (متر/ثانية)")]
    [SerializeField] private float speed = 3f;

    [Header("الحركة — دوران")]
    [Tooltip("مقدار دوران الباب عند الفتح بالدرجات حول محاوره المحلية. " +
             "باب عادي يفتح للجانب: 90 أو -90 على محوره العمودي. صفر = بلا دوران.")]
    [SerializeField] private Vector3 openRotation = Vector3.zero;
    [Tooltip("سرعة الدوران (درجة/ثانية)")]
    [SerializeField] private float rotationSpeed = 120f;
    [Tooltip("المفصلة: الوسط = يلف حول مركزه. الحافة = يدور حول طرفه مثل باب حقيقي، " +
             "بحركة تتسارع وتتباطأ بنعومة. الحافة تُحسب من حجم المجسّم تلقائيًا — " +
             "لو طلعت المفصلة على الطرف الغلط (الخط الأصفر) اختر الحافة الثانية، " +
             "ولو انفتح للجهة الغلط اعكس إشارة الدوران.")]
    [SerializeField] private Hinge hinge = Hinge.Center;
    [Tooltip("تبدأ مفتوحة؟")]
    [SerializeField] private bool startOpen = false;

    [Header("الفتح عند الاقتراب")]
    [Tooltip("تنفتح وحدها لما يقرب اللاعب من هذي المسافة (متر). صفر = لا تفتح إلا بالأحداث.")]
    [SerializeField] private float openWhenPlayerWithin = 0f;
    [Tooltip("ترجع تنغلق لما يبتعد اللاعب. أطفئه لتبقى مفتوحة بعد أول مرة.")]
    [SerializeField] private bool closeWhenPlayerLeaves = true;
    [Tooltip("مسافة إضافية قبل الإغلاق (متر) — تمنع فتحًا وإغلاقًا متكررين على الحد")]
    [SerializeField] private float closeMargin = 0.75f;
    [Tooltip("نقطة قياس المسافة — اتركها فارغة ليُقاس من هذا الكائن. " +
             "لبابين يفتحان معًا (فتحة بنصفين): اربط الاثنين بنفس النقطة في مركزها.")]
    [SerializeField] private Transform measureFrom;
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

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
    [Tooltip("مدة الحركة = طول مقطع الصوت بالضبط، فتصل البوابة مع آخر لحظة منه. " +
             "يتجاهل Speed و Rotation Speed ما دام للاتجاه مقطعٌ. " +
             "صوت الفتح يضبط زمن الفتح، وصوت الإغلاق يضبط زمن الإغلاق.")]
    [SerializeField] private bool matchMovementToSound = false;

    [Header("أحداث")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Vector3 closedPos, openPos;
    private Quaternion closedRot, openRot;
    private bool isOpen;
    private bool wasOpen;
    private bool initialized;
    private bool loopPlaying;
    private Transform player;

    private float totalSlide;     // مسافة الانزلاق الكاملة
    private float totalAngle;     // زاوية الدوران الكاملة

    // وضع المفصلة
    private bool usesHinge;
    private float rotAngle;       // زاوية الفتح الكاملة (درجات)
    private Vector3 rotAxis;      // محور الدوران بفضاء الباب المحلي
    private Vector3 hingeWorld;   // نقطة على خط المفصلة وهو مغلق
    private float slideT, rotT;   // تقدّم الانزلاق والدوران (0 مغلق، 1 مفتوح)

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

        // بلا مصدر صوت ما يُسمع شيء — ننشئه تلقائيًا إذا حطيت مقطعًا.
        // ثنائي الأبعاد عمدًا: كاميرا اللعبة بعيدة، والصوت ثلاثي الأبعاد معها لا يُسمع (خطأ ١١).
        if (audioSource == null && (openSound != null || closeSound != null || movingLoop != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        closedPos = transform.position;
        closedRot = transform.rotation;
        openRot = closedRot * Quaternion.Euler(openRotation);

        totalSlide = openOffset.magnitude;
        totalAngle = Quaternion.Angle(closedRot, openRot);

        usesHinge = TryGetHingeAxis(out rotAngle, out rotAxis);
        if (usesHinge)
        {
            hingeWorld = transform.TransformPoint(HingeLocalPoint(rotAxis));
            openPos = PoseAt(1f, 1f, out _);
        }
        else
        {
            openPos = closedPos + openOffset;
        }

        isOpen = wasOpen = startOpen;
        slideT = rotT = startOpen ? 1f : 0f;
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
        slideT = rotT = 1f;
        transform.SetPositionAndRotation(openPos, openRot);
    }

    private void Update()
    {
        UpdateProximity();

        bool arrived = usesHinge ? StepHinge() : StepLinear();
        UpdateMovingSound(moving: !arrived);

        // إطلاق حدث الوصول مرة واحدة
        if (isOpen != wasOpen && arrived)
        {
            wasOpen = isOpen;
            if (isOpen) onOpened?.Invoke(); else onClosed?.Invoke();
        }
    }

    /// <summary>
    /// تفتح وحدها عند اقتراب اللاعب وتنغلق عند ابتعاده. الإغلاق له هامش إضافي،
    /// فلا تتأرجح البوابة فتحًا وإغلاقًا واللاعب واقف على حد المسافة.
    /// </summary>
    private void UpdateProximity()
    {
        if (openWhenPlayerWithin <= 0f) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        Transform origin = measureFrom != null ? measureFrom : transform;
        float dist = Vector3.Distance(player.position, origin.position);

        if (dist <= openWhenPlayerWithin) Open();
        else if (closeWhenPlayerLeaves && dist > openWhenPlayerWithin + closeMargin) Close();
    }

    /// <summary>
    /// مدة الحركة المطلوبة للاتجاه الحالي (ثواني)، أو صفر إن لم نربطها بالصوت.
    /// </summary>
    private float SoundDuration()
    {
        if (!matchMovementToSound) return 0f;

        AudioClip clip = isOpen ? openSound : closeSound;
        return clip != null ? clip.length : 0f;
    }

    /// <summary>سرعة الانزلاق الآن — محسوبة من طول الصوت إن رُبطت به.</summary>
    private float SlideSpeedNow()
    {
        float d = SoundDuration();
        return d > 0.01f && totalSlide > 0.0001f ? totalSlide / d : speed;
    }

    /// <summary>سرعة الدوران الآن — محسوبة من طول الصوت إن رُبطت به.</summary>
    private float TurnSpeedNow()
    {
        float d = SoundDuration();
        float angle = usesHinge ? rotAngle : totalAngle;
        return d > 0.01f && angle > 0.01f ? angle / d : rotationSpeed;
    }

    /// <summary>الحركة القديمة: انزلاق ودوران حول المركز كلٌّ بسرعته.</summary>
    private bool StepLinear()
    {
        Vector3 targetPos = isOpen ? openPos : closedPos;
        Quaternion targetRot = isOpen ? openRot : closedRot;

        transform.position = Vector3.MoveTowards(transform.position, targetPos,
                                                 SlideSpeedNow() * Time.deltaTime);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
                                                      TurnSpeedNow() * Time.deltaTime);

        // == على الكواتيرنيون مقارنة تقريبية، فنفحص الزاوية صراحةً
        return transform.position == targetPos &&
               Quaternion.Angle(transform.rotation, targetRot) < 0.01f;
    }

    /// <summary>
    /// حركة المفصلة: الدوران حول حافة الباب لا مركزه. الموضع يُحسب من التقدّم
    /// لا بالتحريك التدريجي، فتبقى المفصلة ثابتة في مكانها طوال الحركة.
    /// </summary>
    private bool StepHinge()
    {
        float target = isOpen ? 1f : 0f;

        slideT = totalSlide < 0.0001f ? target
                                      : Mathf.MoveTowards(slideT, target,
                                                          SlideSpeedNow() / totalSlide * Time.deltaTime);
        rotT = Mathf.MoveTowards(rotT, target, TurnSpeedNow() / rotAngle * Time.deltaTime);

        Vector3 pos = PoseAt(slideT, rotT, out Quaternion rot);
        transform.SetPositionAndRotation(pos, rot);

        return Mathf.Approximately(slideT, target) && Mathf.Approximately(rotT, target);
    }

    /// <summary>وضع الباب عند تقدّم معيّن — يتسارع ويتباطأ بنعومة مثل باب ثقيل.</summary>
    private Vector3 PoseAt(float slide, float turn, out Quaternion rot)
    {
        float easedTurn = Mathf.SmoothStep(0f, 1f, turn);
        float easedSlide = Mathf.SmoothStep(0f, 1f, slide);

        rot = closedRot * Quaternion.AngleAxis(rotAngle * easedTurn, rotAxis);
        Quaternion delta = rot * Quaternion.Inverse(closedRot);
        return hingeWorld + delta * (closedPos - hingeWorld) + openOffset * easedSlide;
    }

    /// <summary>هل الباب يدور على حافة؟ ويعطي زاوية الفتح ومحورها المحلي.</summary>
    private bool TryGetHingeAxis(out float angle, out Vector3 axis)
    {
        Quaternion.Euler(openRotation).ToAngleAxis(out angle, out axis);
        return hinge != Hinge.Center && angle > 0.01f && axis.sqrMagnitude > 0.5f;
    }

    /// <summary>
    /// نقطة على خط المفصلة بفضاء الباب المحلي: من بين المحاور العمودية على محور
    /// الدوران نختار أعرضها (عرض الباب لا سُمكه)، ونأخذ طرفه الأيسر أو الأيمن.
    /// </summary>
    private Vector3 HingeLocalPoint(Vector3 axis)
    {
        if (!TryGetLocalBounds(out Bounds b)) return Vector3.zero;

        int widthAxis = 0;
        float best = -1f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 e = Vector3.zero;
            e[i] = 1f;
            float score = b.size[i] * (1f - Mathf.Abs(Vector3.Dot(e, axis)));
            if (score > best) { best = score; widthAxis = i; }
        }

        Vector3 p = b.center;
        p[widthAxis] += (hinge == Hinge.Left ? -0.5f : 0.5f) * b.size[widthAxis];
        return p;
    }

    /// <summary>حدود مجسّمات الباب بفضاء هذا الكائن المحلي (لا تتأثر بدورانه).</summary>
    private bool TryGetLocalBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        Matrix4x4 toLocal = transform.worldToLocalMatrix;

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Bounds lb = r.localBounds;
            Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = m.MultiplyPoint3x4(corner);

                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                else bounds.Encapsulate(p);
            }
        }
        return any;
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
    /// <b>بلا ما تضغط Play</b>. الأصفر = المفصلة ومسار الانزلاق، الأخضر = مكان الباب مفتوحًا.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // مدى الفتح التلقائي
        if (openWhenPlayerWithin > 0f)
        {
            Transform origin = measureFrom != null ? measureFrom : transform;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(origin.position, openWhenPlayerWithin);
        }

        // في وضع اللعب نرسم من الوضع المغلق الأصلي لا من موضعه الحالي
        bool useStored = Application.isPlaying && initialized;
        Vector3 basePos = useStored ? closedPos : transform.position;
        Quaternion baseRot = useStored ? closedRot : transform.rotation;

        Quaternion q = Quaternion.Euler(openRotation);
        Quaternion targetRot = baseRot * q;
        Vector3 pivot = basePos;
        Vector3 targetPos = basePos + openOffset;

        bool hinged = TryGetHingeAxis(out _, out Vector3 axis);
        if (hinged)
        {
            pivot = useStored && usesHinge ? hingeWorld : transform.TransformPoint(HingeLocalPoint(axis));
            targetPos = pivot + (targetRot * Quaternion.Inverse(baseRot)) * (basePos - pivot) + openOffset;
        }

        var rend = GetComponentInChildren<Renderer>();

        // الأصفر: نقطة الارتكاز (المفصلة) ومسار الانزلاق
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(pivot, 0.15f);
        if (hinged)
        {
            float len = rend != null ? rend.bounds.size.magnitude * 0.5f : 1f;
            Vector3 dir = baseRot * axis;
            Gizmos.DrawLine(pivot - dir * len, pivot + dir * len);
        }
        if (openOffset != Vector3.zero) Gizmos.DrawLine(pivot, pivot + openOffset);

        // الأخضر: شكل الباب في وضع الفتح — بموضعه ودورانه معًا
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

        Quaternion q = Quaternion.Euler(openRotation);
        if (TryGetHingeAxis(out _, out Vector3 axis))
        {
            // دوران حول المفصلة ثم الانزلاق
            Vector3 p = transform.TransformPoint(HingeLocalPoint(axis));
            Quaternion delta = transform.rotation * q * Quaternion.Inverse(transform.rotation);
            transform.SetPositionAndRotation(p + delta * (transform.position - p) + openOffset,
                                             delta * transform.rotation);
            return;
        }

        transform.position += openOffset;
        transform.rotation *= q;
    }

    [ContextMenu("معاينة: أغلق")]
    private void PreviewClose()
    {
        if (Application.isPlaying) { Close(); return; }
        UnityEditor.Undo.RecordObject(transform, "Gate Preview Close");

        Quaternion q = Quaternion.Euler(openRotation);
        if (TryGetHingeAxis(out _, out Vector3 axis))
        {
            // المفصلة تتحرك مع الانزلاق فقط: نرجع الانزلاق ثم ندور عكسيًا حولها
            Vector3 p = transform.TransformPoint(HingeLocalPoint(axis)) - openOffset;
            Vector3 pos = transform.position - openOffset;
            Quaternion delta = transform.rotation * Quaternion.Inverse(q) * Quaternion.Inverse(transform.rotation);
            transform.SetPositionAndRotation(p + delta * (pos - p), delta * transform.rotation);
            return;
        }

        transform.rotation *= Quaternion.Inverse(q);
        transform.position -= openOffset;
    }
#endif
}

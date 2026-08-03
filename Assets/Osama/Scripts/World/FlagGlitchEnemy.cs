using UnityEngine;

/// <summary>
/// عدو مربوط بحالة العلم بأسلوب Control (Hiss المشوّه):
///  - العلم في الأرض (غير محمول) → الشكل الصلب، يطارد اللاعب ويقتله بالتلامس.
///  - اللاعب حامل العلم → يتحوّل لشكل مشوّه/شبحي (VFX، غير مؤذٍ) ويتراجع مبتعدًا.
/// يتبدّل تلقائيًا مع أحداث FlagItem (PickedUp / Placed).
///
/// حركة مباشرة بسيطة (بدون NavMesh) مناسبة للانزلاق الشبحي؛ يمكن الترقية لـ
/// NavMeshAgent لاحقًا للتنقّل حول العوائق.
/// </summary>
public class FlagGlitchEnemy : MonoBehaviour
{
    public enum State { Hunting, Retreating }

    [Header("الربط بالعلم")]
    [SerializeField] private FlagItem flag;
    [Tooltip("يعكس علاقته بالعلم: يطارد وأنت <b>حامله</b> ويهدأ لما تتركه. " +
             "لمشاهد الهروب — تأخذ العلم فيستيقظ ويلاحقك حتى المخرج.")]
    [SerializeField] private bool huntsWhenFlagHeld = false;

    [Header("الهدف")]
    [SerializeField] private string playerTag = "Player";

    [Header("الحركة")]
    [Tooltip("سرعة المطاردة (العلم بالأرض)")]
    [SerializeField] private float huntSpeed = 2.5f;
    [Tooltip("سرعة التراجع (اللاعب حامل العلم)")]
    [SerializeField] private float retreatSpeed = 4f;
    [Tooltip("المسافة التي يبتعدها ثم يقف عندها أثناء التراجع (متر)")]
    [SerializeField] private float retreatDistance = 18f;
    [Tooltip("مسافة التلامس التي يقتل عندها اللاعب (متر)")]
    [SerializeField] private float contactRange = 1.3f;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("تثبيت ارتفاع العدو على مستوى ثابت (يمنعه من الانجراف رأسيًا)")]
    [SerializeField] private bool lockToGroundY = true;

    [Header("التحليق (شبح)")]
    [Tooltip("ارتفاع التحليق فوق مستوى البداية (متر). 0 = يمشي على الأرض.")]
    [SerializeField] private float hoverHeight = 0f;
    [Tooltip("مدى التمايل الرأسي أثناء التحليق — يمنع الثبات الآلي فيبدو طافيًا حيًّا")]
    [SerializeField] private float bobAmount = 0.25f;
    [Tooltip("سرعة التمايل")]
    [SerializeField] private float bobSpeed = 1.4f;
    [Tooltip("يتبع ارتفاع اللاعب فيطير لأي مكان — يصعد للمنصّات وينزل للحفر. " +
             "أطفئه ليبقى حبيس مستوى واحد.")]
    [SerializeField] private bool followPlayerHeight = true;
    [Tooltip("سرعة الصعود والهبوط (متر/ثانية) — أبطأ من سرعة المطاردة ليبدو منزلقًا لا قافزًا")]
    [SerializeField] private float verticalSpeed = 2f;
    [Tooltip("يتجاهل فروق الارتفاع الأصغر من هذه (متر). قفزة اللاعب ~1.2م فلا تحرّكه، " +
             "أما نزوله لطابق أو حفرة فيتبعه. بدونها يهتز الشبح مع كل قفزة.")]
    [SerializeField] private float verticalDeadzone = 1.8f;

    [Header("الاختفاء والظهور (Blink) — طابع الرعب")]
    [Tooltip("يومض (يختفي ويقفز) بدل المشي المستمر أثناء المطاردة")]
    [SerializeField] private bool blinkMovement = true;
    [Tooltip("مسافة القفزة الواحدة نحو اللاعب (متر)")]
    [SerializeField] private float blinkStep = 3f;
    [Tooltip("فترة الانتظار الظاهر قبل كل ومضة (ثواني)")]
    [SerializeField] private float blinkInterval = 1.2f;
    [Tooltip("مدة الاختفاء أثناء الومضة (ثواني)")]
    [SerializeField] private float blinkHidden = 0.25f;
    [Tooltip("VFX يظهر لحظة الاختفاء ولحظة الظهور (اختياري)")]
    [SerializeField] private GameObject blinkVfx;
    [Tooltip("صوت الومضة (اختياري)")]
    [SerializeField] private AudioSource blinkAudio;
    [SerializeField] private AudioClip blinkSound;

    [Header("صوت العدو")]
    [Tooltip("مصدر مستقل للأنين — لا تستخدم نفس مصدر الومضة، لأن مستوى الحلقة " +
             "يتغيّر كل إطار وسيبتلع صوت الومضة معه")]
    [SerializeField] private AudioSource voiceSource;
    [Tooltip("أنين/همس مستمر — يعلو كلما اقترب، فيعرف اللاعب قربه بأذنه قبل عينه")]
    [SerializeField] private AudioClip ambientLoop;
    [Range(0f, 1f)]
    [SerializeField] private float ambientMaxVolume = 0.85f;
    [Tooltip("المسافة التي يبدأ الأنين يُسمع عندها (متر)")]
    [SerializeField] private float hearingRange = 22f;
    [Tooltip("صرخة لحظة بدء المطاردة — تُشغَّل مرة واحدة")]
    [SerializeField] private AudioClip huntStartSound;

    [Header("الأنميشن")]
    [SerializeField] private Animator animator;
    [Tooltip("اسم بارامتر Float للسرعة في الأنيميتور (0 = وقوف، >0 = مشي)")]
    [SerializeField] private string speedParam = "Speed";

    [Header("قلتش المشية (حالة الشبح)")]
    [Tooltip("في حالة الشبح، المشية تشتغل لكن متقطّعة كأنها تشويه فيديو")]
    [SerializeField] private bool glitchAnimation = true;
    [Tooltip("مدة تشغيل الأنميشن قبل التجمّد (عشوائي بين X و Y ثانية)")]
    [SerializeField] private Vector2 glitchOnTime = new Vector2(0.05f, 0.16f);
    [Tooltip("مدة التجمّد (عشوائي بين X و Y ثانية)")]
    [SerializeField] private Vector2 glitchFreezeTime = new Vector2(0.03f, 0.12f);
    [Tooltip("اهتزاز مكاني بصري عند كل قفزة قلتش (متر)")]
    [SerializeField] private float glitchJitter = 0.08f;

    [Header("الأشكال البصرية")]
    [Tooltip("الشكل الصلب (يطارد ويؤذي)")]
    [SerializeField] private GameObject solidForm;
    [Tooltip("الشكل المشوّه/الشبح (متراجع، غير مؤذٍ، بس باين فيه شي)")]
    [SerializeField] private GameObject glitchForm;
    [Tooltip("مؤثر VFX يظهر مع الشكل المشوّه (تشويه/جسيمات)")]
    [SerializeField] private GameObject glitchVfx;

    [Header("الضرر")]
    [Tooltip("يقتل اللاعب عند التلامس أثناء المطاردة")]
    [SerializeField] private bool killOnContact = true;

    [Header("مناطق الأمان")]
    [Tooltip("لا يدخل SafeZone مشتعلة ولا يقدر يمسك اللاعب داخلها — يقف برّا ويراقبه")]
    [SerializeField] private bool respectSafeZones = true;

    private Transform player;
    private State state;
    private int speedHash;
    private bool hasSpeedParam;
    private float groundY;
    private float currentBaseY;   // الارتفاع الفعلي، يتحرك بنعومة نحو المستهدف
    private float trackedBase;    // مستوى اللاعب المعتمد، لا يتغيّر إلا بفرق كبير
    private float blinkTimer;
    private bool blinking;
    private float glitchTimer;
    private bool glitchFrozen;
    private Vector3 glitchBaseLocalPos;

    /// <summary>هل العدو في وضع المطاردة (صلب ومؤذٍ)؟</summary>
    public bool IsHunting => state == State.Hunting;

    private void Awake()
    {
        groundY = transform.position.y;
        currentBaseY = groundY + hoverHeight;
        trackedBase = groundY;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (glitchForm != null) glitchBaseLocalPos = glitchForm.transform.localPosition;
        CacheSpeedParam();

        if (voiceSource != null && ambientLoop != null)
        {
            voiceSource.clip = ambientLoop;
            voiceSource.loop = true;
            voiceSource.volume = 0f;
            voiceSource.Play();
        }
    }

    private void OnEnable()
    {
        if (flag != null)
        {
            flag.PickedUp += OnFlagTaken;
            flag.Placed += OnFlagDown;
        }
    }

    private void OnDisable()
    {
        if (flag != null)
        {
            flag.PickedUp -= OnFlagTaken;
            flag.Placed -= OnFlagDown;
        }
    }

    private void Start()
    {
        // الحالة الابتدائية حسب العلم — والعكس عند تفعيل huntsWhenFlagHeld
        bool held = flag != null && flag.IsHeld;
        SetState(held == huntsWhenFlagHeld ? State.Hunting : State.Retreating);
    }

    /// <summary>
    /// يربط العلم بعد التوليد — يستخدمه <see cref="GhostSpawner"/>.
    /// بدونه يولد الشبح بعلم فارغ فيبدأ في حالة التراجع بدل المطاردة.
    /// </summary>
    public void SetFlag(FlagItem newFlag)
    {
        if (flag != null)
        {
            flag.PickedUp -= OnFlagTaken;
            flag.Placed -= OnFlagDown;
        }

        flag = newFlag;

        if (flag != null && isActiveAndEnabled)
        {
            flag.PickedUp += OnFlagTaken;
            flag.Placed += OnFlagDown;
        }

        bool held = flag != null && flag.IsHeld;
        SetState(held == huntsWhenFlagHeld ? State.Hunting : State.Retreating);
    }

    private void OnFlagTaken() => SetState(huntsWhenFlagHeld ? State.Hunting : State.Retreating);
    private void OnFlagDown() => SetState(huntsWhenFlagHeld ? State.Retreating : State.Hunting);

    private void SetState(State s)
    {
        // صرخة بدء المطاردة تُطلق مرة واحدة عند التحوّل، لا كل إطار
        if (s == State.Hunting && state != State.Hunting &&
            huntStartSound != null && voiceSource != null)
            voiceSource.PlayOneShot(huntStartSound);

        state = s;
        // إيقاف أي ومضة جارية حتى لا تعيد تفعيل الشكل الخطأ
        StopAllCoroutines();
        blinking = false;
        blinkTimer = 0f;

        bool hunting = s == State.Hunting;
        if (solidForm != null) solidForm.SetActive(hunting);
        if (glitchForm != null)
        {
            glitchForm.SetActive(!hunting);
            glitchForm.transform.localPosition = glitchBaseLocalPos; // إلغاء اهتزاز القلتش
        }
        if (glitchVfx != null) glitchVfx.SetActive(!hunting);

        // إعادة سرعة الأنيميتور الطبيعية عند الخروج من حالة الشبح
        glitchFrozen = false;
        glitchTimer = 0f;
        if (animator != null) animator.speed = 1f;
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        float speed;
        if (state == State.Hunting)
        {
            speed = Hunt();
        }
        else
        {
            UpdateGlitchStutter(); // يضبط glitchFrozen وسرعة الأنيميتور
            speed = Retreat();
        }

        if (hasSpeedParam) animator.SetFloat(speedHash, speed);

        UpdateVoice();
    }

    /// <summary>
    /// يرفع الأنين كلما قرب من اللاعب ويخفته كلما ابتعد — فيتحوّل الصوت إلى
    /// مؤشّر مسافة يسمعه اللاعب قبل أن يراه. أقوى أدوات الرعب وأرخصها.
    /// </summary>
    private void UpdateVoice()
    {
        if (voiceSource == null || ambientLoop == null) return;

        // يصمت تمامًا وهو متراجع — الأنين للتهديد لا للوجود
        float wanted = 0f;
        if (state == State.Hunting && player != null && hearingRange > 0.01f)
        {
            float dist = Flatten(player.position - transform.position).magnitude;
            wanted = Mathf.Clamp01(1f - dist / hearingRange) * ambientMaxVolume;
        }

        voiceSource.volume = Mathf.Lerp(voiceSource.volume, wanted, Time.deltaTime * 3f);
    }

    /// <summary>يجمّد ويشغّل الأنيميتور بشكل عشوائي سريع فتطلع المشية متقطّعة (قلتش).</summary>
    private void UpdateGlitchStutter()
    {
        if (!glitchAnimation || animator == null)
        {
            glitchFrozen = false;
            return;
        }

        glitchTimer -= Time.deltaTime;
        if (glitchTimer <= 0f)
        {
            glitchFrozen = !glitchFrozen;
            animator.speed = glitchFrozen ? 0f : 1f;
            glitchTimer = glitchFrozen
                ? Random.Range(glitchFreezeTime.x, glitchFreezeTime.y)
                : Random.Range(glitchOnTime.x, glitchOnTime.y);

            // قفزة اهتزاز بصرية عند كل تبديل
            if (glitchForm != null && glitchJitter > 0f)
                glitchForm.transform.localPosition = glitchBaseLocalPos +
                    (Vector3)(Random.insideUnitCircle * glitchJitter);
        }
    }

    private float Hunt()
    {
        Vector3 to = Flatten(player.position - transform.position);
        float dist = to.magnitude;

        if (respectSafeZones)
        {
            // لو اشتعل ملجأ فوق العدو نفسه → يطلع منه فورًا
            var here = SafeZone.ZoneAt(transform.position, SafeZone.Targets.Monsters);
            if (here != null)
            {
                Vector3 away = Flatten(transform.position - here.transform.position);
                if (away.sqrMagnitude < 0.0001f) away = -to; // واقف في المركز تمامًا
                Move(away.normalized, huntSpeed);
                return huntSpeed;
            }

            // اللاعب داخل الضوء → يقف برّا ويراقبه بدل ما يلحقه
            if (SafeZone.IsSafe(player.position, SafeZone.Targets.Monsters))
            {
                FaceDir(to.normalized);
                return 0f;
            }
        }

        if (dist <= contactRange)
        {
            if (killOnContact)
            {
                var k = player.GetComponentInParent<PlayerKillable>();
                if (k != null && !k.IsDead) k.Kill();
            }
            return 0f;
        }

        // يواجه اللاعب دائمًا حتى وهو واقف بين الومضات
        FaceDir(to.normalized);

        if (blinkMovement)
        {
            if (!blinking)
            {
                blinkTimer += Time.deltaTime;
                if (blinkTimer >= blinkInterval)
                {
                    blinkTimer = 0f;
                    StartCoroutine(DoBlink(to.normalized, dist));
                }
            }
            return 0f; // لا مشية ظاهرة — يظهر واقفًا ثم يومض
        }

        Move(to.normalized, huntSpeed);
        return huntSpeed;
    }

    private System.Collections.IEnumerator DoBlink(Vector3 dir, float distToPlayer)
    {
        blinking = true;
        PlayBlink();

        // يختفي
        if (solidForm != null) solidForm.SetActive(false);
        yield return new WaitForSeconds(blinkHidden);

        // يقفز نحو اللاعب (بدون تجاوزه)
        float step = Mathf.Min(blinkStep, Mathf.Max(0f, distToPlayer - contactRange * 0.9f));
        if (respectSafeZones &&
            SafeZone.IsSafe(transform.position + dir * step, SafeZone.Targets.Monsters))
            step = 0f; // لا يومض إلى داخل ملجأ مشتعل
        transform.position += dir * step;
        if (lockToGroundY)
        {
            Vector3 p = transform.position; p.y = groundY; transform.position = p;
        }

        // يظهر فجأة
        if (solidForm != null) solidForm.SetActive(true);
        PlayBlink();
        blinking = false;
    }

    private void PlayBlink()
    {
        if (blinkVfx != null)
        {
            blinkVfx.transform.position = transform.position;
            blinkVfx.SetActive(false);
            blinkVfx.SetActive(true); // إعادة تشغيل المؤثر
        }
        if (blinkSound != null && blinkAudio != null)
            blinkAudio.PlayOneShot(blinkSound);
    }

    private void FaceDir(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
    }

    private float Retreat()
    {
        Vector3 away = Flatten(transform.position - player.position);
        if (away.magnitude >= retreatDistance)
        {
            FaceDir((player.position - transform.position).normalized); // يظل مواجهًا اللاعب وهو واقف
            return 0f; // بعيد كفاية → يقف
        }

        // أثناء تجمّد القلتش لا يتحرك (فتطلع الحركة متقطّعة كالتشويه)
        if (!glitchFrozen)
            Move(away.normalized, retreatSpeed);
        else
            FaceDir(away.normalized);

        // نُبقي بارامتر السرعة مرتفعًا ليظل يختار مقطع المشي؛ التقطيع من animator.speed
        return retreatSpeed;
    }

    private void Move(Vector3 dir, float speed)
    {
        transform.position += dir * speed * Time.deltaTime;
        ApplyHeight();
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * turnSpeed);
        }
    }

    /// <summary>
    /// يثبّت الارتفاع على مستوى البداية + ارتفاع التحليق، مع تمايل جيبي.
    /// التمايل هو ما يفرّق بين "شبح طافٍ" و"مجسّم معلّق في الهواء".
    /// </summary>
    private void ApplyHeight()
    {
        if (!lockToGroundY) return;

        // يلاحق ارتفاع اللاعب فيعبر المنصّات والحفر، أو يبقى على مستوى بدايته
        float wantedBase = trackedBase;

        if (followPlayerHeight && player != null)
        {
            // لا نتبع إلا الفروق الكبيرة (طابق/حفرة). القفزة أصغر من المنطقة الميتة
            // فتُتجاهَل، وإلا اهتز الشبح صعودًا ونزولًا مع كل قفزة للاعب
            if (Mathf.Abs(player.position.y - trackedBase) > verticalDeadzone)
                trackedBase = player.position.y;
            wantedBase = trackedBase;
        }
        else
        {
            wantedBase = groundY;
        }

        // انتقال تدريجي حتى لا يقفز رأسيًا عند فرق ارتفاع مفاجئ
        currentBaseY = Mathf.MoveTowards(currentBaseY, wantedBase + hoverHeight,
                                         verticalSpeed * Time.deltaTime);

        float bob = bobAmount > 0f ? Mathf.Sin(Time.time * bobSpeed) * bobAmount : 0f;

        Vector3 p = transform.position;
        p.y = currentBaseY + bob;   // التمايل فوق الارتفاع المتتبَّع لا بدلًا منه
        transform.position = p;
    }

    private Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    private void CacheSpeedParam()
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
            if (p.name == speedParam && p.type == AnimatorControllerParameterType.Float)
            {
                speedHash = Animator.StringToHash(speedParam);
                hasSpeedParam = true;
                return;
            }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactRange);
        Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}

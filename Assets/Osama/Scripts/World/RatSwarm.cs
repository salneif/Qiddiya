using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// سرب فئران بأسلوب A Plague Tale: يملأ منطقة مظلمة ويقتل من يدخلها،
/// لكنه <b>يهرب من الضوء</b> — أي <see cref="SafeZone"/> مشتعلة تحفر فيه فجوة
/// آمنة يمشي فيها اللاعب.
///
/// كل فأر يُدفع للخارج بشكل مستقل، فتتكوّن الفجوة طبيعيًا حول الضوء وتتحرك معه
/// إذا كانت اللمبة متنقّلة (<see cref="PushableObject"/> / <see cref="RemoteSlideControl"/>).
///
/// التركيب: كائن فارغ في مركز العش + هذا السكربت، وحدّد <see cref="ratPrefab"/>
/// بمجسم فأر صغير. الفئران تُولَّد تلقائيًا عند التشغيل.
///
/// ملاحظة: الفئران تهرب من <see cref="SafeZone"/> فقط. لو أردت العلم نفسه يطردها،
/// أضف SafeZone على كائن العلم — فيبقى المفهوم واحدًا: الضوء أمان.
/// </summary>
public class RatSwarm : MonoBehaviour
{
    /// <summary>متى يهاجم السرب؟</summary>
    public enum Aggression
    {
        [InspectorName("دائمًا")] Always,
        [InspectorName("عند دخول اللاعب منطقتهم")] WhenIntruded,
        [InspectorName("لا يهاجمون")] Never
    }

    [Header("السرب")]
    [Tooltip("مجسم الفأر — صغير جدًا. اتركه فارغًا لاختبار المنطق بلا مجسمات.")]
    [SerializeField] private GameObject ratPrefab;
    [Tooltip("عدد الفئران")]
    [SerializeField] private int count = 30;
    [Tooltip("منطقة العش بشكل حر — كولايدر (Is Trigger) يرسم الحدود. مثالي للممرات " +
             "المستطيلة. يحترم دوران الكولايدر وحجمه. اتركه فارغًا ليُستخدم نصف القطر الدائري.")]
    [SerializeField] private Collider territoryArea;
    [Tooltip("نصف قطر منطقة العش — يُستخدم فقط إذا لم تحدد كولايدر أعلاه")]
    [SerializeField] private float territoryRadius = 8f;
    [Tooltip("تشتّت الفئران حول نقطة خروجها")]
    [SerializeField] private float spawnRadius = 0.5f;
    [Tooltip("نقاط الخروج — يُوزَّع السرب عليها بالتناوب فتتكوّن مجموعات متفرّقة في " +
             "الأماكن التي تحددها، بدل كومة واحدة. اتركها فارغة ليخرجوا من مركز الكائن.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("السلوك")]
    [Tooltip("Always = يطاردون دائمًا. " +
             "When Intruded = يتجوّلون بسلام حتى يدخل اللاعب منطقتهم فيهجمون. " +
             "Never = تجوال فقط، لا يهاجمون أبدًا.")]
    [SerializeField] private Aggression aggression = Aggression.WhenIntruded;
    [Tooltip("مدى تجوال كل فأر حول نقطة خروجه — صغّره ليبقى السرب مجموعات متفرّقة، " +
             "كبّره ليختلطوا في كل المنطقة")]
    [SerializeField] private float wanderRadius = 3f;
    [Tooltip("سرعة المطاردة")]
    [SerializeField] private float chaseSpeed = 2.5f;
    [Tooltip("تباعد الفئران حول اللاعب — يمنعهم من التكدّس في نقطة واحدة فيبدون كفأر واحد")]
    [SerializeField] private float chaseSpread = 1.5f;
    [Tooltip("لا يخرجون عن حدود العش. أطفئه ليطاردوه في كل المستوى بلا حد")]
    [SerializeField] private bool leashToTerritory = true;

    [Header("الحركة")]
    [Tooltip("سرعة التجوال العادي")]
    [SerializeField] private float wanderSpeed = 1.5f;
    [Tooltip("سرعة الهرب من الضوء — اجعلها أسرع بكثير ليبدو الهرب مذعورًا")]
    [SerializeField] private float fleeSpeed = 7f;
    [Tooltip("كم يبتعد الفأر عن حافة منطقة الأمان (متر) — هامش أمان بصري")]
    [SerializeField] private float lightMargin = 0.6f;
    [Tooltip("سرعة التفاتها لاتجاه الحركة")]
    [SerializeField] private float turnSpeed = 12f;

    [Header("الأنميشن")]
    [Tooltip("أقصى تسريع لأنميشن الركض أثناء الهرب (1 = بلا تسريع)")]
    [Range(1f, 4f)]
    [SerializeField] private float fleeAnimBoost = 2.2f;
    [Tooltip("مدى التفاوت العشوائي في سرعة أنميشن كل فأر — يكسر التزامن الآلي للسرب")]
    [SerializeField] private Vector2 animSpeedVariation = new Vector2(0.85f, 1.25f);

    [Header("الاختفاء بالعلم")]
    [Tooltip("العلم — أول ما يحمله اللاعب ينسحب السرب هاربًا ثم يتلاشى (ضوء العلم يطردهم). " +
             "ويعود إذا غُرس العلم من جديد. اتركه فارغًا ليبقى السرب دائمًا.")]
    [SerializeField] private FlagItem hideWhenFlagHeld;
    [Tooltip("مدة الهرب قبل الاختفاء (ثواني) — لا تجعلها صفرًا وإلا اختفوا فجأة بشكل رخيص")]
    [SerializeField] private float retreatTime = 1.6f;

    [Header("الأداء")]
    [Tooltip("إطفاء ظلال الفئران — أكبر توفير مفرد، لأن كل فأر يضيف تمريرة ظل كاملة. " +
             "الفئران صغيرة وملتصقة بالأرض فظلالها بالكاد تُرى.")]
    [SerializeField] private bool disableShadows = true;
    [Tooltip("إيقاف تحريك العظام للفئران خارج الكاميرا")]
    [SerializeField] private bool cullAnimatorsOffscreen = true;
    [Tooltip("توزيع تحديث المنطق على عدة إطارات: 3 = ثلث السرب كل إطار. " +
             "الحركة تُعوَّض بخطوات أكبر فلا يُلاحظ الفرق.")]
    [Range(1, 8)]
    [SerializeField] private int updateBatches = 1;

    [Header("الخطر")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("مسافة القتل من أي فأر (متر)")]
    [SerializeField] private float killRadius = 0.7f;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صرير مستمر للسرب")]
    [SerializeField] private AudioClip squeakLoop;

    [Header("أحداث")]
    [Tooltip("عند قتل اللاعب")]
    public UnityEvent onPlayerKilled;

    private Transform[] rats;
    private Vector3[] wanderTargets;
    private Animator[] ratAnimators;
    private float[] animVariation;
    private Vector3[] chaseOffsets;
    private Vector3[] homes;          // نقطة خروج كل فأر — يتجوّل حولها فيبقى التوزيع
    private bool playerIntruding;     // يُحسب مرة كل إطار لا لكل فأر
    private bool retreating;          // ينسحبون الآن بسبب العلم
    private float retreatTimer;
    private bool hidden;
    private Transform player;
    private PlayerKillable killable;
    private float groundY;

    private void Awake()
    {
        groundY = transform.position.y;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        SpawnRats();

        if (audioSource != null && squeakLoop != null)
        {
            audioSource.clip = squeakLoop;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void SpawnRats()
    {
        count = Mathf.Max(0, count);
        rats = new Transform[count];
        wanderTargets = new Vector3[count];
        ratAnimators = new Animator[count];
        animVariation = new float[count];
        chaseOffsets = new Vector3[count];
        homes = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            // يُوزَّعون على نقاط الخروج بالتناوب فتتكوّن مجموعات في أماكنك المحددة
            Vector3 origin = transform.position;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var p = spawnPoints[i % spawnPoints.Length];
                if (p != null) origin = p.position;
            }

            // بلا نقاط خروج: يُوزَّعون على كامل المنطقة بدل التكوّم في المركز
            Vector3 pos = (spawnPoints != null && spawnPoints.Length > 0)
                ? RandomPointNear(origin, spawnRadius)
                : RandomPointInTerritory();
            homes[i] = pos;
            wanderTargets[i] = RandomPointNear(pos, wanderRadius);

            // إزاحة ثابتة لكل فأر حول اللاعب حتى يحيطوا به بدل ما يتكدسوا في نقطة
            Vector2 spread = Random.insideUnitCircle * chaseSpread;
            chaseOffsets[i] = new Vector3(spread.x, 0f, spread.y);

            // بلا مجسم نُنشئ كائنًا فارغًا حتى يعمل المنطق كاملًا (حركة وقتل)،
            // فتقدر تختبر السرب وتضبط الأرقام قبل ما تجهز المجسمات
            GameObject go = ratPrefab != null
                ? Instantiate(ratPrefab)
                : new GameObject($"Rat_{i}");

            go.transform.SetParent(transform);
            go.transform.position = pos;
            rats[i] = go.transform;

            ratAnimators[i] = go.GetComponentInChildren<Animator>();
            animVariation[i] = Random.Range(animSpeedVariation.x, animSpeedVariation.y);

            ApplyPerformanceSettings(go, ratAnimators[i]);
        }
    }

    /// <summary>
    /// يطبّق إعدادات الأداء على كل فأر عند توليده، فلا تحتاج ضبط ٣٠ كائنًا يدويًا.
    /// </summary>
    private void ApplyPerformanceSettings(GameObject rat, Animator animator)
    {
        if (disableShadows)
        {
            foreach (var r in rat.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        if (cullAnimatorsOffscreen && animator != null)
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
    }

    private Vector3 RandomPointNear(Vector3 origin, float radius)
    {
        Vector2 c = Random.insideUnitCircle * radius;
        return ClampToTerritory(new Vector3(origin.x + c.x, groundY, origin.z + c.y));
    }

    /// <summary>نقطة عشوائية موزّعة على كامل المنطقة — تملأ الشكل مهما كان.</summary>
    private Vector3 RandomPointInTerritory()
    {
        if (territoryArea == null) return RandomPointNear(transform.position, territoryRadius);

        // نعتمد الصندوق المحيط ثم نقصّ للداخل، فينضبط الشكل حتى لو كان مدوَّرًا
        Bounds b = territoryArea.bounds;
        return ClampToTerritory(new Vector3(Random.Range(b.min.x, b.max.x), groundY,
                                            Random.Range(b.min.z, b.max.z)));
    }

    /// <summary>هل النقطة داخل حدود العش؟ (المقارنة أفقية — الفئران على الأرض)</summary>
    private bool InTerritory(Vector3 point)
    {
        if (territoryArea == null)
            return Flatten(point - transform.position).sqrMagnitude
                   <= territoryRadius * territoryRadius;

        // فحص أفقي على الصندوق المحيط بدل ClosestPoint: الأخير ينهار صامتًا مع
        // المقياس السالب أو الكولايدر غير المحدّب، وهذا يطابق الـ Gizmo تمامًا
        Bounds b = territoryArea.bounds;
        return point.x >= b.min.x && point.x <= b.max.x &&
               point.z >= b.min.z && point.z <= b.max.z;
    }

    private void OnEnable()
    {
        if (hideWhenFlagHeld == null) return;
        hideWhenFlagHeld.PickedUp += OnFlagTaken;
        hideWhenFlagHeld.Placed += OnFlagPlaced;
    }

    private void OnDisable()
    {
        if (hideWhenFlagHeld == null) return;
        hideWhenFlagHeld.PickedUp -= OnFlagTaken;
        hideWhenFlagHeld.Placed -= OnFlagPlaced;
    }

    /// <summary>حُمل العلم: يهربون مذعورين ثم يتلاشون.</summary>
    private void OnFlagTaken()
    {
        retreating = true;
        retreatTimer = retreatTime;
        SetRatsHidden(false);
    }

    /// <summary>غُرس العلم: يرجع السرب لمنطقته.</summary>
    private void OnFlagPlaced()
    {
        retreating = false;
        SetRatsHidden(false);
    }

    private void SetRatsHidden(bool hide)
    {
        if (hidden == hide || rats == null) return;
        hidden = hide;

        foreach (var rat in rats)
            if (rat != null) rat.gameObject.SetActive(!hide);
    }

    private void Update()
    {
        if (rats == null) return;

        // انسحاب بسبب العلم: يهربون طوال المهلة ثم يختفون
        if (retreating)
        {
            retreatTimer -= Time.deltaTime;
            if (retreatTimer <= 0f) SetRatsHidden(true);
        }
        if (hidden) return;

        ResolvePlayer();

        // هل اقتحم اللاعب المنطقة؟ يُحسب مرة واحدة لا لكل فأر
        playerIntruding = player != null && InTerritory(player.position);

        // توزيع المنطق على إطارات: كل إطار يحدّث شريحة من السرب
        int batches = Mathf.Max(1, updateBatches);
        for (int i = Time.frameCount % batches; i < rats.Length; i += batches)
        {
            var rat = rats[i];
            if (rat == null) continue;
            UpdateRat(i, rat);
        }

        TryKillPlayer();
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        // نعيد المحاولة كل إطار ما دام مفقودًا: لو وُجد اللاعب بلا PlayerKillable
        // (ترتيب مختلف في سين جديد) كان البحث يتوقف للأبد فلا يموت أبدًا
        if (killable == null) killable = player.GetComponentInParent<PlayerKillable>();
    }

    private void UpdateRat(int index, Transform rat)
    {
        // الضوء أولًا: إن كان الفأر داخل ملجأ مشتعل يهرب منه فورًا
        var zone = SafeZone.ZoneAt(rat.position, SafeZone.Targets.Rats);
        if (zone != null)
        {
            Vector3 away = Flatten(rat.position - zone.transform.position);
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitSphere;

            // نقطة خارج حافة المنطقة مباشرة، مقيّدة داخل حدود العش
            Vector3 escape = zone.transform.position +
                             away.normalized * (zone.Radius + lightMargin);
            escape = ClampToTerritory(escape);

            MoveRat(rat, escape, fleeSpeed);
            SetAnimSpeed(index, fleeSpeed);
            wanderTargets[index] = escape; // لا يرجع لهدفه القديم داخل الضوء
            return;
        }

        // انسحاب العلم يتغلّب على كل شيء: يهرب بعيدًا عن اللاعب بأقصى سرعة
        if (retreating && player != null)
        {
            Vector3 away = Flatten(rat.position - player.position);
            if (away.sqrMagnitude < 0.0001f) away = Flatten(Random.insideUnitSphere);

            MoveRat(rat, rat.position + away.normalized * 4f, fleeSpeed);
            SetAnimSpeed(index, fleeSpeed);
            return;
        }

        // منطقة ممنوعة (منصّة/ممر): يرتد عنها بعيدًا عن مركزها
        var blocker = RatBlocker.At(rat.position);
        if (blocker != null)
        {
            Vector3 out_ = Flatten(rat.position - blocker.Center);
            if (out_.sqrMagnitude < 0.0001f) out_ = Flatten(Random.insideUnitSphere);

            Vector3 escape = ClampToTerritory(rat.position + out_.normalized * 2f);
            MoveRat(rat, escape, fleeSpeed);
            SetAnimSpeed(index, fleeSpeed);
            return;
        }

        // مطاردة اللاعب — كل فأر يقصد نقطة قريبة منه لا نفس النقطة
        if (ShouldChase())
        {
            Vector3 destination = player.position + chaseOffsets[index];
            if (leashToTerritory) destination = ClampToTerritory(destination);

            MoveRat(rat, destination, chaseSpeed);
            SetAnimSpeed(index, chaseSpeed);
            return;
        }

        // تجوال هادئ حول نقطة خروجه — لا حول مركز العش، فيبقى التوزيع كما رسمته
        if (Flatten(rat.position - wanderTargets[index]).sqrMagnitude < 0.09f)
            wanderTargets[index] = RandomPointNear(homes[index], wanderRadius);

        MoveRat(rat, wanderTargets[index], wanderSpeed);
        SetAnimSpeed(index, wanderSpeed);
    }

    /// <summary>هل يهاجم السرب الآن؟ يعتمد على وضع العدوانية ووجود اللاعب حيًّا.</summary>
    private bool ShouldChase()
    {
        if (player == null || (killable != null && killable.IsDead)) return false;

        return aggression switch
        {
            Aggression.Always => true,
            Aggression.WhenIntruded => playerIntruding,
            _ => false
        };
    }

    /// <summary>
    /// يربط سرعة أنميشن الركض بسرعة الحركة الفعلية، مع تفاوت ثابت لكل فأر
    /// حتى لا يتحرك السرب كله بتزامن آلي.
    /// </summary>
    private void SetAnimSpeed(int index, float moveSpeed)
    {
        var anim = ratAnimators[index];
        if (anim == null) return;

        float ratio = wanderSpeed > 0.01f ? moveSpeed / wanderSpeed : 1f;
        anim.speed = animVariation[index] * Mathf.Clamp(ratio, 0.5f, fleeAnimBoost);
    }

    private void MoveRat(Transform rat, Vector3 destination, float speed)
    {
        destination.y = groundY;
        Vector3 dir = destination - rat.position;

        // الفأر يُحدَّث كل updateBatches إطارًا، فنعوّض بخطوة أكبر ليبقى بنفس السرعة
        float step = speed * Time.deltaTime * Mathf.Max(1, updateBatches);
        rat.position = Vector3.MoveTowards(rat.position, destination, step);

        if (Flatten(dir).sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(Flatten(dir).normalized, Vector3.up);
            rat.rotation = Quaternion.Slerp(rat.rotation, look, Time.deltaTime * turnSpeed);
        }
    }

    private Vector3 ClampToTerritory(Vector3 point)
    {
        if (territoryArea != null)
        {
            Bounds b = territoryArea.bounds;
            return new Vector3(Mathf.Clamp(point.x, b.min.x, b.max.x), groundY,
                               Mathf.Clamp(point.z, b.min.z, b.max.z));
        }

        Vector3 offset = Flatten(point - transform.position);
        if (offset.magnitude > territoryRadius)
            offset = offset.normalized * territoryRadius;
        return new Vector3(transform.position.x + offset.x, groundY, transform.position.z + offset.z);
    }

    private void TryKillPlayer()
    {
        if (player == null || killable == null || killable.IsDead) return;

        // منسحبون بسبب العلم = غير مؤذين، وإلا قتلوه وهم هاربون منه
        if (retreating) return;

        // اللاعب داخل ضوء طارد للفئران = آمن مهما اقتربت (الضوء أقوى من العدد)
        if (SafeZone.IsSafe(player.position, SafeZone.Targets.Rats)) return;

        float sqrKill = killRadius * killRadius;
        foreach (var rat in rats)
        {
            if (rat == null) continue;
            if (Flatten(player.position - rat.position).sqrMagnitude > sqrKill) continue;

            killable.Kill();
            onPlayerKilled?.Invoke();
            return;
        }
    }

    private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }

    /// <summary>
    /// حدود العش تُرسم دائمًا (بلا تحديد الكائن) لأنها أداة التشخيص الأولى:
    /// أخضر = اللاعب مقتحم والهجوم مفعّل، أحمر = خارج المنطقة.
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = (Application.isPlaying && playerIntruding)
            ? new Color(0.2f, 1f, 0.3f, 1f)
            : new Color(0.6f, 0.2f, 0.2f, 0.9f);

        if (territoryArea != null)
        {
            Gizmos.DrawWireCube(territoryArea.bounds.center, territoryArea.bounds.size);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity,
                                          new Vector3(1f, 0.02f, 1f));
            Gizmos.DrawWireSphere(Vector3.zero, territoryRadius);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;

        // نقاط الخروج ومدى تشتّت كل مجموعة حولها
        if (spawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            foreach (var p in spawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, spawnRadius);
        }

        // مواقع الفئران ومدى قتلها وقت التشغيل — تراها حتى بلا مجسمات
        if (rats == null) return;
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        foreach (var rat in rats)
            if (rat != null) Gizmos.DrawWireSphere(rat.position, killRadius);
    }
}

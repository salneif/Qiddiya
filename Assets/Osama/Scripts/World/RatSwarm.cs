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
    [Header("السرب")]
    [Tooltip("مجسم الفأر — صغير جدًا. اتركه فارغًا لاختبار المنطق بلا مجسمات.")]
    [SerializeField] private GameObject ratPrefab;
    [Tooltip("عدد الفئران")]
    [SerializeField] private int count = 30;
    [Tooltip("نصف قطر منطقة العش — حدود تحرّك الفئران")]
    [SerializeField] private float territoryRadius = 8f;
    [Tooltip("نصف قطر نقطة الخروج من العش — صغّره (0.5) ليطلعوا كلهم من الجحر " +
             "ثم ينتشروا، أو كبّره ليكونوا موزّعين من البداية")]
    [SerializeField] private float spawnRadius = 0.5f;

    [Header("السلوك")]
    [Tooltip("يطاردون اللاعب بدل التجوال العشوائي")]
    [SerializeField] private bool chasePlayer = true;
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

        for (int i = 0; i < count; i++)
        {
            // كلهم يخرجون من الجحر (نقطة الكائن) ثم ينتشرون بحركتهم
            Vector3 pos = RandomPointAround(spawnRadius);
            wanderTargets[i] = RandomPointAround(territoryRadius);

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

    private Vector3 RandomPointAround(float radius)
    {
        Vector2 c = Random.insideUnitCircle * radius;
        return new Vector3(transform.position.x + c.x, groundY, transform.position.z + c.y);
    }

    private void Update()
    {
        if (rats == null) return;

        ResolvePlayer();

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
        if (player != null) return;
        var go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return;
        player = go.transform;
        killable = player.GetComponentInParent<PlayerKillable>();
    }

    private void UpdateRat(int index, Transform rat)
    {
        // الضوء أولًا: إن كان الفأر داخل ملجأ مشتعل يهرب منه فورًا
        var zone = SafeZone.ZoneAt(rat.position);
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
        if (chasePlayer && player != null && (killable == null || !killable.IsDead))
        {
            Vector3 destination = player.position + chaseOffsets[index];
            if (leashToTerritory) destination = ClampToTerritory(destination);

            MoveRat(rat, destination, chaseSpeed);
            SetAnimSpeed(index, chaseSpeed);
            return;
        }

        // تجوال عادي: هدف جديد كل ما وصل هدفه
        if (Flatten(rat.position - wanderTargets[index]).sqrMagnitude < 0.09f)
            wanderTargets[index] = RandomPointAround(territoryRadius);

        MoveRat(rat, wanderTargets[index], wanderSpeed);
        SetAnimSpeed(index, wanderSpeed);
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
        Vector3 offset = Flatten(point - transform.position);
        if (offset.magnitude > territoryRadius)
            offset = offset.normalized * territoryRadius;
        return new Vector3(transform.position.x + offset.x, groundY, transform.position.z + offset.z);
    }

    private void TryKillPlayer()
    {
        if (player == null || killable == null || killable.IsDead) return;

        // اللاعب داخل ضوء = آمن مهما اقتربت الفئران (الضوء أقوى من العدد)
        if (SafeZone.IsSafe(player.position)) return;

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

    private void OnDrawGizmosSelected()
    {
        // حدود العش
        Gizmos.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity,
                                      new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, territoryRadius);

        // مواقع الفئران ومدى قتلها وقت التشغيل — تراها حتى بلا مجسمات
        if (rats == null) return;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        foreach (var rat in rats)
            if (rat != null) Gizmos.DrawWireSphere(rat.position, killRadius);
    }
}

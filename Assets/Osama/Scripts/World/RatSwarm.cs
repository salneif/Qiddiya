using UnityEngine;
using UnityEngine.Events;

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
    [Tooltip("نصف قطر منطقة العش — الفئران لا تخرج منها")]
    [SerializeField] private float territoryRadius = 8f;

    [Header("الحركة")]
    [Tooltip("سرعة التجوال العادي")]
    [SerializeField] private float wanderSpeed = 1.5f;
    [Tooltip("سرعة الهرب من الضوء — اجعلها أسرع بكثير ليبدو الهرب مذعورًا")]
    [SerializeField] private float fleeSpeed = 7f;
    [Tooltip("كم يبتعد الفأر عن حافة منطقة الأمان (متر) — هامش أمان بصري")]
    [SerializeField] private float lightMargin = 0.6f;
    [Tooltip("سرعة التفاتها لاتجاه الحركة")]
    [SerializeField] private float turnSpeed = 12f;

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

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = RandomPointInTerritory();
            wanderTargets[i] = RandomPointInTerritory();

            if (ratPrefab == null) continue;
            var go = Instantiate(ratPrefab, pos, Quaternion.identity, transform);
            rats[i] = go.transform;
        }
    }

    private Vector3 RandomPointInTerritory()
    {
        Vector2 c = Random.insideUnitCircle * territoryRadius;
        return new Vector3(transform.position.x + c.x, groundY, transform.position.z + c.y);
    }

    private void Update()
    {
        if (rats == null) return;

        ResolvePlayer();

        for (int i = 0; i < rats.Length; i++)
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
            wanderTargets[index] = escape; // لا يرجع لهدفه القديم داخل الضوء
            return;
        }

        // تجوال عادي: هدف جديد كل ما وصل هدفه
        if (Flatten(rat.position - wanderTargets[index]).sqrMagnitude < 0.09f)
            wanderTargets[index] = RandomPointInTerritory();

        MoveRat(rat, wanderTargets[index], wanderSpeed);
    }

    private void MoveRat(Transform rat, Vector3 destination, float speed)
    {
        destination.y = groundY;
        Vector3 dir = destination - rat.position;

        rat.position = Vector3.MoveTowards(rat.position, destination, speed * Time.deltaTime);

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
    }
}

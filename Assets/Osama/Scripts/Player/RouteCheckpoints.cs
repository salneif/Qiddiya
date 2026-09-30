using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>نقاط رجوعٍ على الطريق</b> لمرحلةٍ بلا نقاط حفظ (ستيم تاون): نقطة بداية، وحلقاتٌ خفيفة على
/// الأرض كل ١٤ م على مسار الوجوه — تشتعل حين يقف اللاعب عليها، فيعود إليها إن مات.
///
/// في ستيم لا <see cref="Checkpoint"/> واحدة، ونقطة البعث المضبوطة في السين <b>قدّام</b> البداية: من مات
/// (أو ضغط Respawn) أوّل المرحلة وجد نفسه متقدّمًا. هنا:
/// <list type="bullet">
/// <item><b>البداية</b>: أوّل أرضٍ ثابتة يقف عليها اللاعب تصير نقطة البعث.</item>
/// <item><b>آخر حلقةٍ لُمست</b> هي نقطة البعث — فالرجوع دائمًا لمكانٍ كان فيه، لا لمكانٍ لم يصله.</item>
/// <item>الموضع موضع قدميه هو لحظة اللمس (مكانٌ وقف فيه حيًّا)، بعد أن تثبت الأرض تحته ثلث ثانية —
/// لا يُحفظ فوق مصعدٍ يتحرّك. ويُعلَّق بأرضه: أرضٌ تتحرّك يتحرّك معها، وأرضٌ تختفي تُسقطه للّتي قبله.</item>
/// <item><b>موتتان متتاليتان</b> (أقلّ من ٥ ث) تعني نقطةً سيّئة: تُسقط ويرجع للّتي قبلها.</item>
/// </list>
///
/// لا يعمل حيث في السين نقاط حفظ (السيرك)، ولا حيث لا بعث مضبوط، ولا في التوايلايت (نظام علي).
/// وأوّل <see cref="Checkpoint"/> حقيقيّة تُطلق تُسلّم لها القيادة. يُركّب نفسه.
/// </summary>
[DisallowMultipleComponent]
public class RouteCheckpoints : MonoBehaviour
{
    private const float Spacing = 14f;              // بين حلقتين، وبين الأولى والبداية
    private const int MaxPoints = 16;
    private const float TouchRadius = 1.6f, TouchHeight = 2.2f;
    private const float SettleTime = 0.35f, StaticTolerance = 0.02f;
    private const float QuickDeath = 5f;
    private const float RingSize = 1.9f, RingLift = 0.05f;

    private sealed class Point
    {
        public Vector3 at;              // عند التخطيط
        public Transform floor;         // أرضها (تتحرّك معها)
        public Vector3 local;
        public Renderer ring;
        public bool lit;

        public Vector3 World => floor != null ? floor.TransformPoint(local) : at;
    }

    private sealed class Spawn
    {
        public Transform marker;
        public Collider ground;
    }

    private static RouteCheckpoints instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("RouteCheckpoints") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<RouteCheckpoints>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private string scene;
    private bool decided, armed, planned;
    private CharacterController body;
    private PlayerKillable killable;
    private float nextFind;

    private readonly List<Point> points = new List<Point>();
    private readonly List<Spawn> spawns = new List<Spawn>();
    private int current = -2;                       // -1 = البداية
    private float lastDeath = -99f;

    // لمسٌ ينتظر ثبات الأرض
    private int pendingPoint = -3;
    private Collider pendingGround;
    private Vector3 pendingGroundAt;
    private float pendingSince;

    private int groundMask;
    private Material ringMaterial;

    private void Awake()
    {
        int body = LayerMask.NameToLayer("Player");
        groundMask = Physics.DefaultRaycastLayers & ~(body >= 0 ? 1 << body : 0);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaFunEvents.DropsPlanned += OnPlanned;
        Checkpoint.Activated += OnRealCheckpoint;
        PlayerKillable.Died += OnDied;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaFunEvents.DropsPlanned -= OnPlanned;
        Checkpoint.Activated -= OnRealCheckpoint;
        PlayerKillable.Died -= OnDied;
    }

    private void OnSceneLoaded(Scene loaded, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(loaded);
    }

    /// <summary>كل ما صنعناه كائناتٌ في السين (معلّقةٌ بأرضها)، فتذهب معه. هنا ننسى مراجعها فقط.</summary>
    private void Begin(Scene loaded)
    {
        scene = loaded.name;
        decided = armed = planned = false;
        body = null;
        killable = null;
        nextFind = 0f;
        points.Clear();
        spawns.Clear();
        current = -2;
        pendingPoint = -3;
        pendingGround = null;
    }

    private void OnRealCheckpoint(Checkpoint point) => armed = false;   // نقاط السين تقود من هنا

    private void OnDied()
    {
        if (!armed) return;
        // موتتان متتاليتان: النقطة الحالية سيّئة (خطرٌ عندها، أرضٌ تنهار) — نرجع للّتي قبلها
        if (Time.time - lastDeath < QuickDeath && spawns.Count > 1) Drop(spawns.Count - 1);
        lastDeath = Time.time;
    }

    // ---------- الحلقات ----------

    private void OnPlanned(string plannedScene, int total, string[] golden)
    {
        if (plannedScene != scene) return;
        planned = true;
        if (decided && armed) Lay();   // وإلا تُرسم حين نقرّر (Decide) — لا حلقة في سينٍ ليس لنا
    }

    private void Lay()
    {
        if (points.Count > 0 || !planned || body == null) return;
        IReadOnlyList<ChromaDropPlanner.Spot> route = ChromaDropField.Route;
        if (route == null || route.Count == 0) return;

        // الأقرب للبداية أوّلًا، وكل حلقةٍ تبعد Spacing عن كل ما قبلها
        Vector3 origin = body != null ? body.transform.position : route[0].ground;
        var candidates = new List<ChromaDropPlanner.Spot>();
        foreach (ChromaDropPlanner.Spot s in route)
            if (s.floor != null && !s.floor.isTrigger && s.floor.attachedRigidbody == null) candidates.Add(s);
        candidates.Sort((a, b) => (a.ground - origin).sqrMagnitude.CompareTo((b.ground - origin).sqrMagnitude));

        foreach (ChromaDropPlanner.Spot s in candidates)
        {
            if (points.Count >= MaxPoints) break;
            if (Flat(s.ground - origin) < Spacing) continue;
            bool clear = true;
            foreach (Point p in points)
                if ((p.at - s.ground).sqrMagnitude < Spacing * Spacing) { clear = false; break; }
            if (!clear) continue;

            points.Add(new Point
            {
                at = s.ground,
                floor = s.floor.transform,
                local = s.floor.transform.InverseTransformPoint(s.ground),
                ring = MakeRing(s.floor.transform, s.ground),
            });
        }
    }

    private Renderer MakeRing(Transform floor, Vector3 at)
    {
        if (ringMaterial == null)
        {
            Material source = Resources.Load<Material>("Chroma/Fx/FxRingAdd");
            if (source == null) return null;
            ringMaterial = new Material(source);
        }

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ring.name = "Chroma Respawn Ring";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetPositionAndRotation(at + Vector3.up * RingLift, Quaternion.Euler(90f, 0f, 0f));
        ring.transform.localScale = Vector3.one * RingSize;
        ring.transform.SetParent(floor, true);

        var renderer = ring.GetComponent<Renderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Tint(renderer, false, 0f);
        return renderer;
    }

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static MaterialPropertyBlock block;

    /// <summary>حلقةٌ خافتة تتنفّس قبل اللمس، وذهبيّةٌ هادئة بعده.</summary>
    private static void Tint(Renderer ring, bool lit, float time)
    {
        if (ring == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        Color c = lit
            ? Color.Lerp(ChromaStyle.Get().gold, Color.white, 0.2f) * 0.55f
            : Color.white * (0.22f + 0.1f * Mathf.Sin(time * 2.2f));
        c.a = 1f;
        ring.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        block.SetColor(ColorId, c);
        ring.SetPropertyBlock(block);
    }

    // ---------- اللمس ----------

    private void Update()
    {
        if (decided && !armed) return;
        if (!Bind()) return;
        if (!decided) Decide();
        if (!armed) return;

        float now = Time.time;
        for (int i = 0; i < points.Count; i++)
            if (!points[i].lit && points[i].ring != null && i % 4 == Time.frameCount % 4) Tint(points[i].ring, false, now + i);

        if (ChromaEvents.Quiet || Time.timeScale <= 0f || killable.IsDead) { pendingPoint = -3; return; }
        CheckSpawnStillStands();

        if (current == -2) { Settle(-1); return; }   // البداية أوّلًا

        Vector3 feet = body.transform.position;
        int touching = -3;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 d = points[i].World - feet;
            if (Flat(d) < TouchRadius && Mathf.Abs(d.y) < TouchHeight) { touching = i; break; }
        }
        if (touching >= 0 && touching != current) Settle(touching);
        else pendingPoint = -3;
    }

    /// <summary>يُحفظ حين يقف على أرضٍ ثابتة ثلث ثانية — لا في قفزة، ولا فوق مصعدٍ يتحرّك.</summary>
    private void Settle(int point)
    {
        bool grounded = JumpPolish.Active ? JumpPolish.Grounded : body.isGrounded;
        Collider ground = grounded ? GroundUnder() : null;
        if (ground == null) { pendingPoint = -3; return; }

        if (pendingPoint != point || pendingGround != ground)
        {
            pendingPoint = point;
            pendingGround = ground;
            pendingGroundAt = ground.transform.position;
            pendingSince = Time.time;
            return;
        }
        if ((ground.transform.position - pendingGroundAt).sqrMagnitude > StaticTolerance * StaticTolerance)
        {
            pendingGroundAt = ground.transform.position;   // تتحرّك: نعيد العدّ
            pendingSince = Time.time;
            return;
        }
        if (Time.time - pendingSince < SettleTime) return;

        Commit(point, ground);
    }

    private void Commit(int point, Collider ground)
    {
        pendingPoint = -3;
        Transform player = body.transform;
        var marker = new GameObject(point < 0 ? "Chroma Respawn (start)" : "Chroma Respawn " + point).transform;
        marker.SetPositionAndRotation(player.position, Quaternion.Euler(0f, player.eulerAngles.y, 0f));
        marker.SetParent(ground.transform, true);

        spawns.Add(new Spawn { marker = marker, ground = ground });
        if (spawns.Count > 24) { if (spawns[1].marker != null) Destroy(spawns[1].marker.gameObject); spawns.RemoveAt(1); }
        killable.SetRespawnPoint(marker);
        current = point;
        if (point < 0) return;

        Point p = points[point];
        if (!p.lit)
        {
            p.lit = true;
            Tint(p.ring, true, 0f);
            ChromaEvents.RaiseCheckpoint(player.position);   // حلقةٌ وشررٌ وصوت (ChromaJuice)
        }
    }

    /// <summary>أرض النقطة الحالية اختفت أو أُطفئت: نرجع للّتي قبلها.</summary>
    private void CheckSpawnStillStands()
    {
        for (int i = spawns.Count - 1; i >= 1; i--)
        {
            Spawn s = spawns[i];
            bool gone = s.marker == null || s.ground == null || !s.ground.enabled || !s.ground.gameObject.activeInHierarchy;
            if (!gone) break;
            Drop(i);
        }
    }

    private void Drop(int index)
    {
        if (index <= 0 || index >= spawns.Count) return;
        if (spawns[index].marker != null) Destroy(spawns[index].marker.gameObject);
        spawns.RemoveAt(index);
        Spawn back = spawns[spawns.Count - 1];
        if (back.marker != null) killable.SetRespawnPoint(back.marker);
        current = -3;   // يلمس أيّ حلقةٍ من جديد
    }

    // ---------- اللاعب ----------

    private bool Bind()
    {
        if (body != null && killable != null) return true;
        if (Time.unscaledTime < nextFind) return false;
        nextFind = Time.unscaledTime + 1f;

        GameObject found = PlayerLocator.Find("Player");
        body = found != null ? found.GetComponentInParent<CharacterController>() : null;
        if (body == null) return false;
        killable = body.GetComponentInParent<PlayerKillable>();
        if (killable == null) killable = body.GetComponentInChildren<PlayerKillable>();
        if (killable != null) return true;

        decided = true;          // مرحلةٌ بلا PlayerKillable (التوايلايت، الهب): لا شأن لنا
        armed = false;
        return false;
    }

    /// <summary>
    /// نعمل فقط حيث: بعثٌ مضبوط في السين (وإلا فـPlayerKillable يعيده لآخر أرضٍ وقف عليها — تصميم
    /// السيرك)، ولا نقطة حفظٍ واحدة. ونقرّر بعد أن تنتهي شاشة التحميل ويوضع اللاعب في مدخله.
    /// </summary>
    private void Decide()
    {
        if (ChromaEvents.Quiet) return;
        decided = true;
        armed = killable.RespawnPoint != null &&
                FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0;
        if (armed) Lay();   // خُطّط قبل أن نقرّر
    }

    private Collider GroundUnder()
    {
        Bounds b = body.bounds;
        Vector3 from = new Vector3(b.center.x, b.min.y + 0.3f, b.center.z);
        return Physics.Raycast(from, Vector3.down, out RaycastHit hit, 0.8f, groundMask, QueryTriggerInteraction.Ignore)
            ? hit.collider : null;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}

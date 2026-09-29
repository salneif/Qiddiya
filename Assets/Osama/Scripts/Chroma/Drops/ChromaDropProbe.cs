using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// أسئلة الفيزياء التي تقرّر أين تقف قطرة: أرضٌ تُمشى تحتها، ومتّسعٌ فوقها لرأس اللاعب،
/// وخطّ نظرٍ إليها لا يخترق جدارًا، وبُعدٌ عن كل ما يقتل.
///
/// <b>القطرة وعد</b>: من يراها يمشي إليها. فقطرةٌ داخل جدار أو فوق حفرة أو على حافّة
/// لافا تُعلّم اللاعب أن يثق بها ثم تقتله. لذا كل سؤالٍ هنا يُجاب بالتشدّد: الشكّ يُسقط
/// القطرة، ولا يُسقط اللاعب.
///
/// والخطر يُعرف بثلاث طرق لأن المراحل من أيدٍ مختلفة:
/// <list type="bullet">
/// <item>بالنوع: <c>LavaKill</c> و<c>DangerZone</c> و<c>RiseKill</c> والفاس والفئران وموت الماء
/// عند علي — <b>والمطفأ منها أيضًا</b>: قتلة اللافا في ستيم تُشعَل وتُطفأ مع كل صبّة.</item>
/// <item>بالاسم: Lava / Death / Kill / Water / Void — تريغرات السقوط عند غيرنا بلا سكربتٍ نعرفه.</item>
/// <item>بطبقة <c>Water</c>.</item>
/// </list>
/// </summary>
public sealed class ChromaDropProbe
{
    /// <summary>ارتفاع القطرة فوق أرضها.</summary>
    public const float Hover = 0.9f;

    /// <summary>أرضٌ تُمشى: ميلها أقلّ من ٤٥° تقريبًا.</summary>
    private const float Walkable = 0.7f;

    /// <summary>قربٌ من الخطر يُسقط القطرة — حول القطرة وحول أرضها.</summary>
    private const float HazardReach = 1.2f;

    /// <summary>متّسع الرأس: كبسولةٌ من فوق الأرض بقليل إلى فوق رأس اللاعب.</summary>
    private const float ClearFrom = 0.55f, ClearTo = 1.65f, ClearRadius = 0.28f;

    private static readonly string[] HazardWords = { "lava", "death", "kill", "water", "void" };

    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Collider[] touching = new Collider[64];
    private readonly Dictionary<Collider, bool> verdicts = new Dictionary<Collider, bool>();
    private readonly List<Bounds> boxes = new List<Bounds>();
    private readonly List<Vector4> spheres = new List<Vector4>();   // xyz = المركز، w = نصف القطر
    private readonly List<Type> hazardTypes = new List<Type>();
    private readonly Transform body;
    private readonly int solid;
    private readonly int water;
    private readonly int playerLayer;
    private readonly float floorY = float.NegativeInfinity;

    /// <summary>
    /// <paramref name="body"/> جسم اللاعب — كولايدراته ليست أرضًا ولا جدارًا. والخرائط
    /// تُبنى مرّة لكل سين: الأخطار في المراحل ثابتةٌ في أماكنها.
    /// </summary>
    public ChromaDropProbe(Transform body)
    {
        this.body = body;
        playerLayer = LayerMask.NameToLayer("Player");
        water = LayerMask.NameToLayer("Water");
        solid = Physics.DefaultRaycastLayers;
        if (playerLayer >= 0) solid &= ~(1 << playerLayer);

        AddType(typeof(LavaKill));
        AddType(typeof(DangerZone));
        AddType(typeof(RiseKill));
        AddType(typeof(SwingingAxeTrap));
        AddType(typeof(RatSwarm));
        AddType(typeof(FallDeath));
        AddType(typeof(ChromaDropProbe).Assembly.GetType("A_PlayerDeath_WaterSection"));

        MapVolumes(typeof(LavaKill));
        MapVolumes(typeof(DangerZone));
        MapVolumes(typeof(RiseKill));
        MapAxes();
        MapRats();

        // السقوط تحت حدٍّ موت (FallDeath على اللاعب): لا قطرة قريبًا من ذلك الحدّ
        FallDeath fall = body != null ? body.GetComponentInChildren<FallDeath>(true) : null;
        if (fall == null) fall = Object.FindAnyObjectByType<FallDeath>();
        if (fall != null && Read(fall, "killY", out float killY)) floorY = killY + 1.5f;
    }

    // ---------- الأسئلة ----------

    /// <summary>
    /// أقرب أرضٍ تحت <paramref name="at"/> بين الارتفاعين. الشعاع يبدأ من الأعلى فلا
    /// يرى سطحًا فوقه — فالقطرة لا تصعد أعلى مما يُقفز إليه. وأوّل ما يصيبه يُحكم عليه:
    /// إن كان منحدرًا لا يُمشى فلا أرض، ولا نبحث تحته.
    /// </summary>
    public bool Ground(Vector3 at, float top, float bottom, out RaycastHit ground)
    {
        ground = default;
        float length = top - bottom;
        if (length <= 0f) return false;

        var origin = new Vector3(at.x, top, at.z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, length, solid, QueryTriggerInteraction.Ignore);

        int pick = -1;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hits[i];
            if (h.distance >= best || IsPlayer(h.collider)) continue;

            // صندوقٌ يُدفع أو جسمٌ يسقط ليس أرضًا: يذهب وتبقى القطرة معلّقة في الهواء
            Rigidbody rb = h.rigidbody;
            if (rb != null && !rb.isKinematic) continue;

            best = h.distance;
            pick = i;
        }

        if (pick < 0) return false;
        ground = hits[pick];
        return ground.normal.y >= Walkable;
    }

    /// <summary>هل فوق الأرض متّسعٌ لرأس اللاعب؟ لا قطرةٌ تحت درجٍ أو داخل عمود.</summary>
    public bool Clear(Vector3 ground)
    {
        Vector3 a = ground + Vector3.up * ClearFrom;
        Vector3 b = ground + Vector3.up * ClearTo;
        int n = Physics.OverlapCapsuleNonAlloc(a, b, ClearRadius, touching, solid, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (touching[i] != null && !IsPlayer(touching[i])) return false;
        return true;
    }

    /// <summary>خطّ نظرٍ بين نقطتين لا يقطعه شيءٌ صلب — المسار لا يمرّ عبر الجدران.</summary>
    public bool Sight(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float length = d.magnitude;
        if (length < 0.01f) return true;

        int n = Physics.RaycastNonAlloc(from, d / length, hits, length, solid, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (!IsPlayer(hits[i].collider)) return false;
        return true;
    }

    /// <summary>هل القطرة أو أرضها قرب ما يقتل؟</summary>
    public bool Hazard(Vector3 drop, Vector3 ground)
    {
        if (ground.y < floorY) return true;

        Vector3 foot = ground + Vector3.up * 0.3f;
        for (int i = 0; i < boxes.Count; i++)
            if (boxes[i].Contains(drop) || boxes[i].Contains(foot)) return true;

        for (int i = 0; i < spheres.Count; i++)
        {
            Vector4 s = spheres[i];
            var c = new Vector3(s.x, s.y, s.z);
            float r2 = s.w * s.w;
            if ((drop - c).sqrMagnitude < r2 || (foot - c).sqrMagnitude < r2) return true;
        }

        return Touches(drop) || Touches(foot);
    }

    /// <summary>هل الكولايدر من جسم اللاعب؟</summary>
    public bool IsPlayer(Collider c)
    {
        if (c == null) return false;
        if (playerLayer >= 0 && c.gameObject.layer == playerLayer) return true;
        return body != null && c.transform.IsChildOf(body);
    }

    // ---------- الخطر ----------

    private bool Touches(Vector3 at)
    {
        int n = Physics.OverlapSphereNonAlloc(at, HazardReach, touching, Physics.AllLayers,
                                              QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            Collider c = touching[i];
            if (c == null || IsPlayer(c)) continue;
            if (IsHazard(c)) return true;
        }
        return false;
    }

    /// <summary>
    /// الحكم يُحفظ لكل كولايدر: أرض المرحلة الكبيرة تلمسها كل قطرة، واسم الكائن يُنسخ
    /// نصًّا جديدًا مع كل قراءة — فلا نقرؤه إلا مرّة.
    /// </summary>
    private bool IsHazard(Collider c)
    {
        if (verdicts.TryGetValue(c, out bool known)) return known;

        bool hazard = water >= 0 && c.gameObject.layer == water;
        if (!hazard) hazard = Named(c.transform) || (c.transform.parent != null && Named(c.transform.parent));
        for (int i = 0; !hazard && i < hazardTypes.Count; i++)
            hazard = c.GetComponentInParent(hazardTypes[i]) != null;

        verdicts[c] = hazard;
        return hazard;
    }

    private static bool Named(Transform t)
    {
        string name = t.name;
        foreach (string word in HazardWords)
            if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private void AddType(Type type)
    {
        if (type != null && typeof(Component).IsAssignableFrom(type)) hazardTypes.Add(type);
    }

    /// <summary>
    /// حدود كولايدرات القتل محسوبةٌ من أشكالها لا من الفيزياء: المطفأ منها لا تراه
    /// الفيزياء أصلًا، وهو الذي يُشعَل بعد قليل تحت قدم من صدّق القطرة.
    /// </summary>
    private void MapVolumes(Type type)
    {
        foreach (Object o in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(o is Component owner) || owner == null) continue;
            foreach (Collider c in owner.GetComponentsInChildren<Collider>(true))
            {
                if (!Box(c, out Bounds b)) continue;
                b.Expand(HazardReach * 2f);
                boxes.Add(b);
            }
        }
    }

    /// <summary>الفاس يتأرجح: كرةٌ حول محوره بطول ذراعه، لا حدودُه في لحظةٍ من التأرجح.</summary>
    private void MapAxes()
    {
        foreach (SwingingAxeTrap axe in Object.FindObjectsByType<SwingingAxeTrap>(FindObjectsInactive.Include,
                                                                                   FindObjectsSortMode.None))
        {
            if (axe == null) continue;
            Vector3 pivot = axe.transform.position;
            float reach = 1.5f;
            foreach (Collider c in axe.GetComponentsInChildren<Collider>(true))
            {
                if (!Box(c, out Bounds b)) continue;
                reach = Mathf.Max(reach, Vector3.Distance(pivot, b.center) + b.extents.magnitude);
            }
            spheres.Add(new Vector4(pivot.x, pivot.y, pivot.z, Mathf.Min(reach, 10f) + HazardReach));
        }
    }

    /// <summary>
    /// أرض الفئران تريغرٌ منفصل لا يحمل اسمًا ولا سكربتًا يدلّ عليه — والسرب يحتفظ به
    /// في حقلٍ خاص، فنقرؤه منه. وسربٌ بلا أرضٍ محدّدة: دائرته حول مركزه.
    /// </summary>
    private void MapRats()
    {
        foreach (RatSwarm swarm in Object.FindObjectsByType<RatSwarm>(FindObjectsInactive.Include,
                                                                      FindObjectsSortMode.None))
        {
            if (swarm == null) continue;

            if (Read(swarm, "territoryArea", out Collider area) && area != null && Box(area, out Bounds b))
            {
                b.Expand(new Vector3(HazardReach * 2f, 4f, HazardReach * 2f));
                boxes.Add(b);
                continue;
            }

            float radius = Read(swarm, "territoryRadius", out float r) ? Mathf.Clamp(r, 3f, 12f) : 8f;
            Vector3 p = swarm.transform.position;
            spheres.Add(new Vector4(p.x, p.y, p.z, radius));
        }
    }

    /// <summary>
    /// حدود الكولايدر في العالم من شكله وموضعه — تصحّ والكائن مطفأ، حين تُرجع
    /// <c>Collider.bounds</c> صندوقًا فارغًا.
    /// </summary>
    private static bool Box(Collider c, out Bounds world)
    {
        world = default;
        if (c == null) return false;

        Bounds local;
        switch (c)
        {
            case BoxCollider box: local = new Bounds(box.center, box.size); break;
            case SphereCollider sphere: local = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f)); break;
            case CapsuleCollider capsule:
            {
                var size = Vector3.one * (capsule.radius * 2f);
                size[Mathf.Clamp(capsule.direction, 0, 2)] = Mathf.Max(capsule.height, capsule.radius * 2f);
                local = new Bounds(capsule.center, size);
                break;
            }
            case MeshCollider mesh when mesh.sharedMesh != null: local = mesh.sharedMesh.bounds; break;
            default:
                if (!c.enabled || !c.gameObject.activeInHierarchy) return false;
                world = c.bounds;
                return true;
        }

        Matrix4x4 m = c.transform.localToWorldMatrix;
        Vector3 min = local.min, max = local.max;
        world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                                     (i & 2) == 0 ? min.y : max.y,
                                     (i & 4) == 0 ? min.z : max.z);
            world.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return true;
    }

    /// <summary>
    /// حقلٌ خاص بالاسم — ما غاب أو تغيّر نوعه يرجع false ولا يرمي. لإعداداتٍ في سكربتاتٍ
    /// لا تكشفها (أرض الفئران، وجهة البوابة، مداخل الهب): قراءتها خيرٌ من تعديل صاحبها.
    /// </summary>
    internal static bool Read<T>(object owner, string field, out T value)
    {
        value = default;
        try
        {
            FieldInfo info = owner.GetType().GetField(field, Fields);
            if (info == null || !(info.GetValue(owner) is T v)) return false;
            value = v;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

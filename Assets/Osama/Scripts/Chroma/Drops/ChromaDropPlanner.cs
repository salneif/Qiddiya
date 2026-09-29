using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// أين توضع القطرات في مرحلةٍ لم يرسمها أحدٌ لها: مسارٌ بين معالم المرحلة — بداية
/// اللاعب ونقاط الحفظ والأعلام وقواعدها والبوابات وأبواب النقل ووجهاتها — وقطرةٌ كل
/// ١٫٦ م على كل مقطع، وحلقةٌ صغيرة حول كل نقطة حفظ، وثلاث قطراتٍ ذهبية خارج الطريق بقليل.
///
/// <b>المعرّف ثابتٌ بين الزيارات</b>، وإلا عادت قطرةٌ جُمعت أو اختفت أخرى لم تُجمع.
/// فالمعالم تُلتقط لحظة تحميل السين (<see cref="Capture"/>) قبل أن يحرّك <c>Start</c>
/// شيئًا — قبل أن ينقل <c>PlayerSpawnRouter</c> اللاعب أو يُخفي <c>FlagCarry</c> علمًا —
/// ويُرقَّم كل موضعٍ ممكن على المسار <b>قبل</b> سؤال الفيزياء عنه. الفيزياء تقرّر أيّ
/// المواضع تُملأ، لا أرقامها: بوابةٌ فُتحت منذ الزيارة الماضية لا تُزيح رقمًا.
///
/// <b>المسار شجرة لا سلسلة</b>: كل معلمٍ يُربط بأقرب معلمٍ سبق ربطه، بدءًا من اللاعب
/// (أقرب جارٍ ينمو من البداية). السلسلة كانت تقفز في السيرك من البوابة إلى العلم عبر
/// المرحلة كلها، والشجرة تربط الكواليس ببعضها ثم بالبداية — ترتيب المرحلة نفسه.
///
/// <b>وباب النقل ووجهته وصلٌ بلا مشي</b> (خيام السيرك، ممرّات ستيم): مناطق لا يصل بينها
/// إلا بابٌ ينقل كانت الشجرة تربطها بخطٍّ مستقيم عبر الجدران — فخرج من بداية السيرك
/// أثران إلى المدرّجات، وبقي الباب الوحيد المفتوح بلا أثر.
///
/// وكل مقطع يُمشى من طرفيه نحو منتصفه: الجدار الذي يقطع المقطع لا يحرم إلا ما بعده،
/// فيبقى عند كل معلمٍ أثرٌ يدلّ على الذي بعده.
/// </summary>
public sealed class ChromaDropPlanner
{
    /// <summary>بين قطرتين على المسار.</summary>
    public const float Spacing = 1.6f;

    /// <summary>أقلّ بُعد بين أيّ قطرتين.</summary>
    private const float MinGap = 1.2f;

    /// <summary>سقف قطرات المسار في السين (الحلقات والذهبية فوقه).</summary>
    public const int RouteCap = 70;

    /// <summary>أبعد تفريقٍ للمسار حين يفوق السقف: قطرةٌ من كل هذا العدد.</summary>
    private const int MaxStride = 4;

    public const int RingSize = 6;
    private const float RingRadius = 2.4f;

    public const int GoldenCount = 3;
    private const float GoldenNear = 2.6f, GoldenFar = 4.6f;

    /// <summary>الذهبية بعيدةٌ عن غيرها: جائزةٌ تُقصد، لا حبّةٌ في الصفّ.</summary>
    private const float GoldenGap = 2.2f;

    /// <summary>أعلى ما تصعده القطرة التالية — درجةٌ أو حافّةٌ يُقفز إليها.</summary>
    private const float Climb = 1.3f;

    /// <summary>أبعد ما تنزله: ما تحته أكثر من هذا حفرةٌ أو هاوية، فيقف المسار.</summary>
    private const float Descent = 3.5f;

    /// <summary>معلمان أقرب من هذا معلمٌ واحد (العلم فوق قاعدته، اللاعب داخل نقطة حفظ).</summary>
    private const float MergeFlat = 2.5f, MergeTall = 3f;

    /// <summary>
    /// مواضع تُسأل عنها الفيزياء في الإطار الواحد (خمسة أسئلةٍ لكلٍّ منها) — التوليد يمتدّ
    /// على إطاراتٍ بلا تهنيق، واللاعب يمشي أوّل المرحلة ولا يحسّ به.
    /// </summary>
    private const int ProbesPerFrame = 12;

    /// <summary>
    /// أوّل نظرةٍ من المعلم أعلى من القطرة بنصف متر: المعلم قد يقف على قاعدةٍ أو عتبة،
    /// والخطّ من ارتفاع القطرة يحتكّ بحافّتها فيُقتل الأثر قبل أن يبدأ.
    /// </summary>
    private const float AnchorEye = 0.5f;

    private const string MenuScene = "Hub-Menu";

    /// <summary>موضعٌ اختير لقطرة.</summary>
    public sealed class Spot
    {
        public int slot;          // يُبنى منه المعرّف: "<السين>:<slot>"
        public Vector3 ground;
        public Collider floor;
        public Vector3 local;     // الأرض بإحداثيّات أرضها لحظة الفحص — المركب يمشي والتخطيط يطول
        public int colour;        // رقمٌ في لوحة الألوان؛ الذهبية تتجاهله
        public bool golden;
        public float phase;       // طور التمايل: الأثر يتموّج من معلمه للخارج
        public int trail = -1;    // أثرٌ على المسار؛ -1 للحلقات والذهبية
        public int rank;          // ترتيبه بين المقبول في أثره
    }

    /// <summary>ما يُلتقط لحظة تحميل السين.</summary>
    public sealed class Layout
    {
        public readonly List<Vector3> anchors = new List<Vector3>();
        public readonly List<Vector3> checkpoints = new List<Vector3>();
        public readonly List<Vector2Int> jumps = new List<Vector2Int>();   // بابٌ ينقل: رقما طرفيه في anchors
        public bool hasStart;
    }

    // ---------- الالتقاط ----------

    /// <summary>
    /// معالم السين كما وُضعت فيه. يُنادى عند تحميل السين، قبل أول <c>Start</c>.
    /// الترتيب محدَّدٌ بالنوع ثم بالموضع — لا بترتيب البحث، فهو غير مضمون.
    /// </summary>
    public static Layout Capture()
    {
        var layout = new Layout();

        GameObject player = PlayerLocator.Find("Player");
        if (player != null)
        {
            layout.anchors.Add(Body(player).position);
            layout.hasStart = true;
        }

        // بدايات الهب الأخرى: حيث يظهر اللاعب عائدًا من كل مرحلة، أمام بابها. منها
        // يمتدّ أثرٌ إلى القاعدة التي جاء ليزرع فيها
        var starts = new List<Vector3>();
        foreach (PlayerSpawnRouter router in Object.FindObjectsByType<PlayerSpawnRouter>(FindObjectsSortMode.None))
        {
            if (ChromaDropProbe.Read(router, "entries", out PlayerSpawnRouter.Entry[] entries))
                foreach (PlayerSpawnRouter.Entry e in entries)
                    if (e != null && e.point != null) starts.Add(e.point.position);
            if (ChromaDropProbe.Read(router, "defaultPoint", out Transform point) && point != null)
                starts.Add(point.position);
        }
        starts.Sort(Compare);
        foreach (Vector3 s in starts) Merge(layout.anchors, s);

        foreach (Checkpoint c in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            layout.checkpoints.Add(c.transform.position);
        AddByName(layout.checkpoints, "A_CheckPoint");
        layout.checkpoints.Sort(Compare);

        var rest = new List<Vector3>();
        foreach (FlagItem f in Object.FindObjectsByType<FlagItem>(FindObjectsSortMode.None)) rest.Add(f.transform.position);
        foreach (FlagCarry f in Object.FindObjectsByType<FlagCarry>(FindObjectsSortMode.None)) rest.Add(f.transform.position);
        foreach (ExternalFlagPickup f in Object.FindObjectsByType<ExternalFlagPickup>(FindObjectsSortMode.None))
            rest.Add(f.transform.position);
        rest.Sort(Compare);
        int flags = rest.Count;

        foreach (FlagBase b in Object.FindObjectsByType<FlagBase>(FindObjectsSortMode.None)) rest.Add(b.transform.position);
        rest.Sort(flags, rest.Count - flags, Comparer<Vector3>.Create(Compare));
        int bases = rest.Count;

        // بوابة القائمة ليست وجهةً في اللعب: مسارٌ نحوها يدعو اللاعب للخروج من اللعبة
        foreach (LevelPortal p in Object.FindObjectsByType<LevelPortal>(FindObjectsSortMode.None))
        {
            if (ChromaDropProbe.Read(p, "sceneName", out string scene) && scene == MenuScene) continue;
            rest.Add(p.transform.position);
        }
        rest.Sort(bases, rest.Count - bases, Comparer<Vector3>.Create(Compare));

        foreach (Vector3 c in layout.checkpoints) Merge(layout.anchors, c);
        foreach (Vector3 r in rest) Merge(layout.anchors, r);
        AddTeleports(layout);
        return layout;
    }

    /// <summary>جسم اللاعب: الكائن الذي يحمل الـ CharacterController، لا كولايدرًا ابنًا موسومًا.</summary>
    public static Transform Body(GameObject player)
    {
        var controller = player.GetComponentInParent<CharacterController>();
        return controller != null ? controller.transform : player.transform;
    }

    private static void AddByName(List<Vector3> into, string typeName)
    {
        Type type = typeof(ChromaDropPlanner).Assembly.GetType(typeName);
        if (type == null) return;
        foreach (Object o in Object.FindObjectsByType(type, FindObjectsSortMode.None))
            if (o is Component c && c != null) into.Add(c.transform.position);
    }

    /// <summary>
    /// أبواب النقل (<c>TeleportTent</c>) ووجهاتها معالم، وكل بابٍ مع وجهته وصلٌ في
    /// <see cref="Tree"/>. والمطفأة منها أيضًا: أبواب السيرك الأخرى تُفتح بعد العلم.
    /// </summary>
    private static void AddTeleports(Layout layout)
    {
        Type type = typeof(ChromaDropPlanner).Assembly.GetType("TeleportTent");
        if (type == null) return;

        var doors = new List<Component>();
        foreach (Object o in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (o is Component c && c != null) doors.Add(c);
        doors.Sort((a, b) => Compare(a.transform.position, b.transform.position));

        foreach (Component door in doors)
        {
            if (!ChromaDropProbe.Read(door, "destination", out Transform to) || to == null) continue;
            int from = Merge(layout.anchors, door.transform.position);
            int into = Merge(layout.anchors, to.position);
            if (from != into) layout.jumps.Add(new Vector2Int(from, into));
        }
    }

    /// <summary>يضيف المعلم إلا إن قاربه معلمٌ سبقه، ويرجع رقم الباقي منهما.</summary>
    private static int Merge(List<Vector3> anchors, Vector3 point)
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            Vector3 a = anchors[i];
            Vector2 flat = new Vector2(a.x - point.x, a.z - point.z);
            if (flat.sqrMagnitude < MergeFlat * MergeFlat && Mathf.Abs(a.y - point.y) < MergeTall) return i;
        }
        anchors.Add(point);
        return anchors.Count - 1;
    }

    private static int Compare(Vector3 a, Vector3 b)
    {
        int c = a.x.CompareTo(b.x);
        if (c == 0) c = a.z.CompareTo(b.z);
        if (c == 0) c = a.y.CompareTo(b.y);
        return c;
    }

    // ---------- التخطيط ----------

    private readonly ChromaDropProbe probe;
    private readonly List<Spot> accepted = new List<Spot>();
    private int work;

    // لماذا وقفت الآثار — تُطبع سطرًا واحدًا لكل سين
    private int anchorCount, doorCount, edgeCount, stride = 1, noGround, walls, hazards, cramped;

    public ChromaDropPlanner(ChromaDropProbe probe) => this.probe = probe;

    /// <summary>المسار أخذ قطرةً من كل كم (١ = بلا تفريق) — يُقرأ بعد <see cref="Plan"/>.</summary>
    public int Stride => stride;

    /// <summary>
    /// سطرٌ يقول ما حدث: كم معلمًا، وكم قطرةً من كل نوع، ولماذا وقفت الآثار. اقرأه قبل
    /// أن تخمّن لماذا مقطعٌ فارغ — «جدار» يعني أن الطريق ليس خطًّا مستقيمًا هناك.
    /// </summary>
    public string Summary(string scene)
    {
        int route = 0, ring = 0, gold = 0;
        foreach (Spot s in accepted)
        {
            if (s.golden) gold++;
            else if (s.trail >= 0) route++;
            else ring++;
        }
        return $"[ChromaDrops] {scene}: {anchorCount} معالم، {doorCount} أبواب نقل، {edgeCount} مقاطع — مسار {route}" +
               (stride > 1 ? $" (واحدة من كل {stride})" : "") + $"، حلقات {ring}، ذهبية {gold}. " +
               $"وقف الأثر: بلا أرض {noGround}، جدار {walls}، خطر {hazards}. مواضع تُخطّيت: {cramped}.";
    }

    /// <summary>
    /// يملأ <paramref name="result"/> بالمواضع المقبولة، على إطاراتٍ متتالية.
    /// <paramref name="start"/> بديل البداية إن لم يوجد لاعبٌ لحظة الالتقاط.
    /// </summary>
    public IEnumerator Plan(string scene, Layout layout, Vector3 start, List<Spot> result)
    {
        result.Clear();
        accepted.Clear();
        work = 0;
        uint seed = Hash(scene);

        var anchors = new List<Vector3>(layout.anchors);
        var jumps = new List<Vector2Int>(layout.jumps);
        if (!layout.hasStart)
        {
            // البداية أوّل المعالم دائمًا، فأرقام أطراف الأبواب تتأخّر معها
            anchors.Insert(0, start);
            for (int i = 0; i < jumps.Count; i++) jumps[i] += Vector2Int.one;
        }

        int count = anchors.Count;
        var grounds = new Vector3[count];
        var grounded = new bool[count];
        for (int i = 0; i < count; i++) grounded[i] = Settle(anchors[i], out grounds[i]);

        List<Vector2Int> edges = Tree(anchors, jumps);
        anchorCount = count;
        doorCount = jumps.Count;
        edgeCount = edges.Count;

        // ١) المسار: كل مقطعٍ من طرفيه
        int slot = 0, trail = 0;
        foreach (Vector2Int edge in edges)
        {
            Vector3 a = anchors[edge.x], b = anchors[edge.y];
            Vector3 span = b - a;
            span.y = 0f;
            int inner = Mathf.Max(0, Mathf.RoundToInt(span.magnitude / Spacing) - 1);
            int half = (inner + 1) / 2;

            IEnumerator there = Walk(trail++, slot, a, span, inner, half, grounded[edge.x], grounds[edge.x]);
            while (there.MoveNext()) yield return null;

            IEnumerator back = Walk(trail++, slot + half, b, -span, inner, inner - half, grounded[edge.y], grounds[edge.y]);
            while (back.MoveNext()) yield return null;

            slot += inner;
        }

        Cap(trail);

        // ٢) حلقات نقاط الحفظ
        for (int c = 0; c < layout.checkpoints.Count; c++)
        {
            IEnumerator ring = Ring(seed, c, slot + c * RingSize, layout.checkpoints[c]);
            while (ring.MoveNext()) yield return null;
        }
        slot += layout.checkpoints.Count * RingSize;

        // ٣) الذهبية
        IEnumerator gold = Golden(seed, slot);
        while (gold.MoveNext()) yield return null;

        result.AddRange(accepted);
    }

    /// <summary>
    /// أثرٌ من معلمٍ نحو جاره. يقف عند أوّل ما يمنع السير — لا أرض، أو جدار، أو خطر —
    /// ويتخطّى ما يعيق قطرةً وحدها (عمودٌ، سقفٌ منخفض، قطرةٌ قريبة) ويكمل.
    /// </summary>
    private IEnumerator Walk(int trail, int firstSlot, Vector3 origin, Vector3 span, int inner, int steps,
                             bool hasGround, Vector3 ground)
    {
        if (!hasGround || steps <= 0) yield break;

        float level = ground.y;
        Vector3 last = ground + Vector3.up * (ChromaDropProbe.Hover + AnchorEye);
        int rank = 0;

        for (int k = 1; k <= steps; k++)
        {
            if (Busy()) yield return null;

            Vector3 at = origin + span * (k / (float)(inner + 1));
            if (!probe.Ground(at, level + Climb, level - Descent, out RaycastHit hit)) { noGround++; yield break; }

            Vector3 drop = hit.point + Vector3.up * ChromaDropProbe.Hover;
            if (!probe.Sight(last, drop)) { walls++; yield break; }
            if (probe.Hazard(drop, hit.point)) { hazards++; yield break; }
            if (!probe.Clear(hit.point) || Crowded(drop, MinGap)) { cramped++; continue; }

            accepted.Add(new Spot
            {
                slot = firstSlot + k - 1, ground = hit.point, floor = hit.collider, local = Local(hit),
                colour = k - 1, phase = -0.6f * k, trail = trail, rank = rank++,
            });
            level = hit.point.y;
            last = drop;
        }
    }

    /// <summary>
    /// سقف المسار بالتفريق لا بالقصّ: مرحلةٌ طويلة مفتوحة تُبقي قطرةً من كل اثنتين أو
    /// ثلاث على طول الطريق كلّه، بدل أن يبتلع السقفَ أوّلُ مقطعٍ ويبقى الباقي فارغًا.
    /// والفاصل يبقى قصيرًا يلحقه الجري (٣٫٢–٤٫٨ م)، فالسلسلة لا تنقطع واللحن يكمل.
    /// أوّل قطرةٍ في كل أثر — عند معلمه — تبقى دائمًا.
    /// </summary>
    private void Cap(int trails)
    {
        if (trails == 0) return;

        var sizes = new int[trails];
        int total = 0;
        foreach (Spot s in accepted)
        {
            if (s.trail < 0) continue;
            sizes[s.trail]++;
            total++;
        }

        if (total > RouteCap)
        {
            stride = 2;
            for (; stride < MaxStride; stride++)
            {
                int sum = 0;
                foreach (int n in sizes) sum += (n + stride - 1) / stride;
                if (sum <= RouteCap) break;
            }
            accepted.RemoveAll(s => s.trail >= 0 && s.rank % stride != 0);

            // آثارٌ بالعشرات تفوق السقف حتى بعد التفريق: يسقط الأبعد عن معلمه أوّلًا
            int over = -RouteCap;
            foreach (Spot s in accepted) if (s.trail >= 0) over++;
            while (over-- > 0) accepted.Remove(Farthest());
        }

        // قوس قزح يُعدّ من جديد على ما بقي: الألوان متتالية مهما فُرّق الأثر
        var next = new int[trails];
        foreach (Spot s in accepted)
            if (s.trail >= 0) s.colour = next[s.trail]++;
    }

    private Spot Farthest()
    {
        Spot worst = null;
        foreach (Spot s in accepted)
        {
            if (s.trail < 0) continue;
            if (worst == null || s.rank > worst.rank || (s.rank == worst.rank && s.slot > worst.slot)) worst = s;
        }
        return worst;
    }

    /// <summary>ستّ قطراتٍ حول نقطة الحفظ، لا تعبر جدارًا من مركزها.</summary>
    private IEnumerator Ring(uint seed, int index, int firstSlot, Vector3 point)
    {
        if (!Settle(point, out Vector3 centre)) yield break;

        Vector3 middle = centre + Vector3.up * (ChromaDropProbe.Hover + AnchorEye);
        float turn = Random01(seed, 101, index) * 60f;

        for (int j = 0; j < RingSize; j++)
        {
            if (Busy()) yield return null;

            float angle = (turn + j * 60f) * Mathf.Deg2Rad;
            Vector3 at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RingRadius;
            if (!probe.Ground(at, centre.y + Climb, centre.y - Descent, out RaycastHit hit)) continue;

            Vector3 drop = hit.point + Vector3.up * ChromaDropProbe.Hover;
            if (!probe.Sight(middle, drop) || probe.Hazard(drop, hit.point) ||
                !probe.Clear(hit.point) || Crowded(drop, MinGap)) continue;

            accepted.Add(new Spot
            {
                slot = firstSlot + j, ground = hit.point, floor = hit.collider, local = Local(hit),
                colour = j, phase = j * 1.05f,
            });
        }
    }

    /// <summary>
    /// ثلاثٌ موزّعة على طول المسار، كلٌّ بضعة أمتار عن قطرةٍ فيه — تُرى منها، ويُمشى
    /// إليها، ولا خطر حولها. إن فشلت قاعدةٌ جُرّبت جاراتها.
    /// </summary>
    private IEnumerator Golden(uint seed, int firstSlot)
    {
        var bases = new List<Spot>();
        foreach (Spot s in accepted) if (s.trail >= 0) bases.Add(s);
        if (bases.Count == 0) bases.AddRange(accepted);
        if (bases.Count == 0) yield break;
        bases.Sort((x, y) => x.slot.CompareTo(y.slot));

        for (int k = 0; k < GoldenCount; k++)
        {
            int centre = Mathf.Clamp(Mathf.FloorToInt((k + 0.5f) / GoldenCount * bases.Count), 0, bases.Count - 1);
            bool placed = false;

            for (int b = 0; b < 5 && !placed; b++)
            {
                int index = centre + (b % 2 == 0 ? b / 2 : -(b + 1) / 2);
                if (index < 0 || index >= bases.Count) continue;
                Spot home = bases[index];
                Vector3 from = home.ground + Vector3.up * ChromaDropProbe.Hover;

                for (int attempt = 0; attempt < 8 && !placed; attempt++)
                {
                    if (Busy()) yield return null;

                    int n = b * 8 + attempt;
                    float angle = Random01(seed, 200 + k, n) * Mathf.PI * 2f;
                    float distance = Mathf.Lerp(GoldenNear, GoldenFar, Random01(seed, 300 + k, n));
                    Vector3 at = home.ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (!probe.Ground(at, home.ground.y + Climb, home.ground.y - Descent, out RaycastHit hit)) continue;

                    Vector3 drop = hit.point + Vector3.up * ChromaDropProbe.Hover;
                    if (!probe.Sight(from, drop) || probe.Hazard(drop, hit.point) ||
                        !probe.Clear(hit.point) || Crowded(drop, GoldenGap)) continue;

                    accepted.Add(new Spot
                    {
                        slot = firstSlot + k, ground = hit.point, floor = hit.collider, local = Local(hit),
                        golden = true, phase = k * 2.1f,
                    });
                    placed = true;
                }
            }
        }
    }

    // ---------- أدوات ----------

    /// <summary>أرض المعلم: تحته مباشرةً، ولو كان علمًا على عمود أو بوابةً معلّقة.</summary>
    private bool Settle(Vector3 point, out Vector3 ground)
    {
        ground = point;
        if (!probe.Ground(point, point.y + 1f, point.y - 8f, out RaycastHit hit)) return false;
        ground = hit.point;
        return true;
    }

    /// <summary>
    /// موضع الإصابة بإحداثيّات ما أصابته، لحظتها: التخطيط يمتدّ على إطارات، والقارب الذي
    /// فُحص في أوّلها يكون قد مشى قبل أن تولد قطرته.
    /// </summary>
    private static Vector3 Local(RaycastHit hit) => hit.collider.transform.InverseTransformPoint(hit.point);

    private bool Crowded(Vector3 drop, float gap)
    {
        float g2 = gap * gap;
        foreach (Spot s in accepted)
            if ((s.ground + Vector3.up * ChromaDropProbe.Hover - drop).sqrMagnitude < g2) return true;
        return false;
    }

    private bool Busy() => ++work % ProbesPerFrame == 0;

    /// <summary>
    /// أقرب جارٍ ينمو من البداية (Prim): كل معلمٍ يُربط بأقرب ما رُبط قبله. التعادل
    /// للأصغر رقمًا فالشجرة واحدةٌ في كل زيارة.
    ///
    /// والباب ووجهته (<paramref name="jumps"/>) بلا طول: من بلغ أحدهما بلغ الآخر قبل أيّ
    /// مشي، ولا يُرجع بينهما مقطع — المقاطع المرجَعة كلها تُمشى.
    /// </summary>
    private static List<Vector2Int> Tree(List<Vector3> points, List<Vector2Int> jumps)
    {
        int n = points.Count;
        var edges = new List<Vector2Int>(Mathf.Max(0, n - 1));
        if (n < 2) return edges;

        var linked = new bool[n];
        var best = new float[n];
        var from = new int[n];
        var hop = new bool[n];      // وصلته وجهةُ بابٍ لا مشي

        void Link(int node)
        {
            linked[node] = true;
            for (int i = 0; i < n; i++)
            {
                if (linked[i]) continue;
                float d = (points[i] - points[node]).sqrMagnitude;
                if (d < best[i]) { best[i] = d; from[i] = node; hop[i] = false; }
            }
            foreach (Vector2Int j in jumps)
            {
                int other = j.x == node ? j.y : j.y == node ? j.x : -1;
                if (other < 0 || linked[other]) continue;
                best[other] = 0f;
                from[other] = node;
                hop[other] = true;
            }
        }

        for (int i = 1; i < n; i++) best[i] = float.MaxValue;
        Link(0);

        for (int added = 1; added < n; added++)
        {
            int pick = -1;
            for (int i = 1; i < n; i++)
                if (!linked[i] && (pick < 0 || best[i] < best[pick])) pick = i;

            if (!hop[pick]) edges.Add(new Vector2Int(from[pick], pick));
            Link(pick);
        }
        return edges;
    }

    /// <summary>بصمة اسم السين (FNV-1a) — ثابتةٌ بين التشغيلات بخلاف <c>GetHashCode</c>.</summary>
    private static uint Hash(string text)
    {
        uint h = 2166136261u;
        foreach (char ch in text)
        {
            h ^= ch;
            h *= 16777619u;
        }
        return h;
    }

    /// <summary>رقمٌ «عشوائيّ» بين ٠ و١ يتحدّد كليًّا بمدخلاته — الموضع نفسه في كل زيارة.</summary>
    private static float Random01(uint seed, int a, int b)
    {
        uint h = seed ^ (uint)(a * 73856093) ^ (uint)(b * 19349663);
        h ^= h >> 16;
        h *= 0x7feb352du;
        h ^= h >> 15;
        h *= 0x846ca68bu;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / 16777216f;
    }
}

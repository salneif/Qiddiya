using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>قطرات اللون</b> في كل مرحلة: أثرٌ من كراتٍ مضيئة يدلّ على الطريق، يُجذب إلى صدر
/// اللاعب حين يقترب، ويُعزف لحنًا صاعدًا كلّما التقط صفًّا منه بلا توقّف.
///
/// <list type="bullet">
/// <item><b>الموضوعة</b>: يرسمها <see cref="ChromaDropPlanner"/> بعد أن يستقرّ اللاعب في
/// مكانه وتختفي شاشة التحميل، وتظهر موجةً تمشي من اللاعب للخارج. ما جُمع منها في
/// هذه اللعبة لا يعود (<see cref="ChromaBank.IsCollected"/>).</item>
/// <item><b>دفعات الأحداث</b>: نقطة حفظ، بوابة، لغز، علم — قطراتٌ تنفجر من مكان الحدث
/// (أو من اللاعب إن كان الحدث أبعد من مداه) وتسقط حوله، ثم تنجذب وحدها بعد ٤ ث إن كان
/// اللاعب قريبًا. كل مكانٍ يدفع مرّةً في اللعبة. ولا تنتقل مع اللاعب لسينٍ آخر: ما بقي
/// منها حوله حين يغادر يُحسب له، فلا يضيع منها شيء.</item>
/// </list>
///
/// <b>في العالم الرمادي</b> تُرى القطرة البعيدة ضوءًا أبيض، وأقرب ستٍّ منها تتفتّح
/// ملوّنة (<see cref="ChromaDropZones"/>). وفي المراحل الملوّنة أصلًا (ستيم والتوايلايت)
/// تبقى بألوانها كاملةً.
///
/// <b>ويصمت كلّه</b> حين <see cref="ChromaEvents.Quiet"/>: القائمة والانترو وشاشة التحميل
/// والكريديت — القطرات تختفي، ولا جذب ولا صوت.
///
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DefaultExecutionOrder(-60)]   // قبل ColorZones.LateUpdate (-50): مراسي الفقاعات في مواضع هذا الإطار
[DisallowMultipleComponent]
public class ChromaDropField : MonoBehaviour
{
    // ---------- الضبط ----------

    /// <summary>بعد اختفاء شاشة التحميل: لحظةٌ يستقرّ فيها اللاعب قبل رسم المسار.</summary>
    private const float SettleSeconds = 0.5f;

    /// <summary>أحداث أوّل لحظات السين تُتجاهل — استعادة حالةٍ لا إنجاز.</summary>
    private const float EventsAfter = 1.5f;

    /// <summary>مدى الجذب من صدر اللاعب.</summary>
    private const float MagnetRadius = 2.8f;

    /// <summary>طيران القطرة إلى الصدر (يطول قليلًا للبعيدة).</summary>
    private const float FlySeconds = 0.25f;

    private const float PopSeconds = 0.4f;

    /// <summary>موجة الظهور: سرعتها بالمتر في الثانية، وأطول انتظارٍ فيها.</summary>
    private const float WaveSpeed = 30f, WaveMax = 2.5f;

    /// <summary>تمايلٌ هادئ (راديان/ث). وطوره يتأخّر مع كل قطرة، فيجري على الأثر كموجة.</summary>
    private const float BobHeight = 0.07f, BobSpeed = 3.6f;

    /// <summary>أبعد من هذا لا تُحرَّك القطرة الساكنة — تمايل ٧ سم لا يُرى من ٨٠ م.</summary>
    private const float LiveRange = 80f;

    private const float SparkRange = 30f;
    private const float ZoneEvery = 0.4f;

    /// <summary>أقلّ فاصلٍ بين نبضتي لون الالتقاط — دفعةٌ كاملة لا تستهلك مخزون المناطق.</summary>
    private const float PulseGap = 0.2f;

    /// <summary>
    /// أجسادٌ تُبنى في الإطار الواحد: كل ما بعد ٧٥ م ينتظر أطول الموجة نفسه فيولد معًا —
    /// مئات الكائنات في إطارٍ واحد تهنيقةٌ أوّل كل مرحلةٍ طويلة.
    /// </summary>
    private const int PopsPerFrame = 8;

    private const int BurstLimit = 60;
    private const float BurstCollectable = 0.35f, BurstStagger = 0.045f;
    private const float BurstAuto = 1.5f, BurstReach = 25f;   // لا تضيع: من جُمِّد (بوابة النهاية) تأتيه وحدها
    // أقلّ عددًا وأثقل قيمة: دفعةٌ تُرى وتُجمع، لا مطرٌ يتقطّع له الإطار
    private const int CheckpointBurst = 3, GateBurst = 3, PuzzleBurst = 5, PickupBurst = 6;
    private const int SmallValue = 2;
    /// <summary>أجسادٌ جاهزة في المخزون قبل أوّل دفعة — لا بناء كائناتٍ لحظة الحدث.</summary>
    private const int Prewarm = 12;
    private const int PlantBurst = 5, PlantValue = 5;
    private const int GoldenValue = 10;

    /// <summary>
    /// حدثان من النوع نفسه أقرب من هذا مكانٌ واحد — بابان ينفتحان معًا، وعلمٌ يُلتقط
    /// ثانيةً في بيته بعد موتة.
    /// </summary>
    private const float SamePlace = 6f;

    // ---------- للواجهة ----------

    /// <summary>القطرات الموضوعة في هذا السين (المجموعة منها سابقًا ضمنها).</summary>
    public static int PlacedTotal { get; private set; }

    /// <summary>كم جُمع منها في هذه اللعبة.</summary>
    public static int PlacedCollected { get; private set; }

    /// <summary>طول سلسلة الالتقاط الجارية.</summary>
    public static int Combo => instance != null ? instance.song.Combo : 0;

    /// <summary>لون آخر قطرةٍ جُمعت — يُقرأ لحظة <see cref="ChromaBank.Gained"/>.</summary>
    public static Color LastColor { get; private set; } = Color.white;

    private static ChromaDropField instance;

    /// <summary>
    /// أماكن الدفعات المدفوعة في هذه اللعبة، لكل سينٍ ونوع. البوابة تُفتح كلّما اقترب
    /// اللاعب منها، والعلم يُلتقط ثانيةً بعد كل موتة، واللغز يُعاد مع البعث — والمكافأة
    /// التي تتكرّر تُحلب وتُملّ. فكل مكانٍ يدفع مرّة.
    /// </summary>
    private static readonly Dictionary<string, List<Vector3>> paid = new Dictionary<string, List<Vector3>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("ChromaDropField") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaDropField>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());   // السين الأوّل حُمّل قبل أن نشترك
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        PlacedTotal = PlacedCollected = 0;
        LastColor = Color.white;
        paid.Clear();
    }

    private readonly List<ChromaDrop> drops = new List<ChromaDrop>();
    private readonly Stack<ChromaDropView> pool = new Stack<ChromaDropView>();
    private readonly ChromaDropSong song = new ChromaDropSong();

    private ChromaDropZones zones;
    private ChromaDropProbe probe;
    private ChromaDropPlanner.Layout layout;
    private Transform stage;
    private ParticleSystem sparks, rings;
    private string sceneName = "";
    private float loadedAt;
    private bool hushed;

    private Transform body, chestBone;
    private CharacterController controller;
    private PlayerKillable killable;
    private float nextLookup;
    private Vector3 lastChest;
    private bool hadChest;

    private Camera eye;
    private float nextCamera, nextZones, nextPulse;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaEvents.CheckpointReached += OnCheckpoint;
        ChromaEvents.GateOpened += OnGate;
        ChromaEvents.PuzzleSolved += OnPuzzle;
        ChromaEvents.FlagPickedUp += OnFlagPicked;
        ChromaEvents.FlagPlanted += OnFlagPlanted;
        ChromaBank.RunReset += OnRunReset;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaEvents.CheckpointReached -= OnCheckpoint;
        ChromaEvents.GateOpened -= OnGate;
        ChromaEvents.PuzzleSolved -= OnPuzzle;
        ChromaEvents.FlagPickedUp -= OnFlagPicked;
        ChromaEvents.FlagPlanted -= OnFlagPlanted;
        ChromaBank.RunReset -= OnRunReset;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(scene);
    }

    // ---------- السين ----------

    /// <summary>
    /// سينٌ جديد يبدأ نظيفًا: ما كان اللاعب سيأخذه يُحسب له (<see cref="CreditOwed"/>)، وما
    /// سواه يذهب مع سينه (الحاوية كانت فيه). والمعالم تُلتقط الآن — قبل أوّل <c>Start</c> في السين.
    /// </summary>
    private void Begin(Scene scene)
    {
        CreditOwed();
        StopAllCoroutines();

        if (zones != null) zones.ReleaseAll();
        zones = null;
        drops.Clear();
        pool.Clear();
        if (stage != null) Destroy(stage.gameObject);
        stage = null;
        sparks = rings = null;
        probe = null;
        song.Reset();

        body = chestBone = null;
        controller = null;
        killable = null;
        hadChest = false;
        nextLookup = 0f;
        eye = null;

        PlacedTotal = PlacedCollected = 0;
        sceneName = scene.name;
        loadedAt = Time.unscaledTime;
        hushed = false;

        layout = ChromaDropPlanner.Capture();
        StartCoroutine(Generate(sceneName));
    }

    private IEnumerator Generate(string scene)
    {
        float calm = 0f;
        while (true)
        {
            calm = ChromaEvents.Quiet ? 0f : calm + Time.unscaledDeltaTime;
            if (calm >= SettleSeconds && FindPlayer()) break;
            yield return null;
        }

        if (!ChromaDropArt.Ready) yield break;
        Stage();
        if (probe == null) yield break;

        // الفيزياء لم ترَ بعد ما حرّكه Start (اللاعب، الأبواب المستعادة)
        Physics.SyncTransforms();

        var spots = new List<ChromaDropPlanner.Spot>();
        var planner = new ChromaDropPlanner(probe);
        yield return planner.Plan(scene, layout, body.position, spots);
        if (scene != sceneName || stage == null) yield break;
        Debug.Log(planner.Summary(scene));
        song.Spread(planner.Stride);

        ChromaStyle style = ChromaStyle.Get();
        Vector3 from = body != null ? body.position : Vector3.zero;
        float now = Time.time;
        int collected = 0;

        foreach (ChromaDropPlanner.Spot s in spots)
        {
            string id = scene + ":" + s.slot;
            if (ChromaBank.IsCollected(id)) { collected++; continue; }

            var d = new ChromaDrop
            {
                id = id,
                golden = s.golden,
                value = s.golden ? GoldenValue : 1,
                color = s.golden ? style.gold : style.Palette(s.colour),
                phase = s.phase,
                state = ChromaDrop.Phase.Waiting,
                since = now,
                scale = 0f,
                nextSpark = now + Random.Range(0.5f, 4f),
            };
            d.Anchor(s.ground, s.floor, s.local);
            d.position = d.rest;
            d.delay = Mathf.Min(WaveMax, Vector3.Distance(from, d.rest) / WaveSpeed);
            drops.Add(d);
        }

        PlacedTotal = spots.Count;
        PlacedCollected = collected;

        var goldenIds = new List<string>();
        foreach (ChromaDropPlanner.Spot s in spots)
            if (s.golden) goldenIds.Add(sceneName + ":" + s.slot);
        ChromaFunEvents.RaiseDropsPlanned(sceneName, spots.Count, goldenIds.ToArray());
    }

    /// <summary>حاوية السين: القطرات وجسيماتها ومراسي اللون — تموت مع السين كلّها.</summary>
    private void Stage()
    {
        if (stage == null)
        {
            var go = new GameObject("ChromaDrops") { hideFlags = HideFlags.HideInHierarchy };
            stage = go.transform;
            sparks = ChromaDropArt.Sparks(stage);
            rings = ChromaDropArt.Rings(stage);
            zones = new ChromaDropZones();
            zones.Build(stage);
            pool.Clear();
            for (int i = 0; i < Prewarm; i++) pool.Push(ChromaDropView.Create(stage));
            hushed = false;
        }

        if (probe == null && body != null) probe = new ChromaDropProbe(body);
    }

    /// <summary>
    /// اللاعب وصدره. البحث مرّةً كل نصف ثانية لا كل إطار: سيناتٌ بلا لاعبٍ أصلًا.
    /// والصدر عظمةٌ من الهيكل البشري إن وُجد — كبسولة لاعب التوايلايت مترٌ واحد
    /// ومركزها عند قدميه، فالحساب منها يضع "الصدر" عند ركبتيه.
    /// </summary>
    private bool FindPlayer()
    {
        if (body != null) return true;
        if (Time.unscaledTime < nextLookup) return false;
        nextLookup = Time.unscaledTime + 0.5f;

        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return false;

        body = ChromaDropPlanner.Body(player);
        controller = body.GetComponent<CharacterController>();
        killable = player.GetComponentInParent<PlayerKillable>();
        chestBone = null;
        hadChest = false;

        Animator animator = body.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chestBone == null) chestBone = animator.GetBoneTransform(HumanBodyBones.Spine);
        }
        return true;
    }

    private Vector3 Chest()
    {
        if (chestBone != null) return chestBone.position;
        if (controller != null)
            return controller.transform.TransformPoint(controller.center + Vector3.up * (controller.height * 0.2f));
        return body.position + Vector3.up * 1.2f;
    }

    private Camera Eye()
    {
        if (eye != null && eye.isActiveAndEnabled) return eye;
        if (Time.unscaledTime < nextCamera) return eye;
        nextCamera = Time.unscaledTime + 0.5f;
        eye = Camera.main;
        return eye;
    }

    // ---------- كل إطار ----------

    private void LateUpdate()
    {
        if (stage == null) return;

        bool hush = ChromaEvents.Quiet;
        if (hush != hushed)
        {
            hushed = hush;
            stage.gameObject.SetActive(!hush);
            if (hush && zones != null) zones.ReleaseAll();
        }
        if (hush) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;    // موقوفة: لا شيء يتحرّك، ولا صوت فوق قائمة الإيقاف
        float now = Time.time;

        if (body == null) FindPlayer();
        bool present = body != null;
        bool alive = present && (killable == null || !killable.IsDead);
        Vector3 chest = present ? Chest() : Vector3.zero;

        // قفزةٌ في إطار واحد نقلٌ لا مشي (بعثٌ أو بوابة): ما يطير يُحسب فورًا
        bool jumped = hadChest && present && (chest - lastChest).sqrMagnitude > 25f;
        // صعوده أو نزوله الآن (م/ث) — المصعد يترك قطراته خلفه، فتُشفط إليه
        float climb = hadChest && present && !jumped ? (chest.y - lastChest.y) / dt : 0f;
        lastChest = chest;
        hadChest = present;

        Camera cam = Eye();
        Quaternion facing = cam != null ? cam.transform.rotation : Quaternion.identity;
        Vector3 player = present ? body.position : chest;
        bool colourWorld = ColorZones.Available;
        int popped = 0;

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            ChromaDrop d = drops[i];
            float away = (d.rest - player).sqrMagnitude;
            bool near = away < LiveRange * LiveRange;

            switch (d.state)
            {
                case ChromaDrop.Phase.Waiting:
                    if (now - d.since < d.delay || popped >= PopsPerFrame) continue;
                    d.view = Take();
                    if (d.view == null) continue;
                    popped++;
                    d.view.Show(d.Ringed);
                    d.painted = -1f;
                    d.state = ChromaDrop.Phase.Popping;
                    d.since = now;
                    break;

                case ChromaDrop.Phase.Popping:
                {
                    float k = (now - d.since) / PopSeconds;
                    d.scale = k >= 1f ? 1f : BackOut(k);
                    if (k >= 1f) d.state = ChromaDrop.Phase.Resting;
                    Hover(d, now);
                    break;
                }

                case ChromaDrop.Phase.Resting:
                    if (!near)
                    {
                        // لا تمايل ولا جذب من بعيد، لكن الهالة تتبع الكاميرا — ثُمن البعيدة كل إطار
                        if (d.view != null && ((i + Time.frameCount) & 7) == 0) d.view.Face(facing, Spin(d, now));
                        continue;
                    }
                    Vector3 before = d.rest;
                    // أرضها ذهبت: تأتيه بدل أن تختفي أمامه (أسامة: لا قلتش "ما يقدر ياخذها")
                    if (!d.Follow())
                    {
                        if (alive) Pull(d, chest, now); else Vanish(i);
                        continue;
                    }
                    if (alive && d.Collectible(now) && (Sinking(d, before, dt) || LeftBehind(d, chest, climb)))
                    {
                        Pull(d, chest, now);
                        continue;
                    }
                    Hover(d, now);
                    if (alive) Attract(d, chest, now);
                    if (now >= d.nextSpark && away < SparkRange * SparkRange) Twinkle(d, now);
                    break;

                case ChromaDrop.Phase.Arcing:
                {
                    float k = (now - d.since) / d.duration;
                    if (k >= 1f)
                    {
                        d.state = ChromaDrop.Phase.Resting;
                        d.scale = 1f;
                        Hover(d, now);
                    }
                    else
                    {
                        d.position = Vector3.LerpUnclamped(d.from, d.rest, k) + Vector3.up * (d.height * 4f * k * (1f - k));
                        d.scale = Mathf.Min(1f, 0.4f + 1.6f * k);
                    }
                    if (alive) Attract(d, chest, now);
                    break;
                }

                case ChromaDrop.Phase.Flying:
                {
                    float k = (now - d.since) / d.duration;
                    if (k >= 1f || jumped || !present) { Collect(i, present ? chest : d.position, now); continue; }

                    // يبدأ متردّدًا ثم ينقضّ، ويعلو قليلًا في طريقه — شفطٌ لا انزلاق
                    float e = k * k;
                    d.position = Vector3.LerpUnclamped(d.from, chest, e) + Vector3.up * (d.height * Mathf.Sin(Mathf.PI * e));
                    d.scale = Mathf.Lerp(1f, 0.45f, e);
                    break;
                }
            }

            Draw(d, now, facing, colourWorld);
        }

        if (present && now >= nextZones)
        {
            nextZones = now + ZoneEvery;
            zones.Assign(drops, player);
        }
        zones.Tick(dt, now);
        song.Tick(now);
    }

    private void Hover(ChromaDrop d, float now)
    {
        float lift = BobHeight * (d.golden ? 1.4f : 1f) * Mathf.Sin(now * BobSpeed + d.phase);
        d.position = d.rest + Vector3.up * lift;
    }

    /// <summary>دوران الكرة والحلقة: بطيء، وطوره يختلف من قطرةٍ لأخرى.</summary>
    private static float Spin(ChromaDrop d, float now) => now * 35f + d.phase * 57f;

    /// <summary>
    /// الجذب: داخل المدى وبلا جدارٍ بين الصدر والقطرة — قطرةٌ على الطابق الأعلى لا
    /// تُشفط عبر السقف. وقطرات الأحداث بعد ٤ ث تُشفط من ١٢ م ولو خلف شيء: لا تضيع.
    /// </summary>
    private void Attract(ChromaDrop d, Vector3 chest, float now)
    {
        if (!d.Collectible(now) || now < d.nextLook) return;

        float r2 = (d.position - chest).sqrMagnitude;
        bool reach = r2 <= MagnetRadius * MagnetRadius;
        bool rescue = d.id == null && now >= d.autoAt && r2 <= BurstReach * BurstReach;
        if (!reach && !rescue) return;

        d.nextLook = now + 0.15f;
        if (!rescue && probe != null && !probe.Sight(chest, d.position)) return;

        Pull(d, chest, now);
    }

    /// <summary>تطير إلى صدره الآن، بلا شرط مدًى ولا نظر.</summary>
    private void Pull(ChromaDrop d, Vector3 chest, float now)
    {
        float distance = Vector3.Distance(d.position, chest);
        d.state = ChromaDrop.Phase.Flying;
        d.since = now;
        d.from = d.position;
        d.duration = FlySeconds * Mathf.Clamp(distance / MagnetRadius, 1f, 2.4f);
        d.height = 0.3f + 0.06f * distance;
    }

    /// <summary>أرضها تهبط أسرع من ٢ م/ث — مصعدٌ نازل أو أرضٌ تسقط: لا تسقط معها.</summary>
    private static bool Sinking(ChromaDrop d, Vector3 before, float dt) => before.y - d.rest.y > 2f * dt;

    /// <summary>
    /// هو يصعد أو ينزل بسرعة (مصعد) وهي قريبةٌ أفقيًّا وفارقها الرأسي يكبر — ستبقى خلفه
    /// حيث لا يعود. تُشفط إليه وهو يمرّ بها.
    /// </summary>
    private static bool LeftBehind(ChromaDrop d, Vector3 chest, float climb)
    {
        if (Mathf.Abs(climb) < 1.2f) return false;
        Vector3 flat = d.rest - chest;
        float dy = flat.y;
        flat.y = 0f;
        return flat.sqrMagnitude < 3.5f * 3.5f && Mathf.Abs(dy) > 1.2f;
    }

    private void Draw(ChromaDrop d, float now, Quaternion facing, bool colourWorld)
    {
        if (d.view == null) return;

        float breathe = 1f + 0.07f * Mathf.Sin(now * 2.3f + d.phase);
        float glow = (d.golden ? 1.75f : d.big ? 1.4f : 1.05f) * d.scale * breathe;
        float ring = (d.golden ? 1.25f : 1f) * d.scale * (1f + 0.06f * Mathf.Sin(now * 3.1f + d.phase));
        d.view.Place(d.position, d.Size * d.scale, glow, ring, facing, Spin(d, now));

        // الرمادي: بعيدةٌ فضوءٌ أبيض، قريبةٌ فلونها كاملًا مع فقاعتها. والملوّن: لونها دائمًا
        float mix = colourWorld ? d.bloom : 1f;
        if (Mathf.Abs(mix - d.painted) < 0.03f) return;
        d.painted = mix;

        Color pale = Color.Lerp(d.color, Color.white, 0.45f);
        Color orb = Color.Lerp(pale, d.color, mix);
        var halo = Color.Lerp(new Color(1f, 1f, 1f, 0.5f), new Color(d.color.r, d.color.g, d.color.b, 0.7f), mix);
        if (d.golden) halo.a = Mathf.Min(1f, halo.a + 0.2f);
        d.view.Paint(orb, halo, new Color(d.color.r, d.color.g, d.color.b, 0.9f));
    }

    private void Twinkle(ChromaDrop d, float now)
    {
        if (d.Ringed)
        {
            // حلقةٌ من البريق تدور حول الذهبية
            d.nextSpark = now + Random.Range(0.1f, 0.18f);
            float a = now * 3.2f + d.phase;
            var around = new Vector3(Mathf.Cos(a), 0.25f * Mathf.Sin(a * 1.7f), Mathf.Sin(a)) * (d.golden ? 0.55f : 0.45f);
            ChromaDropArt.Emit(sparks, d.position + around, Vector3.up * 0.25f, Random.Range(0.16f, 0.3f), 0.5f,
                               Random.value < 0.5f ? Color.white : d.color);
            return;
        }

        d.nextSpark = now + Random.Range(2.4f, 5f);
        ChromaDropArt.Emit(sparks, d.position + Random.insideUnitSphere * 0.14f, Vector3.up * 0.2f,
                           Random.Range(0.16f, 0.26f), 0.45f, Color.white);
    }

    // ---------- الالتقاط ----------

    /// <summary>
    /// وصلت الصدر: تُحسب، ويُعزف لها، ويبرق مكانها وينبض لونًا. التسجيل في البنك آخر
    /// شيء — من يسمع <see cref="ChromaBank.Gained"/> يجد لونها وعدّاد السين محدّثين.
    /// </summary>
    private void Collect(int index, Vector3 at, float now)
    {
        ChromaDrop d = drops[index];
        RemoveAt(index);
        zones.Forget(d);
        Recycle(d);

        if (d.id != null)
        {
            ChromaBank.MarkCollected(d.id);
            PlacedCollected++;
        }
        LastColor = d.color;

        Burst(d, at);
        song.Pickup(d.golden, now);
        // الوجوه العادية بلا اهتزاز — جمعها سريعٌ متتابع، ونقرةٌ لكل ثلاثة كانت طنينًا دائمًا
        if (d.golden) PadRumble.Tick();

        if (ColorZones.SafetyScene) { }   // اللون هناك يعني الأمان — الالتقاط بلا دائرة
        else if (d.golden) ColorZones.Pulse(at, 4.5f, 0.22f, 0.5f, 1.2f);
        else if (now >= nextPulse && ColorZones.Pulse(at, 2.2f, 0.12f, 0.1f, 0.55f)) nextPulse = now + PulseGap;

        Bank(d.value, at);
        ChromaFunEvents.RaiseDropCollected(d.id, d.golden, false, at);
    }

    /// <summary>
    /// التسجيل في البنك يُنادي كل مستمعي <see cref="ChromaBank.Gained"/> في مكانه — ومستمعٌ
    /// معطوب في نظامٍ آخر لا يوقف حلقة القطرات عند كل التقاط.
    /// </summary>
    private static void Bank(int value, Vector3 at)
    {
        try { ChromaBank.Add(value, at); }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    private void Burst(ChromaDrop d, Vector3 at)
    {
        Color bright = Color.Lerp(d.color, Color.white, 0.35f);
        int count = d.golden ? 26 : d.big ? 16 : 10;
        float speed = d.golden ? 6f : 4f;

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            ChromaDropArt.Emit(sparks, at, dir * Random.Range(1.8f, speed), Random.Range(0.14f, d.golden ? 0.42f : 0.3f),
                               Random.Range(0.35f, 0.65f), i % 3 == 0 ? Color.white : bright);
        }

        ChromaDropArt.Emit(rings, at, Vector3.zero, d.golden ? 3.2f : d.big ? 2.2f : 1.5f, d.golden ? 0.6f : 0.35f, bright);
        if (d.golden) ChromaDropArt.Emit(rings, at, Vector3.zero, 5.5f, 0.95f, d.color);
    }

    /// <summary>ذهبت أرضها: تذوب بنفخة بريق — لا تبقى معلّقةً فوق حفرة، ولا تُحسب.</summary>
    private void Vanish(int index)
    {
        ChromaDrop d = drops[index];
        RemoveAt(index);
        zones.Forget(d);
        for (int i = 0; i < 5; i++)
            ChromaDropArt.Emit(sparks, d.position, Random.onUnitSphere * 1.2f, 0.2f, 0.4f, Color.white);
        Recycle(d);
    }

    /// <summary>حذفٌ بلا إزاحة: آخر القائمة مكانه — والحلقة تمشي من الآخر فقد مرّت عليه.</summary>
    private void RemoveAt(int index)
    {
        int last = drops.Count - 1;
        drops[index] = drops[last];
        drops.RemoveAt(last);
    }

    /// <summary>
    /// السين ينتهي: ما كان يطير إلى اللاعب وصله، وقطرات الأحداث حوله تُحسب له — كانت
    /// ستنجذب إليه بعد لحظات لولا الانتقال أو الكريديت. آخر علمٍ يُغرس يبدأ الكريديت في
    /// النداء نفسه، فتختفي قطراته قبل أن تُلتقط وتُحسب هنا حين تُحمَّل القائمة.
    /// </summary>
    private void CreditOwed()
    {
        // بالرقم لا بـ foreach: مستمعٌ لـ Gained يضيف إلى القائمة لا يكسر الحلقة
        for (int i = 0; i < drops.Count; i++)
        {
            ChromaDrop d = drops[i];
            // قطرات الدفعات كُسبت بحدثٍ فهي له أينما كانت — بوابة النهاية تنقله قبل أن يجمعها
            bool owed = d.state == ChromaDrop.Phase.Flying || d.id == null;
            if (!owed) continue;
            if (d.id != null) ChromaBank.MarkCollected(d.id);
            Bank(d.value, d.position);
        }
    }

    private ChromaDropView Take()
    {
        while (pool.Count > 0)
        {
            ChromaDropView v = pool.Pop();
            if (v.Alive) return v;
        }
        return stage != null ? ChromaDropView.Create(stage) : null;
    }

    private void Recycle(ChromaDrop d)
    {
        if (d.view != null && d.view.Alive)
        {
            d.view.Hide();
            pool.Push(d.view);
        }
        d.view = null;
    }

    // ---------- دفعات الأحداث ----------

    private bool Listening() =>
        !ChromaEvents.Quiet && Time.unscaledTime - loadedAt >= EventsAfter && FindPlayer();

    private void OnCheckpoint(Vector3 at)
    {
        if (Listening() && FirstTime("checkpoint", Nearest(at))) Spill(at, CheckpointBurst, SmallValue, false);
    }

    private void OnGate(Vector3 at)
    {
        if (Listening() && FirstTime("gate", at)) Spill(at, GateBurst, SmallValue, false);
    }

    private void OnPuzzle(Vector3 at)
    {
        if (Listening() && FirstTime("puzzle", at)) Spill(at, PuzzleBurst, SmallValue, false);
    }

    private void OnFlagPicked(Vector3 at)
    {
        if (Listening() && FirstTime("flag", at)) Spill(at, PickupBurst, SmallValue, false);
    }

    /// <summary>
    /// ٢٥ قطرة في خمسٍ كبيرة — الغرس أكبر لحظةٍ في المرحلة، وخمسٌ تُرى خيرٌ من ٢٥ تتزاحم.
    /// ولا يتكرّر: العلم المزروع يُقفل في قاعدته.
    /// </summary>
    private void OnFlagPlanted(Vector3 at)
    {
        if (Listening()) Spill(at, PlantBurst, PlantValue, true);
    }

    /// <summary>
    /// أقرب نقطة حفظٍ معروفة لموضع الحدث: نقاط علي تُبلغ بموضع اللاعب لا بموضعها،
    /// فالدخول إليها من طرفها الآخر يبقى النقطة نفسها.
    /// </summary>
    private Vector3 Nearest(Vector3 at)
    {
        Vector3 point = at;
        float best = 8f * 8f;
        if (layout != null)
        {
            foreach (Vector3 c in layout.checkpoints)
            {
                float d = (c - at).sqrMagnitude;
                if (d < best) { best = d; point = c; }
            }
        }
        return point;
    }

    /// <summary>
    /// أوّل مرّةٍ في هذه اللعبة يحدث هذا هنا؟ المطابقة بالمسافة لا بمفتاحٍ مقرّب: الباب
    /// المنزلق يُبلغ بموضعه وهو يتحرّك، فيختلف بين فتحةٍ وأخرى.
    /// </summary>
    private bool FirstTime(string kind, Vector3 at)
    {
        string key = sceneName + ":" + kind;
        if (!paid.TryGetValue(key, out List<Vector3> places)) paid[key] = places = new List<Vector3>();
        foreach (Vector3 p in places)
            if ((p - at).sqrMagnitude < SamePlace * SamePlace) return false;
        places.Add(at);
        return true;
    }

    /// <summary>
    /// قطراتٌ تنفجر من مكان الحدث في أقواس وتسقط على أرضٍ حوله — تُرى منه، ولا خطر
    /// تحتها. ما لم يجد أرضًا يسقط عند المركز. وتصير قابلةً للجذب واحدةً بعد أخرى،
    /// فتُشفط تيّارًا ويُسمع التقاطها عفقةً صاعدة لا نقرةً واحدة.
    /// </summary>
    private void Spill(Vector3 at, int count, int value, bool big)
    {
        if (!ChromaDropArt.Ready) return;
        Stage();
        if (stage == null || probe == null) return;

        // حدثٌ أبعد من مدى الجذب يحتفل عند اللاعب: ساعة ستيم تُبلغ من أصل العالم خلف
        // الجدران، وبوابات الهب تُفتح بعيدًا عن القاعدة — قطراتٌ هناك لا تُرى ولا تُجذب
        if (body != null && (at - body.position).sqrMagnitude > BurstReach * BurstReach) at = Chest();

        int flying = 0;
        foreach (ChromaDrop d in drops) if (d.id == null) flying++;
        count = Mathf.Min(count, BurstLimit - flying);
        if (count <= 0) return;

        ChromaStyle style = ChromaStyle.Get();
        float now = Time.time;
        bool grounded = probe.Ground(at, at.y + 0.5f, at.y - 6f, out RaycastHit baseHit);
        Vector3 floor = grounded ? baseHit.point : at - Vector3.up;
        Vector3 middle = floor + Vector3.up * ChromaDropProbe.Hover;
        int tint = Random.Range(0, 7);
        float turn = Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            Vector3 ground = floor;
            Collider under = grounded ? baseHit.collider : null;
            float radial = Mathf.Lerp(1.2f, 3.4f, Mathf.Sqrt((i + 0.5f) / count));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                float angle = (turn + i * 137.5f + attempt * 47f) * Mathf.Deg2Rad;
                Vector3 p = floor + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radial * (1f - 0.3f * attempt));
                if (!probe.Ground(p, floor.y + 1.5f, floor.y - 3.5f, out RaycastHit hit)) continue;

                Vector3 drop = hit.point + Vector3.up * ChromaDropProbe.Hover;
                if (!probe.Sight(middle, drop) || probe.Hazard(drop, hit.point)) continue;

                ground = hit.point;
                under = hit.collider;
                break;
            }

            var d = new ChromaDrop
            {
                value = value,
                big = big,
                color = style.Palette(tint + i),
                phase = i * 0.9f,
                state = ChromaDrop.Phase.Arcing,
                since = now,
                from = at,
                position = at,
                scale = 0.4f,
                collectibleAt = now + BurstCollectable + i * BurstStagger,
                autoAt = now + BurstAuto,
                nextSpark = now + Random.Range(0.5f, 3f),
            };
            d.Anchor(ground, under);
            float distance = Vector3.Distance(at, d.rest);
            d.duration = 0.5f + 0.06f * distance;
            d.height = 1.1f + 0.3f * distance;

            d.view = Take();
            if (d.view == null) return;
            d.view.Show(d.Ringed);
            // مخفيّةً في مكان الحدث حتى أوّل إطارٍ يتحرّك: جسدٌ من المخزون يحمل موضعه القديم
            d.view.Place(at, 0f, 0f, 0f, Quaternion.identity, 0f);
            d.painted = -1f;
            drops.Add(d);
        }

        if (Time.timeScale > 0f) ChromaSfx.Play("Drop_Burst", 0.7f);
    }

    private void OnRunReset()
    {
        paid.Clear();
        PlacedCollected = 0;
    }

    /// <summary>كبرٌ يتجاوز حجمه قليلًا ثم يستقرّ — ظهورٌ بقفزة لا بانزلاق.</summary>
    private static float BackOut(float x)
    {
        const float s = 1.7f;
        x = Mathf.Clamp01(x) - 1f;
        return x * x * ((s + 1f) * x + s) + 1f;
    }
}

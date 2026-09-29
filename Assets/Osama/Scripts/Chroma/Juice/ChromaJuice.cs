using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>العصارة</b>: لحظات اللعبة تُرى وتُحَسّ. نقطة الحفظ، والعلم يُلتقط ويُغرس، ولغزٌ يُحلّ،
/// وبوابةٌ تنفتح، والموت — لكلٍّ نبضة لونٍ وجسيماتٌ وصوت في موضعها من العالم. وغبار القدمين
/// في <see cref="ChromaJuiceSteps"/>.
///
/// <b>اللون من النبضة لا من الجسيمات</b>: الشاشة تُرمَّد بعد رسم كل شيء، والجسيمات معه. فكل
/// احتفالٍ يُطلق <see cref="ColorZones.Pulse"/> في اللحظة نفسها فيتلوّن ما تحتها، وما خرج عنها
/// يبقى أبيض مضيئًا يُقرأ على الرمادي. وإن رُفضت النبضة (نفد المخزون، أو سينٌ بلا نظام لون)
/// بقي الاحتفال كما هو، رماديًّا. وفي سينٍ فيه ملاجئ لا نبضة أصلًا (<see cref="Splash"/>).
///
/// <b>لا يحتفل بما لم يفعله اللاعب</b>:
/// <list type="bullet">
/// <item>أوّل ١٫٥ ث من كل سين: ما يقع فيها استعادةٌ لا لعب.</item>
/// <item>حين <see cref="ChromaEvents.Quiet"/>، أو بلا لاعب.</item>
/// <item>نقطة الحفظ والبوابة مرّةً لكل موضعٍ في الزيارة: النقاط تُطلق كلّما دُخلت، وبوابات القرب
/// تنفتح كلّما اقترب.</item>
/// <item>احتفالٌ صغير بجانب احتفالٍ بدأ للتوّ لا يُضاف فوقه: لغزٌ يفتح البوابة التي بجانبه في
/// الإطار نفسه، فدائرةٌ واحدة تكفي.</item>
/// </list>
///
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DisallowMultipleComponent]
public class ChromaJuice : MonoBehaviour
{
    /// <summary>أوّل ما بعد التحميل: ما يقع فيه استعادةٌ لا لعب — بابٌ يُفتح، علمٌ يُعاد، نقطةٌ يولد فيها.</summary>
    private const float SettleTime = 1.5f;

    /// <summary>
    /// مركز النبضة فوق الأرض. الحدّ الرأسي يُقاس منه: على الأرض نفسها يضيع نصفه تحتها، وعلى
    /// منتصف العلم يعلو معه. فالمتر يُبقي اللون في طابقه داخل المباني (طوابق الباك ستيج بينها
    /// ٥٫٥ م) ويصعد على ما حوله في الخارج.
    /// </summary>
    private const float ZoneLift = 1f;

    /// <summary>الحلقة فوق الأرض بشعرة، وإلا تقاسمت معها العمق فتقطّعت.</summary>
    internal const float RingLift = 0.04f;

    /// <summary>حدثان أقرب من هذا (أفقيًّا ورأسيًّا) = الموضع نفسه.</summary>
    private const float SameSpot = 3f;

    /// <summary>احتفالٌ صغير بجانب احتفالٍ بدأ للتوّ لا يُضاف فوقه.</summary>
    private const float CrowdTime = 1.5f, CrowdRadius = 8f;

    /// <summary>
    /// لغزٌ أبعد من هذا عن اللاعب يُحتفل به عنده: من يحلّ اللغز واقفٌ عنده، و<c>ClockPuzzleHint</c>
    /// في ستيم كائنٌ في أصل العالم لا على الساعة. والبوابة لا تُنقل: قاعدة العلم في الهب تفتح
    /// بابًا على جزيرةٍ أخرى، والدائرة هناك هي التي تقول «انفتح».
    /// </summary>
    private const float PuzzleReach = 12f;

    /// <summary>حدثٌ أقرب من هذا إلى اللاعب: أرضه هي التي تحت قدميه (<see cref="Floor"/>).</summary>
    private const float NearPlayer = 4f;

    /// <summary>بعد الموت لا غبار: الجسد يحترق، أو يُنقل عند علي بلا إعلان.</summary>
    private const float QuietAfterDeath = 1.5f;

    /// <summary>
    /// حبر الموت في آخر احتراق <see cref="DeathDissolveEffect"/> (ثانيةٌ مع كل
    /// <see cref="PlayerKillable"/> — ستيم والسيرك) لا معه: سحابةٌ فوق الجسد تحجب الاحتراق، وهو
    /// خبر الموت الحقيقي. وموت علي بلا احتراق، فحبره فوري.
    /// </summary>
    private const float BurnTime = 0.85f;

    private static readonly Color Warm = new Color(1f, 0.94f, 0.82f, 1f);

    private static ChromaJuice instance;

    private struct Moment
    {
        public Vector3 at;
        public float time;
    }

    private ChromaJuiceFx fx;
    private ChromaJuiceSteps steps;
    private ChromaJuiceCamera lens;
    private ChromaStyle style;
    private int groundMask;

    /// <summary>في هذا السين ملاجئ (<see cref="SafeZone"/>)؟ يُسأل مع كل سين، لا مع كل احتفال.</summary>
    private bool shelters;

    private readonly List<Vector3> celebrated = new List<Vector3>();
    private readonly Moment[] recent = new Moment[6];
    private int recentNext;
    private int hue;
    private float loadedAt;

    private Transform player;
    private CharacterController controller;
    private PlayerKillable killable;
    private float nextSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("ChromaJuice") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ChromaJuice>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // ---------- لمكوّنيه ----------

    internal ChromaJuiceFx Fx => fx;
    internal ChromaJuiceCamera Lens => lens;
    internal ChromaStyle Style => style;

    /// <summary>كل الطبقات إلا طبقة اللاعب: الأرض تحته لا جسده.</summary>
    internal int GroundMask => groundMask;

    /// <summary>وقت لعب: لا هدوء مطلوب، ومضت لحظة الاستعادة بعد التحميل.</summary>
    internal bool Live => !ChromaEvents.Quiet && Time.unscaledTime - loadedAt >= SettleTime;

    /// <summary>
    /// اللاعب الفعّال، أو null. يُبحث عنه مرّةً في الثانية إن غاب أو أُطفئ — <see cref="PlayerLocator"/>
    /// يجمع كل الموسومين فلا يُنادى كل إطار — ويُنسى مع كل سين.
    /// </summary>
    internal Transform Player
    {
        get
        {
            bool gone = player == null || !player.gameObject.activeInHierarchy;
            if (gone && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                GameObject found = PlayerLocator.Find("Player");
                if (found != null)
                {
                    controller = found.GetComponentInParent<CharacterController>();
                    player = controller != null ? controller.transform : found.transform;
                    killable = found.GetComponentInParent<PlayerKillable>();
                }
            }
            return player != null && player.gameObject.activeInHierarchy ? player : null;
        }
    }

    internal CharacterController Controller => controller;
    internal bool PlayerDead => killable != null && killable.IsDead;

    // ---------- الدورة ----------

    private void Awake()
    {
        style = ChromaStyle.Get();
        fx = new ChromaJuiceFx(transform);
        steps = gameObject.AddComponent<ChromaJuiceSteps>();
        lens = gameObject.AddComponent<ChromaJuiceCamera>();

        int body = LayerMask.NameToLayer("Player");
        groundMask = Physics.DefaultRaycastLayers & ~(body >= 0 ? 1 << body : 0);

        loadedAt = Time.unscaledTime;
        shelters = HasShelters();
        ForgetRecent();
    }

    private void OnEnable()
    {
        ChromaEvents.CheckpointReached += OnCheckpoint;
        ChromaEvents.FlagPickedUp += OnFlagPicked;
        ChromaEvents.FlagPlanted += OnFlagPlanted;
        ChromaEvents.PuzzleSolved += OnPuzzleSolved;
        ChromaEvents.GateOpened += OnGateOpened;
        ChromaEvents.PlayerDied += OnPlayerDied;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ChromaEvents.CheckpointReached -= OnCheckpoint;
        ChromaEvents.FlagPickedUp -= OnFlagPicked;
        ChromaEvents.FlagPlanted -= OnFlagPlanted;
        ChromaEvents.PuzzleSolved -= OnPuzzleSolved;
        ChromaEvents.GateOpened -= OnGateOpened;
        ChromaEvents.PlayerDied -= OnPlayerDied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy() => fx?.Dispose();

    /// <summary>سينٌ جديد يبدأ نظيفًا: لا دخان ولا احتفالٌ مؤجّل ولا ذاكرةُ مواضع من الذي قبله.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        StopAllCoroutines();
        fx.Clear();
        steps.Forget();
        lens.Stop();
        celebrated.Clear();
        ForgetRecent();

        player = null;
        controller = null;
        killable = null;
        nextSearch = 0f;
        loadedAt = Time.unscaledTime;
        shelters = HasShelters();
    }

    // ---------- اللحظات ----------

    /// <summary>نقطة حفظ: دائرة لونٍ متوسّطة، وحلقةٌ ذهبية على الأرض، وعمود شررٍ يصعد حولها.</summary>
    private void OnCheckpoint(Vector3 at)
    {
        if (!FirstTime(at) || !Ready() || Crowded(at)) return;

        bool grounded = Floor(at, out Vector3 floor);
        Splash(grounded ? floor + Vector3.up * ZoneLift : at, 6f, 0.45f, 1f, 3f);
        fx.Flash(floor + Vector3.up * 0.7f, 3f, 0.3f, Warm);
        if (grounded) fx.Ring(floor + Vector3.up * RingLift, 4.2f, 0.65f, Bright(style.gold));
        StartCoroutine(Column(floor, 36, 0.9f));
        ChromaSfx.Play("Pulse_Whoosh", 0.55f, Random.Range(0.97f, 1.04f));
        Remember(at);
    }

    /// <summary>
    /// التقاط العلم: رشقة لونٍ منتصرة — دائرةٌ كبيرة، وحلقتان تتلاحقان، ونافورة شررٍ وقطراتٍ
    /// بألوان اللوحة. ونبضتا اليد لكل الأعلام، لا لما يُحمل بـ<see cref="FlagItem"/> وحده.
    /// </summary>
    private void OnFlagPicked(Vector3 at)
    {
        if (!Ready()) return;

        bool grounded = Floor(at, out Vector3 floor);
        Vector3 from = grounded ? Source(at, floor) : at;
        Splash(grounded ? floor + Vector3.up * ZoneLift : at, 12f, 0.6f, 0.9f, 3.5f);
        fx.Flash(from, 4f, 0.35f, Warm);
        if (grounded)
        {
            fx.Ring(floor + Vector3.up * RingLift, 6.5f, 0.75f, Bright(style.Palette(hue)));
            StartCoroutine(RingLater(0.12f, floor + Vector3.up * RingLift, 4f, 0.6f,
                                     Bright(style.Palette(hue + 3))));
        }
        StartCoroutine(Fountain(from, 40, 18, 0.45f, 1f));
        ChromaSfx.Play("Pulse_Whoosh", 0.85f, Random.Range(0.98f, 1.03f));
        PadRumble.Pickup();
        Remember(at);
    }

    /// <summary>
    /// غرس العلم — أكبر لحظة: أوسع دائرةٍ وأطولها، وحلقتان، ونافورةٌ أعلى، ومطر قصاصات،
    /// ودمدمةٌ تعلو في اليد.
    /// </summary>
    private void OnFlagPlanted(Vector3 at)
    {
        if (!Ready()) return;

        bool grounded = Floor(at, out Vector3 floor);
        Vector3 from = grounded ? Source(at, floor) : at;
        Splash(grounded ? floor + Vector3.up * ZoneLift : at, 16f, 1.2f, 2f, 4f);
        fx.Flash(from, 5.5f, 0.45f, Warm);
        if (grounded)
        {
            fx.Ring(floor + Vector3.up * RingLift, 8.5f, 0.95f, Bright(style.gold));
            StartCoroutine(RingLater(0.15f, floor + Vector3.up * RingLift, 5.5f, 0.8f,
                                     Bright(style.Palette(hue + 2))));
        }
        StartCoroutine(Fountain(from, 55, 30, 0.7f, 1.12f));
        StartCoroutine(Confetti(from, floor, 40, 150, 1.6f));
        ChromaSfx.Play("Pulse_Whoosh", 1f, 0.86f);
        PadRumble.Open(1f);
        Remember(at);
    }

    private void OnPuzzleSolved(Vector3 at)
    {
        if (Ready()) Minor(Near(at, PuzzleReach));
    }

    /// <summary>البوابة مرّةً لكل موضع: بوابات القرب تنفتح كلّما اقترب اللاعب.</summary>
    private void OnGateOpened(Vector3 at)
    {
        if (FirstTime(at) && Ready()) Minor(at);
    }

    /// <summary>لغزٌ حُلّ أو بوابةٌ انفتحت: دائرةٌ صغيرة وانفجار شرر.</summary>
    private void Minor(Vector3 at)
    {
        if (Crowded(at)) return;

        Vector3 center = Floor(at, out Vector3 floor) ? floor + Vector3.up * ZoneLift : at;
        Splash(center, 8f, 0.35f, 0.9f, 3f);
        fx.Flash(center, 2.8f, 0.28f, Warm);
        Burst(center, 28, 8);
        ChromaSfx.Play("Pulse_Whoosh", 0.4f, Random.Range(1.08f, 1.16f));
        Remember(at);
    }

    /// <summary>
    /// الموت: نفخة حبرٍ صغيرة حيث اختفى، وقطراتٌ قليلة تتطاير. خفيفةٌ عمدًا — تقول «هنا اختفى»
    /// لا أكثر — وبعد الاحتراق لا فوقه (<see cref="BurnTime"/>).
    /// </summary>
    private void OnPlayerDied(Vector3 at)
    {
        Vector3 spot = steps.Vanished(at);
        steps.Suspend(QuietAfterDeath);
        if (Ready()) StartCoroutine(Ink(spot, killable != null ? BurnTime : 0f));
    }

    // ---------- على مهل ----------
    //
    // ذيول الاحتفال (ثانيةٌ ونصف على الأكثر) تكمل وإن صار الوقت «هادئًا»: الهدوء يُسأل قبل أن
    // يبدأ الاحتفال، وغرس العلم الأخير يبدأ الكريديت في الإطار نفسه — فقطعُها يُسقط مطر
    // القصاصات من ذروة اللعبة. وتحميل السين يوقفها ويمحو جسيماتها.

    /// <summary>كم أُطلق حتى الآن من <paramref name="count"/> موزّعةً على <paramref name="duration"/> — بالزمن لا بالإطارات.</summary>
    private static int Due(int count, float duration, float elapsed) =>
        duration <= 0f ? count : Mathf.Min(count, Mathf.CeilToInt(count * elapsed / duration));

    /// <summary>الحلقة الثانية تلحق الأولى — موجتان لا واحدة.</summary>
    private IEnumerator RingLater(float delay, Vector3 at, float radius, float life, Color color)
    {
        for (float t = 0f; t < delay; t += Time.deltaTime) yield return null;
        fx.Ring(at, radius, life, color);
    }

    /// <summary>حبر الموت بعد <paramref name="delay"/>: سحابةٌ صغيرة تذوب، وقطراتٌ تتطاير.</summary>
    private IEnumerator Ink(Vector3 spot, float delay)
    {
        for (float t = 0f; t < delay; t += Time.deltaTime) yield return null;

        Color cloud = style.ink;
        cloud.a = 0.4f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            fx.Dust(spot + dir * 0.15f, dir * Random.Range(0.5f, 1.2f) + Vector3.up * Random.Range(0.2f, 0.5f),
                    Random.Range(0.35f, 0.6f), Random.Range(0.7f, 1f), cloud);
        }
        for (int i = 0; i < 6; i++)
            fx.Blob(spot, Throw(2f, 3.5f, 1.2f, 2.6f), Random.Range(0.05f, 0.08f), Random.Range(0.55f, 0.8f), style.ink);
    }

    /// <summary>عمود شررٍ يصعد حول النقطة، موزّعٌ بالزاوية الذهبية فلا يتكتّل في جهة.</summary>
    private IEnumerator Column(Vector3 floor, int count, float duration)
    {
        float angle = Random.value * Mathf.PI * 2f;
        float t = 0f;
        for (int i = 0; i < count;)
        {
            t += Time.deltaTime;
            for (int due = Due(count, duration, t); i < due; i++)
            {
                angle += 2.39996f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 swirl = Vector3.Cross(Vector3.up, dir) * 0.45f;
                fx.Drift(floor + dir * Random.Range(0.7f, 1.3f) + Vector3.up * Random.Range(0.1f, 0.5f),
                         Vector3.up * Random.Range(1.4f, 2.6f) + swirl - dir * 0.1f,
                         Random.Range(0.22f, 0.36f), Random.Range(1f, 1.5f), Bright(NextHue()));
            }
            yield return null;
        }
    }

    /// <summary>نافورة: شررٌ يعلو ويعود، وقطرات لونٍ معتمة تتلوّن حيث تطير.</summary>
    private IEnumerator Fountain(Vector3 from, int sparks, int drops, float duration, float strength)
    {
        float t = 0f;
        int s = 0, d = 0;
        while (s < sparks || d < drops)
        {
            t += Time.deltaTime;
            for (int due = Due(sparks, duration, t); s < due; s++)
                fx.Spray(from, Throw(5.5f, 8f, 0.6f, 2.2f) * strength, Random.Range(0.22f, 0.36f),
                         Random.Range(1.1f, 1.7f), Bright(NextHue()));
            for (int due = Due(drops, duration, t); d < due; d++)
                fx.Blob(from, Throw(4.5f, 7f, 0.9f, 2.6f) * strength, Random.Range(0.09f, 0.16f),
                        Random.Range(1.1f, 1.5f), NextHue());
            yield return null;
        }
    }

    /// <summary>
    /// قصاصات: دفعةٌ تنطلق من العلم، ثم مطرٌ يتساقط حوله. يبدأ دون سقف الحدّ الرأسي للنبضة
    /// (٤ م فوق مركزها) فتبقى ملوّنةً طول سقوطها — وتحت السقف إن كان المكان مسقوفًا، فلا
    /// تظهر القصاصات من داخل الخشب.
    /// </summary>
    private IEnumerator Confetti(Vector3 from, Vector3 floor, int burst, int rain, float duration)
    {
        for (int i = 0; i < burst; i++)
            fx.Confetti(from, Throw(4.5f, 7.5f, 0.8f, 3f), Piece(), Random.Range(2.4f, 3.2f), NextHue());

        // السقف: ٠٫٥ فوق الأرض ثم ٠٫٢ تحته. والأقرب من مترين شيءٌ فوق العلم (عموده، مصباح) لا سقف
        float top = 4.6f;
        if (Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.up, out RaycastHit roof, top, groundMask,
                            QueryTriggerInteraction.Ignore) && roof.distance > 1.5f)
            top = Mathf.Min(top, roof.distance + 0.3f);

        float t = 0f;
        for (int i = 0; i < rain;)
        {
            t += Time.deltaTime;
            for (int due = Due(rain, duration, t); i < due; i++)
            {
                Vector2 spread = Random.insideUnitCircle * 5.5f;
                fx.Confetti(floor + new Vector3(spread.x, Random.Range(Mathf.Max(1f, top - 1.4f), top), spread.y),
                            new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.6f, -0.1f), Random.Range(-0.5f, 0.5f)),
                            Piece(), Random.Range(2.6f, 3.4f), NextHue());
            }
            yield return null;
        }
    }

    /// <summary>انفجار شررٍ يطفو في كل الجهات إلا تحت الأرض، وقطراتٌ قليلة.</summary>
    private void Burst(Vector3 at, int sparks, int drops)
    {
        for (int i = 0; i < sparks; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            fx.Drift(at, dir.normalized * Random.Range(2.2f, 4f), Random.Range(0.22f, 0.36f),
                     Random.Range(0.8f, 1.3f), Bright(NextHue()));
        }
        for (int i = 0; i < drops; i++)
            fx.Blob(at, Throw(3f, 5f, 0.8f, 2f), Random.Range(0.07f, 0.12f), Random.Range(0.9f, 1.2f), NextHue());
    }

    /// <summary>سرعة قذفٍ للأعلى بصعودٍ وانتشارٍ عشوائيّين في جهةٍ عشوائية.</summary>
    private static Vector3 Throw(float upMin, float upMax, float outMin, float outMax)
    {
        float a = Random.value * Mathf.PI * 2f;
        float spread = Random.Range(outMin, outMax);
        return new Vector3(Mathf.Cos(a) * spread, Random.Range(upMin, upMax), Mathf.Sin(a) * spread);
    }

    /// <summary>قصاصةٌ مستطيلة بحجم شخصيةٍ طولها متر.</summary>
    private static Vector2 Piece() => new Vector2(Random.Range(0.11f, 0.16f), Random.Range(0.06f, 0.09f));

    // ---------- متى وأين ----------

    private bool Ready() => isActiveAndEnabled && Live && Player != null;

    /// <summary>
    /// نبضة الاحتفال — إلا بين الملاجئ. في السيرك اللون أمان: الملجأ يُلوّن ما يحميه بالضبط
    /// («ما تراه هو ما يحميك»)، فدائرة لونٍ لا تحمي تكذب على اللاعب — يقف فيها والفئران آتية.
    /// هناك يبقى الاحتفال بشرره وصوته، أبيض.
    /// </summary>
    private void Splash(Vector3 at, float radius, float hold, float fade, float reach)
    {
        if (!shelters) ColorZones.Pulse(at, radius, 0.18f, hold, fade, reach);
    }

    /// <summary>ولو مطفأً: ملجأٌ لم يُشعَل بعد يبقى قانون السين.</summary>
    private static bool HasShelters() => FindAnyObjectByType<SafeZone>(FindObjectsInactive.Include) != null;

    /// <summary>
    /// أوّل مرّة لهذا الموضع في الزيارة؟ ويُحفظ وإن لم يُحتفل به: بوابةٌ فُتحت وقت التحميل أو
    /// نقطةٌ وُلد فيها لا تحتفل حين يعود إليها.
    /// </summary>
    private bool FirstTime(Vector3 at)
    {
        foreach (Vector3 seen in celebrated)
            if (Flat(seen - at) < SameSpot && Mathf.Abs(seen.y - at.y) < SameSpot) return false;

        if (celebrated.Count >= 64) celebrated.RemoveAt(0);
        celebrated.Add(at);
        return true;
    }

    private void Remember(Vector3 at)
    {
        recent[recentNext] = new Moment { at = at, time = Time.unscaledTime };
        recentNext = (recentNext + 1) % recent.Length;
    }

    private bool Crowded(Vector3 at)
    {
        foreach (Moment moment in recent)
            if (Time.unscaledTime - moment.time < CrowdTime && Flat(moment.at - at) < CrowdRadius) return true;
        return false;
    }

    private void ForgetRecent()
    {
        for (int i = 0; i < recent.Length; i++) recent[i].time = -100f;
    }

    /// <summary>موضع الحدث، أو اللاعب إن كان الحدث أبعد من <paramref name="reach"/> عنه.</summary>
    private Vector3 Near(Vector3 at, float reach)
    {
        Transform body = Player;
        return body != null && Flat(at - body.position) > reach ? body.position : at;
    }

    /// <summary>
    /// الأرض التي يمشي عليها اللاعب تحت الحدث — مكان الحلقة ومرجع النبضة. الحدث يأتي من منتصف
    /// العلم أو مركز تريغر، وتحته قد تكون قاعدةٌ أو طاولة، وحلقةٌ على سطحها تطفو فوق الأرض
    /// حولها. فإن كان اللاعب قريبًا أُخذ الارتفاع من تحت قدميه، والموضع من الحدث.
    /// </summary>
    private bool Floor(Vector3 at, out Vector3 floor)
    {
        floor = at;
        Transform body = Player;
        Vector3 probe = body != null && Flat(at - body.position) < NearPlayer ? body.position : at;
        if (!Ground(probe, 4f, out Vector3 under)) return false;

        floor = new Vector3(at.x, under.y, at.z);
        return true;
    }

    /// <summary>منبع النافورة: العلم نفسه، أو فوق الأرض بقليل إن جاء الحدث من القدمين.</summary>
    private static Vector3 Source(Vector3 at, Vector3 floor) =>
        at.y < floor.y + 0.5f ? floor + Vector3.up * 0.9f : at;

    /// <summary>
    /// أقرب سطحٍ تحت <paramref name="at"/>، لا جسد اللاعب ولا تريغر. شعاعٌ يرجع الأقرب بعينه —
    /// <c>RaycastNonAlloc</c> لا يضمنه إن امتلأ مخزنه، فتضيع الأرضية تحت كومة كولايدرات — وما
    /// كان من جسد اللاعب (ابنٌ على طبقةٍ غير طبقته) يُعبَر إلى ما تحته.
    /// </summary>
    private bool Ground(Vector3 at, float depth, out Vector3 point)
    {
        point = at;
        Vector3 from = at + Vector3.up * 0.3f;
        float reach = depth + 0.3f;
        for (int tries = 0; tries < 3 && reach > 0f; tries++)
        {
            if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, reach, groundMask,
                                 QueryTriggerInteraction.Ignore)) return false;
            if (player == null || !hit.collider.transform.IsChildOf(player))
            {
                point = hit.point;
                return true;
            }

            float step = hit.distance + 0.01f;
            from += Vector3.down * step;
            reach -= step;
        }
        return false;
    }

    private static float Flat(Vector3 d) => Mathf.Sqrt(d.x * d.x + d.z * d.z);

    private Color NextHue() => style.Palette(hue++);

    /// <summary>ألوان اللوحة نحو الأبيض قليلًا: الشرر ضوءٌ يُضاف، والمشبع وحده يبدو باهتًا عليه.</summary>
    private static Color Bright(Color color) => Color.Lerp(color, Color.white, 0.3f);
}

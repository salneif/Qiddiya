using UnityEngine;

/// <summary>
/// غبار القدمين: نفخةٌ عند القفز — وحلقةٌ في الهواء للقفزة المزدوجة — وحلقة غبارٍ عند الهبوط
/// بقدر السقطة، ونفخاتٌ صغيرة خلف القدمين وهو يجري. والسقطة الكبيرة تُسمع وتُحَسّ: نفخة صوتٍ
/// خافتة، ورجّةٌ في اليد، وانخفاضةٌ صغيرة في الكاميرا (<see cref="ChromaJuiceCamera"/>).
///
/// <b>كله من حركة اللاعب نفسها</b>، لا من سكربت حركته — فيعمل مع أيٍّ منها بلا سؤال:
/// <list type="bullet">
/// <item><b>الأرض بكرةٍ تُلقى تحت القدمين</b>، لا بـ<c>isGrounded</c>: ذاك جواب آخر <c>Move</c>
/// من أيّ سكربت، وفي التوايلايت سكربت المنصّات يستدعي <c>Move</c> كل إطار — بصفرٍ إن لم يكن
/// اللاعب فوقها — فيصير الجواب تابعًا لترتيب السكربتات لا للأرض.</item>
/// <item><b>السرعة نسبةً لما تحت القدمين</b>: الواقف على منصّةٍ تسير لا يثير غبارًا.</item>
/// <item><b>أبعد من ٣ م في إطار = نقل</b> (موت، بوابة، نقطة ظهور) لا سقوطٌ يُحتفل به — الحدّ
/// نفسه في <see cref="TeleportLanding"/>.</item>
/// </list>
///
/// <b>الحدود من فيزياء هذه اللعبة</b> (ومجرّبةٌ على محاكاةٍ لسكربت الحركة بـ٣٠ و٦٠ و١٤٤ إطارًا):
/// الجاذبية ٢٠ والقفزة مترٌ واحد، فكل قفزةٍ تترك نفخةً صغيرة عند هبوطها (طيرانها ٠٫٦ ث)،
/// والسقوط عن حافّةٍ من نحو متر، والسقطة الكبيرة من نحو مترين ونصف. والشخصية بطول مترٍ وتجري ٢–٣
/// م/ث (٢ في الهب)، فحدّ الجري ١٫٧: الانحناء (١) والعصا المائلة نصف ميلٍ لا يثيران شيئًا،
/// و٤٫٥ م/ث لا تُبلغ في أي سين. والنفخة فوقه بقدر السرعة: أثرٌ باهت لمشي الهب، وكاملةٌ لجري
/// السيرك والتوايلايت (<see cref="FullRun"/>).
/// </summary>
[DisallowMultipleComponent]
public class ChromaJuiceSteps : MonoBehaviour
{
    /// <summary>
    /// هبوطٌ بهذه السرعة (م/ث) يثير الغبار: سقطةٌ من نحو مترٍ فأكثر. والسرعة المقيسة أقلّ من
    /// الحقيقية بنصف مترٍ في الثانية تقريبًا — الكرة تلمس الأرض قبل القدمين بسنتيمترات،
    /// والإطار يقسم السقوط خطوات — فالحدود كلها تحت أرقام الفيزياء بقليل.
    /// </summary>
    private const float HardLanding = 5.5f;

    /// <summary>أبطأ منه يُحسب إن طال الطيران — القفزة على أرضٍ مستوية، ونزولٌ عن زلّاقة.</summary>
    private const float SoftLanding = 3f, LongAir = 0.45f;

    /// <summary>سقطةٌ كبيرة (نحو مترين ونصف فأكثر): صوتٌ ورجّة وانخفاضة كاميرا.</summary>
    private const float BigLanding = 9f;

    /// <summary>أقصى السلّم (سقطة خمسة أمتار): فوقها كل شيءٍ بأقصاه.</summary>
    private const float HugeLanding = 14f;

    /// <summary>
    /// القفزة ترفع اللاعب فورًا، والمشي عن الحافة لا يرفعه أبدًا. والدرج يرفعه ١٠ سم دفعةً
    /// (<c>Step Offset</c>)، فالارتفاع ضِعفها، والسرعة فوق صعود السلالم.
    /// </summary>
    private const float JumpRise = 0.2f, JumpSpeed = 3.5f, JumpWindow = 0.35f;

    /// <summary>
    /// ركلةٌ في الهواء (المزدوجة بعد البالون في التوايلايت، أو نطّاطة): الصعود يزيد بهذا (م/ث)
    /// في إطارٍ واحد، بعد <see cref="KickAfter"/> من الإقلاع — فقفزة الأرض نفسها لا تُعدّ.
    /// </summary>
    private const float KickRise = 4f, KickAfter = 0.1f;

    private const float RunSpeed = 1.7f, RunEvery = 0.3f;

    /// <summary>أسرع ما تجري الشخصية (السيرك والتوايلايت): الغبار كاملًا، وتحته يخفت ويصغر.</summary>
    private const float FullRun = 3f;

    /// <summary>بعد الهبوط لا غبار جري — نفخة الهبوط تكفي.</summary>
    private const float CalmAfterLanding = 0.2f;

    private const float Teleport = 3f;

    /// <summary>إطارٌ أطول من هذا تهنيقة تحميل: لا تُشتقّ منه سرعة.</summary>
    private const float MaxStep = 0.1f;

    /// <summary>الكرة تبدأ داخل كبسولة اللاعب (فلا تصطدم بها) وتنزل هذا تحت قاعدتها.</summary>
    private const float ProbeLift = 0.05f, GroundSlack = 0.08f;

    private ChromaJuice juice;

    private bool tracking, airborne, jumped, kicking, running;
    private Vector3 lastFeet, lastGround, takeoff, kickAt, heading;
    private float leftAt, peakFall, lastRise, lastSeenAt, nextDustAt, calmUntil, quietUntil, speed;
    private Transform floor;
    private Vector3 floorAt;

    private void Awake() => juice = GetComponent<ChromaJuice>();

    /// <summary>ينسى ما تتبّعه: التالي يبدأ من جديد، ولا يُقارن بما قبل نقلٍ أو موت.</summary>
    internal void Forget()
    {
        tracking = false;
        airborne = false;
        running = false;
        floor = null;
    }

    /// <summary>صمتٌ بعد الموت — نظام علي لا يُعلن متى يعود اللاعب.</summary>
    internal void Suspend(float seconds)
    {
        quietUntil = Mathf.Max(quietUntil, Time.time + seconds);
        Forget();
    }

    /// <summary>
    /// أين اختفى اللاعب. الحدث يأتي بموضعه لحظة الإعلان، وموت الماء عند علي يُنزله عشرين
    /// مترًا قبل أن يُعلن — فإن ابتعد الموضع عمّا رأيناه للتوّ، فما رأيناه هو مكانه.
    /// </summary>
    internal Vector3 Vanished(Vector3 reported)
    {
        if (!tracking || Time.time - lastSeenAt > 0.25f) return reported;

        Vector3 body = lastFeet + Vector3.up * 0.5f;
        return (reported - body).sqrMagnitude > Teleport * Teleport ? body : reported;
    }

    /// <summary>بعد حركة اللاعب في <c>Update</c>، فيُقرأ موضعه الأخير لهذا الإطار.</summary>
    private void LateUpdate()
    {
        if (juice == null || !juice.Live || Time.time < quietUntil)
        {
            Forget();
            return;
        }

        CharacterController body = juice.Player != null ? juice.Controller : null;
        if (body == null || !body.enabled || juice.PlayerDead)
        {
            Forget();
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // موقوف: لا شيء يتحرّك، ولا يُنسى شيء
        if (dt > MaxStep)
        {
            Forget();
            return;
        }

        Bounds box = body.bounds;
        var feet = new Vector3(box.center.x, box.min.y, box.center.z);
        bool grounded = Probe(box, out RaycastHit ground);
        Vector3 carried = Carried(grounded, ground);

        if (!tracking || (feet - lastFeet).sqrMagnitude > Teleport * Teleport)
        {
            // أوّل إطار، أو نُقل: من هنا نبدأ، ولا قفزة تُنسب إلى ما قبله
            tracking = true;
            airborne = !grounded;
            jumped = true;
            kicking = false;
            running = false;
            leftAt = Time.time;
            peakFall = 0f;
            speed = 0f;
            lastFeet = feet;
            lastSeenAt = Time.time;
            if (grounded) lastGround = OnGround(feet, ground);
            return;
        }

        Vector3 delta = feet - lastFeet;
        lastFeet = feet;
        lastSeenAt = Time.time;

        if (!grounded)
        {
            Fly(feet, delta.y / dt);
            return;
        }

        Vector3 spot = OnGround(feet, ground);
        if (airborne) Landed(spot);
        lastGround = spot;
        Run(spot, (delta - carried) / dt, dt);
    }

    /// <summary>في الهواء: أسرع سقوطٍ حتى الآن، وهل ارتفع كمن قفز — من الأرض أو في الهواء.</summary>
    private void Fly(Vector3 feet, float rise)
    {
        if (!airborne)
        {
            airborne = true;
            jumped = false;
            kicking = false;
            leftAt = Time.time;
            takeoff = lastGround;
            peakFall = 0f;
        }

        peakFall = Mathf.Max(peakFall, -rise);
        Kick(feet, rise);

        if (jumped || Time.time - leftAt > JumpWindow) return;
        if (feet.y - takeoff.y < JumpRise || rise < JumpSpeed) return;

        jumped = true;
        JumpPuff(takeoff);
    }

    /// <summary>
    /// ركلةٌ في الهواء: الصعود قفز فجأةً في الإطار الماضي وبقي — فليس دفعة إطارٍ واحد (درجةٌ
    /// رفعته، حافّةٌ لامسها). نفخةٌ حيث ركلت القدمان، وهي إقلاعٌ جديد: لا نفخة أرضٍ بعدها،
    /// والهبوط يُقاس بالسقوط بعدها لا قبلها.
    /// </summary>
    private void Kick(Vector3 feet, float rise)
    {
        if (kicking && rise > JumpSpeed)
        {
            jumped = true;
            peakFall = 0f;
            AirPuff(kickAt);
        }

        kicking = Time.time - leftAt > KickAfter && rise > JumpSpeed && rise - lastRise > KickRise;
        if (kicking) kickAt = feet;
        lastRise = rise;
    }

    /// <summary>
    /// عاد إلى الأرض. الطيرات الصغيرة (درجٌ نازل، منحدر) لا تُثير شيئًا ولا تقطع غبار الجري.
    /// </summary>
    private void Landed(Vector3 spot)
    {
        airborne = false;

        bool longFlight = Time.time - leftAt >= LongAir;
        if (peakFall < HardLanding && !(longFlight && peakFall >= SoftLanding)) return;

        calmUntil = Time.time + CalmAfterLanding;
        LandPuff(spot, peakFall);
    }

    /// <summary>
    /// على الأرض: نفخةٌ صغيرة خلف القدمين كل <see cref="RunEvery"/> وهو يجري، والأولى فور
    /// انطلاقه وأكبر قليلًا — الانطلاقة تُرى.
    /// </summary>
    private void Run(Vector3 spot, Vector3 velocity, float dt)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.01f) heading = velocity.normalized;

        // تُنعَّم: إطارٌ يرتطم بحافّةٍ أو يتأخّر لا يُطلق نفخةً ولا يقطع الإيقاع
        speed = Mathf.Lerp(speed, velocity.magnitude, 1f - Mathf.Exp(-12f * dt));

        if (speed < RunSpeed)
        {
            running = false;
            return;
        }

        // هبط وهو يجري: يكمل إيقاعه بعد نفخة الهبوط، بلا انطلاقةٍ جديدة
        if (Time.time < calmUntil)
        {
            running = true;
            return;
        }

        bool kick = !running;
        running = true;
        if (!kick && Time.time < nextDustAt) return;

        nextDustAt = Time.time + RunEvery * Random.Range(0.85f, 1.15f);
        // بالسرعة الآن لا المنعَّمة: تلك تعبر حدّ الجري لحظة الانطلاقة والقدمان قد بلغتا
        // سرعتهما، فتخرج الانطلاقة أبهت نفخة
        RunPuff(spot, heading, kick, Mathf.InverseLerp(RunSpeed, FullRun, velocity.magnitude));
    }

    // ---------- الأرض ----------

    /// <summary>
    /// هل تحت القدمين أرض؟ كرةٌ أضيق قليلًا من الكبسولة تبدأ داخلها — فلا تصطدم باللاعب
    /// نفسه — وتنزل حتى <see cref="GroundSlack"/> تحت قاعدتها. والطبقة <c>Player</c> خارج البحث.
    /// </summary>
    private bool Probe(Bounds box, out RaycastHit ground)
    {
        float radius = Mathf.Max(0.05f, Mathf.Min(box.extents.x, box.extents.z) * 0.9f);
        var origin = new Vector3(box.center.x, box.min.y + radius + ProbeLift, box.center.z);
        return Physics.SphereCast(origin, radius, Vector3.down, out ground, ProbeLift + GroundSlack,
                                  juice.GroundMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>كم تحرّك ما تحت القدمين منذ الإطار الماضي — منصّةٌ تسير تحمل اللاعب معها.</summary>
    private Vector3 Carried(bool grounded, RaycastHit ground)
    {
        Transform under = grounded ? ground.collider.transform : null;
        Vector3 moved = under != null && under == floor ? under.position - floorAt : Vector3.zero;

        floor = under;
        if (under != null) floorAt = under.position;
        return moved;
    }

    private static Vector3 OnGround(Vector3 feet, RaycastHit ground) =>
        new Vector3(feet.x, ground.point.y, feet.z);

    // ---------- الغبار ----------

    /// <summary>ورق اللعبة بشفافية: فاتحٌ يُقرأ على الأرض الرمادية، ودافئٌ داخل مناطق اللون.</summary>
    private Color Dust(float alpha)
    {
        Color color = juice.Style.paper;
        color.a = alpha;
        return color;
    }

    /// <summary>ستّ نفخاتٍ تنفرش حول القدمين لحظة الإقلاع.</summary>
    private void JumpPuff(Vector3 spot)
    {
        Color color = Dust(0.45f);
        float turn = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < 6; i++)
        {
            float a = turn + i * (Mathf.PI / 3f) + Random.Range(-0.3f, 0.3f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float size = Random.Range(0.4f, 0.6f);
            juice.Fx.Dust(spot + dir * 0.12f + Vector3.up * (size * 0.3f),
                          dir * Random.Range(0.7f, 1.1f) + Vector3.up * Random.Range(0.1f, 0.3f),
                          size, Random.Range(0.35f, 0.5f), color);
        }
    }

    /// <summary>
    /// ركلةٌ في الهواء لا أرض تحتها: ستّ نفخاتٍ صغيرة تنفرش من القدمين إلى الجوانب والأسفل —
    /// دفعةٌ على الهواء نفسه — وحلقةٌ باهتة تحتهما.
    /// </summary>
    private void AirPuff(Vector3 spot)
    {
        Color color = Dust(0.35f);
        float turn = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < 6; i++)
        {
            float a = turn + i * (Mathf.PI / 3f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            juice.Fx.Dust(spot + dir * 0.1f, dir * Random.Range(1f, 1.4f) + Vector3.down * 0.3f,
                          Random.Range(0.3f, 0.45f), Random.Range(0.3f, 0.4f), color);
        }
        juice.Fx.Ring(spot, 0.6f, 0.3f, new Color(1f, 1f, 1f, 0.3f));
    }

    /// <summary>
    /// حلقة غبارٍ تنفرش على الأرض، عددها وحجمها وسرعتها بقدر السقطة. والكبيرة يُسمع لها
    /// ويُحَسّ بها وتنخفض لها الكاميرا، وتترك حلقةً باهتة.
    /// </summary>
    private void LandPuff(Vector3 spot, float impact)
    {
        float k = Mathf.InverseLerp(5f, HugeLanding, impact);
        int count = Mathf.RoundToInt(Mathf.Lerp(6f, 16f, k));
        Color color = Dust(Mathf.Lerp(0.45f, 0.6f, k));
        float turn = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = turn + i * (Mathf.PI * 2f / count) + Random.Range(-0.2f, 0.2f);
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float size = Mathf.Lerp(0.45f, 1.1f, k) * Random.Range(0.8f, 1.2f);
            Vector3 push = dir * (Mathf.Lerp(1f, 3.2f, k) * Random.Range(0.8f, 1.2f)) +
                           Vector3.up * (Random.Range(0.15f, 0.4f) * (1f + k));
            juice.Fx.Dust(spot + dir * 0.15f + Vector3.up * (size * 0.3f), push,
                          size, Mathf.Lerp(0.45f, 0.8f, k) * Random.Range(0.9f, 1.1f), color);
        }

        if (impact < BigLanding) return;

        juice.Fx.Ring(spot + Vector3.up * ChromaJuice.RingLift, Mathf.Lerp(0.9f, 1.8f, k), 0.4f,
                      new Color(1f, 1f, 1f, 0.35f));
        // الخطوات تُسمع الهبوط بنفسها — صوتان لهبوطٍ واحد ضجيج
        if (!FootstepSounds.HandlesLandings)
            ChromaSfx.Play("Land_Puff", Mathf.Lerp(0.3f, 0.55f, k), Random.Range(0.94f, 1.06f));
        juice.Lens.Dip(Mathf.Lerp(0.08f, 0.18f, k));
    }

    /// <summary>
    /// نفخةٌ صغيرة خلف القدمين تبقى مكانها وهو يبتعد. الانطلاقة أكبر قليلًا، وكلتاهما بقدر
    /// <paramref name="pace"/> (٠ عند حدّ الجري، ١ بأسرعه): تخفت وتصغر مع البطء.
    /// </summary>
    private void RunPuff(Vector3 spot, Vector3 forward, bool kick, float pace)
    {
        Color color = Dust((kick ? 0.4f : 0.3f) * Mathf.Lerp(0.4f, 1f, pace));
        var side = new Vector3(-forward.z, 0f, forward.x);
        int count = kick ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            float size = Random.Range(0.3f, 0.45f) * (kick ? 1.25f : 1f) * Mathf.Lerp(0.8f, 1f, pace);
            juice.Fx.Dust(spot - forward * 0.15f + side * Random.Range(-0.1f, 0.1f) + Vector3.up * (size * 0.3f),
                          -forward * Random.Range(0.3f, 0.6f) + Vector3.up * Random.Range(0.2f, 0.45f),
                          size, Random.Range(0.45f, 0.6f), color);
        }
    }
}

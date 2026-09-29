using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// صوت خطوات اللاعب، في كل سين، بلا ربط.
///
/// الهب ومرحلة التوايلايت والسيرك كانت صامتة تحت قدمي اللاعب، فيبدو أنه <b>يطفو</b>
/// لا يمشي. وستيم وحدها فيها صوت — سكربت علي، مربوطٌ فيها بيده.
///
/// <b>خطوةٌ لكل قدمٍ تنزل، لا تسجيلٌ يدور.</b> تسجيل المشي الطويل كان يُسمع كأنه فيديو
/// يشتغل تحت اللاعب: إيقاعه إيقاع من سجّله لا إيقاع الشخصية. فقُطِّع إلى تسع خطواتٍ
/// منفصلة (<c>Plate_01..09</c>)، لكل واحدة تلاشٍ قصير في أولها يمنع الطقّة وذيلٌ ناعم في
/// آخرها، وتُشغَّل <b>لحظة نزول القدم في الأنميشن</b>: عظمتا القدمين في الهيكل البشري
/// تُراقَبان، والقدم التي كانت تهبط ثم ثبتت قرب أدنى نقطة لها = خطوة. فيبقى الصوت على
/// الإيقاع مهما تغيّرت سرعة المشي أو الجري.
///
/// وأوّل خطوة بعد الوقوف أخفض، ثم يعلو الصوت خلال ثلث ثانية — دخولٌ لا ضربة.
/// ولا تتكرّر نفس الخطوة مرّتين متتاليتين، وتتغيّر طبقتها وشدّتها قليلًا كل مرّة.
///
/// لو ما كان للشخصية هيكلٌ بشري: خطوةٌ كل <see cref="StrideFallback"/> من المسافة.
///
/// <b>ولا يعمل حيث يوجد صوتٌ غيره</b> (<c>A_FootSounds</c> في ستيم).
///
/// يُركّب نفسه، والأصوات في <c>Osama/Resources/Footsteps</c> فتدخل البلد يقينًا.
/// </summary>
[DisallowMultipleComponent]
public class FootstepSounds : MonoBehaviour
{
    private const string Folder = "Footsteps/";
    private const string StepPrefix = "Plate_";

    /// <summary>أقلّ سرعة تُعدّ مشيًا — دون ذلك انزلاقٌ أو دفعُ جدار.</summary>
    private const float MoveThreshold = 0.6f;

    private const float StepVolume = 0.24f;   // خطوةٌ تُحسّ ولا تطغى على الموسيقى
    private const float LandVolume = 0.45f;

    /// <summary>شدّة أوّل خطوة بعد الوقوف، ثم يعلو إلى الكامل خلال <see cref="WarmUp"/>.</summary>
    private const float FirstStepVolume = 0.55f;
    private const float WarmUp = 0.35f;

    /// <summary>كم يقف اللاعب حتى تُعدّ الخطوة التالية "أوّل خطوة".</summary>
    private const float RestBeforeFirstStep = 0.4f;

    /// <summary>أقرب مسافةٍ زمنية بين خطوتين — تمنع خطوتين من قدمٍ واحدة ترتجف.</summary>
    private const float MinStepGap = 0.2f;

    /// <summary>القدم نفسها لا تنزل مرّتين في أقلّ من هذا — ارتجاف المنحنى ليس خطوة.</summary>
    private const float FootRefractory = 0.28f;

    /// <summary>بعد صوت الهبوط لا خطوة فورًا — وإلا سُمع الهبوط مرّتين.</summary>
    private const float QuietAfterLanding = 0.2f;

    /// <summary>
    /// صوت الهبوط لهبوطٍ حقيقي فقط: طيرانٌ بهذه المدة على الأقل، أو سقوطٌ بهذه السرعة.
    /// <c>isGrounded</c> يرتجف إطارًا على المنحدر والدرج، وكل رجفةٍ كانت "هبوطًا" له صوت.
    /// </summary>
    private const float MinAirForLanding = 0.25f;
    /// <summary>
    /// ونزولٌ حقيقي من أعلى نقطةٍ بلغها: القارب يحمل اللاعب ويهبط به مع الموج، و"السرعة"
    /// وحدها كانت تُسمع هبوطًا كل موجة — أزعج صوتٍ في التوايلايت.
    /// </summary>
    private const float MinDropForLanding = 0.45f;

    /// <summary>للشخصيات بلا هيكلٍ بشري: خطوةٌ كل هذه المسافة (م).</summary>
    private const float StrideFallback = 0.85f;

    /// <summary>
    /// إن مشى اللاعب هذه المدة ولم تُكتشف قدمٌ نازلة، فالأنميشن لا يحرّك القدمين
    /// (أو الهيكل غير ما نتوقّع) — نرجع للمسافة كي لا يمشي صامتًا.
    /// </summary>
    private const float FeetGiveUpAfter = 1.2f;

    // كشف نزول القدم، نسبةً لطول الساق كي يصلح لأي حجم شخصية
    private const float DescendRate = 0.3f;     // سرعة هبوط القدم (أطوال ساق/ث) = نازلة
    private const float SettleRate = 0.1f;      // دونها = ثبتت على الأرض
    private const float ContactMargin = 0.08f;  // قرب أدنى نقطة لها (أطوال ساق)
    private const float LowRise = 0.35f;        // كم يرتفع "أدنى نقطة" بالثانية ليتبع الدرج

    /// <summary>اسم سكربت الخطوات الآخر. حيث وُجد نسكت له.</summary>
    private const string OtherFootsteps = "A_FootSounds";

    private static FootstepSounds instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("FootstepSounds") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<FootstepSounds>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>قدمٌ واحدة: ارتفاعها عن جسم اللاعب، وهل كانت تهبط.</summary>
    private class Foot
    {
        public Transform bone;
        public float lastHeight;
        public float low;
        public bool falling;
        public float lastPlant = -10f;
    }

    private readonly List<AudioClip> steps = new List<AudioClip>();
    private AudioClip land;
    private AudioSource source;

    private CharacterController controller;
    private Transform body;
    private Foot left, right;
    private float legLength = 1f;
    private bool useFeet, hasFeet;
    private Animator blendAnimator;
    private static readonly int MovementBlend = Animator.StringToHash("MovementBlend");

    /// <summary>
    /// هل هذا السكربت يُسمِع الهبوط الآن؟ مؤثّر الغبار عند السقوط الكبير يسكت له كي لا
    /// يُسمع الهبوط الواحد صوتين.
    /// </summary>
    public static bool HandlesLandings =>
        instance != null && instance.scanned && !instance.silenced && instance.land != null;

    private Vector3 previous;
    private float airTime, peakY;
    private bool wasGrounded = true;
    private bool silenced;
    private bool stepsSilenced;   // خطوات غيرنا تعمل هنا (ستيم) — نُسمع الهبوط وحده
    private bool scanned;
    private float nextScanAt;

    private float lastStepAt = -10f;
    private float quietUntil;
    private float walkingSince = -1f;
    private float stillSince;
    private float movingWithoutFeet;
    private float strideLeft;
    private int lastClip = -1;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        controller = null;
        body = null;
        left = right = null;
        scanned = false;
        silenced = false;
        stepsSilenced = false;
        nextScanAt = 0f;
        if (source != null) source.Stop();
    }

    private void Update()
    {
        if (silenced) return;
        if (!scanned && !Scan()) return;
        if (body == null) { scanned = false; return; }

        float dt = Time.deltaTime;
        if (dt <= 0f || dt > 0.1f)   // توقّف أو تهنيقة تحميل: لا نقيس سرعةً منها
        {
            previous = body.position;
            SampleFeet(0f);
            return;
        }

        // الأرض كما يراها سكربت الحركة: قوارب التوايلايت تمسح isGrounded كل إطار
        bool grounded = JumpPolish.Active ? JumpPolish.Grounded : controller == null || controller.isGrounded;

        float y = body.position.y;
        if (!grounded)
        {
            if (airTime <= 0f) peakY = y;
            airTime += dt;
            peakY = Mathf.Max(peakY, y);
        }

        bool realLanding = airTime >= MinAirForLanding && peakY - y >= MinDropForLanding;
        if (grounded) airTime = 0f;

        if (grounded && !wasGrounded && realLanding && land != null)
        {
            source.pitch = Random.Range(0.92f, 1.06f);
            source.PlayOneShot(land, LandVolume * Random.Range(0.85f, 1f));
            quietUntil = Time.time + QuietAfterLanding;
            lastStepAt = Time.time;
        }
        wasGrounded = grounded;

        Vector3 now = body.position;
        Vector3 moved = Vector3.ProjectOnPlane(now - previous, Vector3.up);   // الأفقي وحده
        previous = now;

        // السرعة من المسافة المقطوعة لا من الإدخال: تعمل مع أي سكربت حركة، ومع
        // اليد والكيبورد، ومع من يُدفع أو يُسحب
        float speed = moved.magnitude / dt;
        // ويمشي فعلًا: القارب يحمل الواقف فتتغيّر مسافته وقدماه ساكنتان
        bool striding = blendAnimator == null || blendAnimator.GetFloat(MovementBlend) > 0.3f;
        bool walking = grounded && speed >= MoveThreshold && striding;
        if (stepsSilenced) return;

        if (walking)
        {
            if (walkingSince < 0f) walkingSince = Time.time;
        }
        else if (walkingSince >= 0f)
        {
            walkingSince = -1f;
            stillSince = Time.time;
            strideLeft = StrideFallback * 0.5f;   // الخطوة الأولى بعد نصف خطوة، لا فورًا
            movingWithoutFeet = 0f;
            useFeet = hasFeet;   // يُعاد تفعيل القدمين: التخلّي عنهما لحظةٌ لا للسين كله
        }

        bool footDown = SampleFeet(dt);
        if (!walking) return;

        if (useFeet)
        {
            if (footDown) { Step(); movingWithoutFeet = 0f; }
            else if ((movingWithoutFeet += dt) > FeetGiveUpAfter) useFeet = false;
            return;
        }

        strideLeft -= moved.magnitude;
        if (strideLeft <= 0f)
        {
            strideLeft += StrideFallback;
            Step();
        }
    }

    /// <summary>هل نزلت قدمٌ الآن؟ تُحدَّث القدمان كل إطار حتى وهو واقف كي لا تقفز قراءتهما.</summary>
    private bool SampleFeet(float dt)
    {
        if (!useFeet || left == null || left.bone == null || right.bone == null) return false;

        bool l = Landed(left, right, dt);
        bool r = Landed(right, left, dt);
        return l || r;
    }

    /// <summary>
    /// القدم كانت تهبط، والآن ثبتت قرب أدنى نقطةٍ لها = لامست الأرض. الارتفاع نسبةً
    /// لجسم اللاعب فالصعود على الدرج لا يُحسب هبوطًا، والحدود بأطوال الساق فتصلح لأي حجم.
    /// </summary>
    private bool Landed(Foot foot, Foot other, float dt)
    {
        float h = foot.bone.position.y - body.position.y;
        if (dt <= 0f) { foot.lastHeight = h; return false; }

        float rate = (h - foot.lastHeight) / dt / legLength;
        foot.lastHeight = h;
        foot.low = Mathf.Min(foot.low + LowRise * legLength * dt, h);

        if (rate < -DescendRate)
        {
            foot.falling = true;
            return false;
        }

        // القدم النازلة فعلًا هي السفلى من الاثنتين؛ والأخرى في الهواء أو تغادر
        if (foot.falling && rate > -SettleRate && h < foot.low + ContactMargin * legLength &&
            h <= other.lastHeight + 0.02f * legLength && Time.time - foot.lastPlant >= FootRefractory)
        {
            foot.falling = false;
            foot.lastPlant = Time.time;
            return true;
        }
        return false;
    }

    private void Step()
    {
        if (steps.Count == 0) return;
        if (Time.time < quietUntil || Time.time - lastStepAt < MinStepGap) return;

        // أوّل خطوةٍ بعد وقفة أخفض، ثم يعلو الصوت تدريجيًّا
        float volume = StepVolume;
        bool fromRest = Time.time - lastStepAt > RestBeforeFirstStep + MinStepGap &&
                        Time.time - stillSince >= RestBeforeFirstStep;
        float walked = walkingSince >= 0f ? Time.time - walkingSince : 0f;
        if (fromRest) volume *= FirstStepVolume;
        else if (walked < WarmUp) volume *= Mathf.Lerp(FirstStepVolume, 1f, walked / WarmUp);

        // لا تتكرّر نفس الخطوة مرّتين متتاليتين
        int pick = Random.Range(0, steps.Count);
        if (steps.Count > 1 && pick == lastClip) pick = (pick + 1 + Random.Range(0, steps.Count - 1)) % steps.Count;
        lastClip = pick;

        source.pitch = Random.Range(0.94f, 1.06f);
        source.PlayOneShot(steps[pick], volume * Random.Range(0.85f, 1f));
        lastStepAt = Time.time;
    }

    /// <summary>
    /// يلقى اللاعب والأصوات. المحاولة مرّة في الثانية لا كل إطار: بعض السينات بلا
    /// لاعبٍ أصلًا (القائمة، الانترو).
    /// </summary>
    private bool Scan()
    {
        if (Time.unscaledTime < nextScanAt) return false;
        nextScanAt = Time.unscaledTime + 1f;

        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (script == null || script.GetType().Name != OtherFootsteps) continue;

            stepsSilenced = true;   // صوتان لخطوةٍ واحدة أسوأ من لا صوت — والهبوط لا صوت له عندهم
            break;
        }

        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return false;

        controller = player.GetComponentInParent<CharacterController>();
        body = controller != null ? controller.transform : player.transform;

        if (steps.Count == 0)
        {
            for (int i = 1; i <= 32; i++)
            {
                var clip = Resources.Load<AudioClip>($"{Folder}{StepPrefix}{i:00}");
                if (clip == null) break;
                steps.Add(clip);
            }
            land = Resources.Load<AudioClip>(Folder + "Step_Land");
        }

        if (steps.Count == 0)
        {
            Debug.LogWarning($"[FootstepSounds] ما لقيت {StepPrefix}01 في Osama/Resources/{Folder} " +
                             "— لا صوت للخطوات.", this);
            silenced = true;
            return false;
        }

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // كاميرا اللعبة بعيدة عن اللاعب، والمجسّم لا يُسمع
        }

        FindFeet();

        previous = body.position;
        airTime = 0f;
        wasGrounded = true;
        walkingSince = -1f;
        stillSince = -10f;
        strideLeft = StrideFallback * 0.5f;
        movingWithoutFeet = 0f;
        scanned = true;
        return true;
    }

    /// <summary>عظمتا القدمين من هيكل الشخصية البشري، إن وُجد.</summary>
    private void FindFeet()
    {
        left = right = null;
        useFeet = hasFeet = false;
        blendAnimator = null;

        Animator animator = body.GetComponentInChildren<Animator>();
        if (animator == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == MovementBlend && p.type == AnimatorControllerParameterType.Float) blendAnimator = animator;

        if (!animator.isHuman) return;

        Transform l = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform r = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (l == null || r == null) return;

        legLength = hips != null ? Mathf.Max(0.2f, hips.position.y - Mathf.Min(l.position.y, r.position.y)) : 0.9f;

        float bodyY = body.position.y;
        left = new Foot { bone = l, lastHeight = l.position.y - bodyY, low = l.position.y - bodyY };
        right = new Foot { bone = r, lastHeight = r.position.y - bodyY, low = r.position.y - bodyY };
        useFeet = hasFeet = true;
    }
}

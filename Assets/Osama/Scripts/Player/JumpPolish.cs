using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// يُصلح "القلتش" في القفز والهبوط — في كل المراحل، من الخارج، بلا سطرٍ في سكربتات سلطان
/// وعلي ولا في أنميتر علي.
///
/// <b>أربع علل، كلها في التقاء سكربت الحركة بالأنميتر:</b>
///
/// ١. <b>الأنميتر لا يقبل المقاطعة.</b> كل انتقالاته <c>Interruption Source = None</c>،
/// وقفزة AnyState لا تقطع مزجًا جاريًا (٠٫٢٥ ث). فمن قفز بُعيد هبوطه ارتفع بوضعية الهبوط
/// ثم انقلب لأنميشن القفز في الهواء. <b>الإصلاح</b>: لحظة القفزة، إن كان الأنميتر في مزج،
/// نقفز إلى حالة القفز فورًا ونمسح الزناد كي لا ينطلق متأخّرًا بعد الهبوط.
///
/// ٢. <b><c>isGrounded</c> يرتجف.</b> سكربت الحركة يلصق اللاعب بالأرض بـ −٢ م/ث فقط، فعلى
/// المنحدر والدرج وحافّة الأرض يصير "في الهواء" إطارًا ويرجع، و<c>InAir</c> يتبعه حرفيًّا.
/// <b>الإصلاح</b>: الأنميتر لا يصدّق أن اللاعب في الهواء إلا بعد ١٢٠ مللي ث، أو إن كان
/// يصعد/يسقط فعلًا بسرعة. الهبوط يُصدَّق فورًا.
///
/// ٣. <b>حالة الهبوط تعلق.</b> لا انتقال من الهبوط رجوعًا للسقوط: من هبط على حافّة وانزلق
/// عنها بقي بوضعية الهبوط وهو يسقط. وقفزةٌ صُدّت (سقفٌ منخفض) تُبقيه بوضعية القفز على
/// الأرض. <b>الإصلاح</b>: من علِق في إحداهما أكثر من لحظة نعيده للحالة الصحيحة.
///
/// ٤. <b>قفزة ثانية في الهواء.</b> نافذة التسامح (نصف ثانية بعد مغادرة الأرض) لا تُغلق حين
/// تُستهلك قفزة، فضغطتان سريعتان = قفزتان. <see cref="DoubleJumpGuard"/> كان يغلقها في
/// الهب وحده؛ هنا تُغلق في كل مرحلة، ولحظة القفزة نفسها (من حدث القفز) لا بعد أن يرتفع.
/// المشي عن الحافّة لا يُطلق الحدث، فتسامحه باقٍ كما وُضع.
///
/// كل الوصول <b>بالاسم عبر الانعكاس</b>: إن غُيّر اسمٌ سكت هذا السكربت وبقي كل شيء كما كان.
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DefaultExecutionOrder(500)]   // بعد سكربت الحركة (0) الذي يكتب InAir، وقبل أن يقيّم الأنميتر
[DisallowMultipleComponent]
public class JumpPolish : MonoBehaviour
{
    private const string MovementScript = "PlayerController";
    private const string JumpScript = "A_CrouchAndJump";

    private const string JumpState = "A_Jump_Idle_Masc";
    private const string FallState = "A_InAir_FallLarge_Masc";
    private const string LandState = "A_Land_Idle_Masc";
    private const string MoveState = "Movement";

    private static readonly int InAirParam = Animator.StringToHash("InAir");
    private static readonly int JumpTrigger = Animator.StringToHash("Jump");

    /// <summary>كم يبقى بلا أرض قبل أن يصدّق الأنميتر أنه في الهواء.</summary>
    private const float AirDebounce = 0.12f;
    /// <summary>صعودٌ بهذه السرعة (م/ث) = قفزةٌ الآن، بلا انتظار.</summary>
    private const float RisingSpeed = 1.5f;
    /// <summary>سقوطٌ بهذه السرعة (م/ث) = سقوطٌ حقيقي، بلا انتظار.</summary>
    private const float FallingSpeed = -4f;
    private const float StuckInLand = 0.15f;
    private const float StuckInJump = 0.3f;
    private const float JumpBlend = 0.06f;

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // قيمٌ معلّبة مرّة واحدة: الكتابة بالانعكاس كل إطارٍ في الهواء بلا تخصيص ذاكرة
    private static readonly object No = false;
    private static readonly object Shut = -1f;

    /// <summary>يعمل الآن على لاعب هذا السين — <see cref="DoubleJumpGuard"/> يسكت له.</summary>
    public static bool Active { get; private set; }

    private static JumpPolish instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("JumpPolish") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<JumpPolish>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Active = false;
    }

    // ما رُبط في هذا السين
    private CharacterController controller;
    private Transform body;
    private Animator animator;
    private Component movement;
    private FieldInfo canMove;
    private Component jumper;
    private FieldInfo canJump, window;
    private EventInfo jumpEvent;
    private Delegate jumpHandler;
    private bool bound, giveUp;
    private float nextBindAt;

    private int jumpHash, fallHash, landHash, moveHash;
    private bool hasJump, hasFall, hasLand, hasMove, hasInAir;

    // الحالة
    private bool jumpedThisAir;
    private float jumpedAt;
    private float airFor, lastY;
    private float stuckLand, stuckJump;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unbind();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        Unbind();
        giveUp = false;
        nextBindAt = 0f;
    }

    private void Update()
    {
        if (!bound && !Bind()) return;
        if (controller == null || body == null || jumper == null)   // دُمّر مع سينه
        {
            Unbind();
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // موقوف: لا شيء يتحرّك، ولا نلمس الأنميتر

        bool grounded = controller.isGrounded;
        float y = body.position.y;
        float vy = (y - lastY) / dt;
        lastY = y;

        KeepWindowClosed(grounded);

        airFor = grounded ? 0f : airFor + dt;
        bool air = !grounded && (airFor >= AirDebounce || vy > RisingSpeed || vy < FallingSpeed);

        if (animator == null || !animator.isActiveAndEnabled) return;
        if (hasInAir) animator.SetBool(InAirParam, air);
        Unstick(air, dt);
    }

    /// <summary>بعد قفزةٍ حقيقية تبقى نافذة التسامح مغلقةً ما دام في الهواء.</summary>
    private void KeepWindowClosed(bool grounded)
    {
        if (!jumpedThisAir) return;

        if (!grounded)
        {
            canJump.SetValue(jumper, No);
            window.SetValue(jumper, Shut);
        }
        else if (Time.time - jumpedAt > 0.3f)
        {
            jumpedThisAir = false;   // هبط — أو القفزة لم تقع أصلًا
        }
    }

    /// <summary>حالةٌ لا تناسب ما يحدث فعلًا، ولا انتقال في الأنميتر يخرجها منها.</summary>
    private void Unstick(bool air, float dt)
    {
        if (animator.IsInTransition(0)) { stuckLand = stuckJump = 0f; return; }

        int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        if (hasLand && hasFall && state == landHash && air)
        {
            stuckLand += dt;
            if (stuckLand > StuckInLand) { animator.CrossFadeInFixedTime(fallHash, 0.15f, 0); stuckLand = 0f; }
        }
        else stuckLand = 0f;

        if (hasJump && hasMove && state == jumpHash && !air)
        {
            stuckJump += dt;
            if (stuckJump > StuckInJump) { animator.CrossFadeInFixedTime(moveHash, 0.2f, 0); stuckJump = 0f; }
        }
        else stuckJump = 0f;
    }

    /// <summary>
    /// حدث القفز من سكربت علي — يُنادى بعد سكربت الحركة (اشتركنا بعده)، فزناد القفز
    /// مضبوطٌ الآن. إن كان الأنميتر في مزجٍ لا يُقاطَع نقفز للحالة بأيدينا.
    /// </summary>
    private void OnJump(float force, bool canDoubleJump, bool jumpAllowed)
    {
        if (canMove != null && !(bool)canMove.GetValue(movement)) return;   // مجمَّد: لم يقفز

        jumpedThisAir = true;
        jumpedAt = Time.time;

        if (!jumpAllowed || !hasJump || animator == null || !animator.isActiveAndEnabled) return;
        if (!animator.IsInTransition(0)) return;   // AnyState سيلتقطها بنفسه هذا الإطار

        animator.ResetTrigger(JumpTrigger);
        animator.CrossFadeInFixedTime(jumpHash, JumpBlend, 0);
    }

    // ---------- الربط ----------

    private bool Bind()
    {
        if (giveUp || Time.unscaledTime < nextBindAt) return false;
        nextBindAt = Time.unscaledTime + 1f;

        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return false;

        controller = player.GetComponentInParent<CharacterController>();
        if (controller == null) return false;
        body = controller.transform;

        movement = FindScript(controller, MovementScript);
        jumper = FindScript(controller, JumpScript);
        if (movement == null || jumper == null) { giveUp = true; return false; }   // لاعبٌ آخر (مقطع 2.5D)

        canMove = Field(movement, "CanMove", typeof(bool));
        canJump = Field(jumper, "canJump", typeof(bool));
        window = Field(jumper, "_currentWidow", typeof(float));
        jumpEvent = jumper.GetType().GetEvent("OnJump", Any);
        if (canJump == null || window == null || jumpEvent == null)
            return Fail("تغيّرت أسماء سكربت القفز");

        try
        {
            MethodInfo method = GetType().GetMethod(nameof(OnJump), BindingFlags.Instance | BindingFlags.NonPublic);
            jumpHandler = Delegate.CreateDelegate(jumpEvent.EventHandlerType, this, method);
            jumpEvent.AddEventHandler(jumper, jumpHandler);
        }
        catch (Exception e)
        {
            return Fail("ما نفع الاشتراك في OnJump: " + e.Message);
        }

        FieldInfo animatorField = Field(movement, "animator", typeof(Animator));
        animator = animatorField != null ? animatorField.GetValue(movement) as Animator : null;
        if (animator == null) animator = controller.GetComponentInChildren<Animator>();
        ReadStates();

        lastY = body.position.y;
        airFor = 0f;
        jumpedThisAir = false;
        bound = true;
        Active = true;
        return true;
    }

    /// <summary>أيّ الحالات والمعاملات موجودة فعلًا — ما غاب يُتخطّى إصلاحه بصمت.</summary>
    private void ReadStates()
    {
        hasJump = hasFall = hasLand = hasMove = hasInAir = false;
        if (animator == null || animator.runtimeAnimatorController == null) return;

        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == InAirParam && p.type == AnimatorControllerParameterType.Bool) hasInAir = true;

        jumpHash = Animator.StringToHash(JumpState);
        fallHash = Animator.StringToHash(FallState);
        landHash = Animator.StringToHash(LandState);
        moveHash = Animator.StringToHash(MoveState);
        hasJump = animator.HasState(0, jumpHash);
        hasFall = animator.HasState(0, fallHash);
        hasLand = animator.HasState(0, landHash);
        hasMove = animator.HasState(0, moveHash);
    }

    private void Unbind()
    {
        if (jumpEvent != null && jumpHandler != null && jumper != null)
        {
            try { jumpEvent.RemoveEventHandler(jumper, jumpHandler); }
            catch (Exception) { }
        }

        controller = null;
        body = null;
        animator = null;
        movement = jumper = null;
        jumpEvent = null;
        jumpHandler = null;
        bound = false;
        Active = false;
    }

    private bool Fail(string why)
    {
        Debug.LogWarning($"[JumpPolish] {why} — عُطّل الإصلاح، والقفز يعمل كما كان.", this);
        Unbind();
        giveUp = true;
        return false;
    }

    private static Component FindScript(Component root, string typeName)
    {
        foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    private static FieldInfo Field(Component target, string name, Type type)
    {
        FieldInfo f = target.GetType().GetField(name, Any);
        return f != null && f.FieldType == type ? f : null;
    }
}

using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// يسدّ ثغرات السلّم (ستيم والتوايلايت) من الخارج — بلا سطرٍ في سكربت السلّم عند علي.
///
/// ١. <b>النزول للأسفل يعلّق اللاعب.</b> بداية السلّم تُدخله وضع التسلّق فقط، والخروج في
/// الأعلى وحده. فمن صعد قليلًا ثم نزل وصل الأرض وحركته مقفلة: لا يمشي ولا يقفز حتى يصعد
/// السلّم كله. <b>الإصلاح</b>: من يضغط للأسفل وقدماه على الأرض يُترك من السلّم.
///
/// ٢. <b>الأنميشن يعلق بوضعية التسلّق.</b> سلّمٌ قصير يبلغ اللاعب أعلاه قبل أن ينتهي
/// أنميشن بداية التسلّق، فينطفئ <c>isOnLadder</c> ولا انتقال في الأنميتر يخرج من تلك
/// الحالة — فيمشي بوضعية المتسلّق. <b>الإصلاح</b>: حالة تسلّقٍ بلا سلّم تُعاد للحركة.
///
/// ٣. <b>الموت على السلّم.</b> من يُبعث بعد موتٍ وهو يتسلّق يبقى مقفلًا عند نقطة البعث.
/// <b>الإصلاح</b>: قفزةٌ في الموضع أكبر من أي تسلّق = بعث — يُترك من السلّم.
///
/// كل الوصول <b>بالاسم عبر الانعكاس</b>: إن غُيّر اسمٌ سكت هذا السكربت وبقي السلّم كما كان.
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DefaultExecutionOrder(600)]   // بعد سكربت السلّم الذي يحرّك اللاعب هذا الإطار
[DisallowMultipleComponent]
public class LadderAssist : MonoBehaviour
{
    private const string LadderScript = "LadderController";
    private const string MovementScript = "PlayerController";

    /// <summary>أدنى من هذا في مدخل السلّم = يضغط للأسفل.</summary>
    private const float DownInput = -0.1f;
    /// <summary>كم يبقى على الأرض ضاغطًا للأسفل قبل أن يُترك — لا ضغطة عابرة.</summary>
    private const float StepOffAfter = 0.08f;
    private const float StuckPose = 0.3f;
    /// <summary>أكبر من أي تسلّق في إطار — مسافةٌ كهذه بعثٌ أو نقل.</summary>
    private const float TeleportDistance = 2.5f;

    private static readonly int OnLadderParam = Animator.StringToHash("isOnLadder");
    private static readonly int StartTrigger = Animator.StringToHash("StartLadder");
    private static readonly int EndTrigger = Animator.StringToHash("EndLadder");
    private static readonly int DirParam = Animator.StringToHash("MovementDirInLadder");
    private static readonly int StartState = Animator.StringToHash("Ladder_Up_Start_InPlace");
    private static readonly int PlayState = Animator.StringToHash("Ladder_Up_Play_InPlace");
    private static readonly int MoveState = Animator.StringToHash("Movement");

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly object No = false;
    private static readonly object Yes = true;

    private static LadderAssist instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("LadderAssist") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<LadderAssist>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private CharacterController controller;
    private Transform body;
    private Component ladder, movement;
    private FieldInfo onLadder, gettingOut, input, audioField, canMove;
    private Animator animator;
    private bool bound, giveUp, hasMoveState;
    private int groundMask = Physics.DefaultRaycastLayers;
    private float nextBindAt;

    private Vector3 lastPosition;
    private float downOnGround, stuckPose;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bound = giveUp = false;
        nextBindAt = 0f;
        controller = null;
        ladder = movement = null;
        animator = null;
    }

    private void Update()
    {
        if (!bound && !Bind()) return;
        if (controller == null || body == null || ladder == null) { bound = false; return; }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 position = body.position;
        bool teleported = (position - lastPosition).sqrMagnitude > TeleportDistance * TeleportDistance;
        lastPosition = position;

        // معامل الأنميتر بوّابةٌ بلا تخصيص ذاكرة؛ الانعكاس لا يُقرأ إلا والسلّم محتمل
        bool maybeOnLadder = animator == null || animator.GetBool(OnLadderParam);
        if (maybeOnLadder && (bool)onLadder.GetValue(ladder))
        {
            stuckPose = 0f;
            if (teleported) { Release(); return; }

            bool pressingDown = (float)input.GetValue(ladder) < DownInput;
            downOnGround = pressingDown && FeetOnGround() ? downOnGround + dt : 0f;
            if (downOnGround >= StepOffAfter) Release();
            return;
        }

        downOnGround = 0f;
        FreeStuckPose(dt);
    }

    /// <summary>حالة تسلّقٍ والسلّم انتهى، ولا خروجٌ من الأعلى جارٍ — تعود للحركة.</summary>
    private void FreeStuckPose(float dt)
    {
        if (!hasMoveState || animator == null || !animator.isActiveAndEnabled || animator.IsInTransition(0))
        {
            stuckPose = 0f;
            return;
        }
        int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (state != StartState && state != PlayState) { stuckPose = 0f; return; }
        if (gettingOut != null && (bool)gettingOut.GetValue(ladder)) { stuckPose = 0f; return; }

        stuckPose += dt;
        if (stuckPose < StuckPose) return;

        animator.CrossFadeInFixedTime(MoveState, 0.2f, 0);
        stuckPose = 0f;
    }

    /// <summary>
    /// قدماه على أرض؟ فحصٌ فيزيائي تحت الكبسولة لا <c>isGrounded</c>: على السلّم لا يُحرَّك
    /// الكونترولر للأسفل إلا بسكربت علي، وقوارب التوايلايت تمسح isGrounded كل إطار.
    /// </summary>
    private bool FeetOnGround()
    {
        if (controller.isGrounded) return true;

        Vector3 scale = body.lossyScale;
        float radius = controller.radius * Mathf.Max(scale.x, scale.z) * 0.9f;
        Vector3 center = body.TransformPoint(controller.center);
        Vector3 low = center - Vector3.up * (controller.height * 0.5f * scale.y - radius - 0.05f);
        return Physics.SphereCast(low, radius, Vector3.down, out _, 0.2f, groundMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>يترك السلّم كما يفعل الخروج من أعلاه تمامًا، بلا دفعة الخروج.</summary>
    private void Release()
    {
        onLadder.SetValue(ladder, No);
        downOnGround = 0f;

        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.ResetTrigger(StartTrigger);
            animator.ResetTrigger(EndTrigger);
            animator.SetBool(OnLadderParam, false);
            animator.SetFloat(DirParam, 0f);
            if (hasMoveState) animator.CrossFadeInFixedTime(MoveState, 0.2f, 0);
        }

        if (audioField != null && audioField.GetValue(ladder) is AudioSource climb && climb != null && climb.isPlaying)
            climb.Stop();

        if (canMove != null && movement != null) canMove.SetValue(movement, Yes);
    }

    private bool Bind()
    {
        if (giveUp || Time.unscaledTime < nextBindAt) return false;
        nextBindAt = Time.unscaledTime + 1f;

        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return false;

        controller = player.GetComponentInParent<CharacterController>();
        if (controller == null) return false;
        body = controller.transform;

        ladder = FindScript(controller, LadderScript);
        movement = FindScript(controller, MovementScript);
        if (ladder == null || movement == null) { giveUp = true; return false; }   // لاعبٌ بلا سلالم

        onLadder = Field(ladder, "AlreadyOnLadderNow", typeof(bool));
        gettingOut = Field(ladder, "isGetingOutOftheLadder", typeof(bool));
        input = Field(ladder, "input", typeof(float));
        audioField = Field(ladder, "climbAudio", typeof(AudioSource));
        canMove = Field(movement, "CanMove", typeof(bool));
        if (onLadder == null || input == null || canMove == null)
        {
            Debug.LogWarning("[LadderAssist] تغيّرت أسماء سكربت السلّم — عُطّل المساعد، والسلّم يعمل كما كان.", this);
            giveUp = true;
            return false;
        }

        FieldInfo animatorField = Field(ladder, "animator", typeof(Animator));
        animator = animatorField != null ? animatorField.GetValue(ladder) as Animator : null;
        if (animator == null) animator = controller.GetComponentInChildren<Animator>();
        hasMoveState = animator != null && animator.runtimeAnimatorController != null &&
                       animator.HasState(0, MoveState);

        int playerLayer = LayerMask.NameToLayer("Player");
        groundMask = Physics.DefaultRaycastLayers & ~(playerLayer >= 0 ? 1 << playerLayer : 0) & ~(1 << controller.gameObject.layer);

        lastPosition = body.position;
        downOnGround = stuckPose = 0f;
        bound = true;
        return true;
    }

    private static Component FindScript(Component root, string typeName)
    {
        foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    private static FieldInfo Field(Component target, string name, System.Type type)
    {
        FieldInfo f = target.GetType().GetField(name, Any);
        return f != null && f.FieldType == type ? f : null;
    }
}

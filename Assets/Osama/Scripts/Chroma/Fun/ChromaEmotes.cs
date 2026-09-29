using System.Reflection;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>تلويح وتصفيق</b>: Q / سهم تحت = يلوّح، R / سهم فوق = يصفّق — يلتفت للكاميرا ويؤدّيها
/// مرّتين ثم يعود.
///
/// أنميتر علي بلا حالاتٍ لهذا ولا نلمسه: رسمٌ بياني (Playables) يمزج متحكّم الأنميتر
/// نفسه مع الحركة بتدرّجٍ دخولًا وخروجًا (٠٫٢ ث)، ثم يُهدم فيعود الأنميتر وحده. نفس فكرة
/// تلويح الكريديت، بلا قفزةٍ حادّة.
///
/// لا يبدأ في الهواء، ولا على سلّمٍ أو زيبلاين، ولا منحنيًا أو ميّتًا أو مجمّدًا، ولا فوق
/// خزانةٍ أو تصوير. وأيّ حركةٍ أو قفزة تقطعه بلطف.
/// </summary>
[DisallowMultipleComponent]
public class ChromaEmotes : MonoBehaviour
{
    private const float BlendIn = 0.2f, BlendOut = 0.25f;
    private const int Loops = 2;
    private const float TurnSpeed = 6f;

    private static readonly int CrouchParam = Animator.StringToHash("IsCrouch");
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>يؤدّي حركةً الآن.</summary>
    public static bool Playing { get; private set; }

    private static ChromaEmotes instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaEmotes") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaEmotes>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Playing = false;
    }

    private AnimationClip wave, clap;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private Transform body;
    private Component mover;
    private FieldInfo canMove;
    private bool froze;
    private float started, length;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Stop();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Stop();

    private void Update()
    {
        if (Playing) { Tick(); return; }
        if (ChromaEvents.Quiet || Time.timeScale <= 0f || ChromaWardrobe.IsOpen || ChromaPhotoMode.IsOpen) return;

        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        bool waveNow = (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame);
        bool clapNow = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame);
        if (waveNow) TryStart("wave");
        else if (clapNow) TryStart("clap");
    }

    private void TryStart(string emote)
    {
        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return;
        var controller = player.GetComponentInParent<CharacterController>();
        if (controller == null || !controller.enabled) return;
        body = controller.transform;

        mover = Script(body, "PlayerController");
        canMove = mover != null ? mover.GetType().GetField("CanMove", Any) : null;
        if (canMove == null || canMove.FieldType != typeof(bool) || !(bool)canMove.GetValue(mover)) return;

        bool grounded = JumpPolish.Active ? JumpPolish.Grounded : controller.isGrounded;
        if (!grounded || Flag(body, "LadderController", "AlreadyOnLadderNow") ||
            Flag(body, "A_ZipLineSystem", "WeAreInZipLine")) return;

        var killable = body.GetComponentInChildren<PlayerKillable>();
        if (killable != null && killable.IsDead) return;

        Animator animator = mover.GetType().GetField("animator", Any)?.GetValue(mover) as Animator;
        if (animator == null) animator = body.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
        if (HasBool(animator, CrouchParam) && animator.GetBool(CrouchParam)) return;

        AnimationClip clip = Clip(emote);
        if (clip == null) return;

        // المتحكّم نفسه في المدخل الأوّل (واقفٌ فحالته السكون)، والحركة في الثاني
        graph = PlayableGraph.Create("ChromaEmote");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Emote", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController), 0, mixer, 0);
        graph.Connect(AnimationClipPlayable.Create(graph, clip), 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        output.SetSourcePlayable(mixer);
        graph.Play();

        canMove.SetValue(mover, false);
        froze = true;
        length = Mathf.Max(0.5f, clip.length * Loops);
        started = Time.time;
        Playing = true;
        ChromaFunEvents.RaiseEmote(emote);
    }

    private void Tick()
    {
        if (!graph.IsValid() || body == null) { Stop(); return; }
        if (Time.timeScale <= 0f) return;   // موقوف: الرسم متجمّد معه

        float t = Time.time - started;
        if (Interrupted() && t < length - BlendOut)
        {
            // يبدأ الخروج من وزنه الحالي، بلا قفزة
            float w = Weight(t);
            started = Time.time - (length - BlendOut * w);
            t = Time.time - started;
        }
        if (t >= length) { Stop(); return; }

        float weight = Weight(t);
        mixer.SetInputWeight(0, 1f - weight);
        mixer.SetInputWeight(1, weight);

        // يلتفت للكاميرا أفقيًّا
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 flat = Vector3.ProjectOnPlane(cam.transform.position - body.position, Vector3.up);
            if (flat.sqrMagnitude > 0.01f)
                body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(flat, Vector3.up),
                                                 1f - Mathf.Exp(-TurnSpeed * Time.deltaTime));
        }
    }

    private float Weight(float t) =>
        t < BlendIn ? t / BlendIn : t > length - BlendOut ? Mathf.Clamp01((length - t) / BlendOut) : 1f;

    /// <summary>حركةٌ أو قفزة من اللاعب — يريد أن يمشي، فتنتهي الحركة.</summary>
    private static bool Interrupted()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                           kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed ||
                           kb.rightArrowKey.isPressed || kb.spaceKey.isPressed)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.09f || pad.buttonSouth.isPressed);
    }

    private void Stop()
    {
        if (graph.IsValid()) graph.Destroy();
        if (froze && mover != null && canMove != null) canMove.SetValue(mover, true);
        froze = false;
        Playing = false;
    }

    private AnimationClip Clip(string emote)
    {
        if (emote == "wave") return wave != null ? wave : wave = Resources.Load<AnimationClip>("CreditsWaveOne");
        return clap != null ? clap : clap = Resources.Load<AnimationClip>("Emotes/Clap");
    }

    private static Component Script(Component root, string typeName)
    {
        foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    private static bool Flag(Component root, string typeName, string field)
    {
        Component c = Script(root, typeName);
        FieldInfo f = c != null ? c.GetType().GetField(field, Any) : null;
        return f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(c);
    }

    private static bool HasBool(Animator animator, int hash)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool) return true;
        return false;
    }
}

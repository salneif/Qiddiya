using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// <b>يلاحظ من علق</b> — فلا يحتاج اللاعب أن يعرف أن للإيقاف زرّ «Respawn»:
///
/// <list type="bullet">
/// <item><b>عالق</b>: يضغط الحركة ٣٫٥ ث ولم يتحرّك ربع متر (جدارٌ ابتلعه، زاويةٌ حبسته) — سطرٌ صغير
/// أسفل الشاشة: <c>STUCK?  ESC &gt; RESPAWN</c>. يذوب أوّل ما يتحرّك.</item>
/// <item><b>سقط من العالم</b> (قلتش أرضية): في الهواء أكثر من ٥ ث ونزل ٣٠ م تحت آخر أرض — يرجع
/// وحده لآخر نقطة حفظ، ولافتةٌ تقول ما حدث.</item>
/// <item><b>موقعٌ مكسور</b> (NaN): يرجع فورًا.</item>
/// </list>
///
/// لا يعمل تحت الإيقاف ولا في المشاهد الهادئة، ولا والشخصية على سلّم أو زيبلاين أو في حركة تلويح،
/// ولا و<c>CanMove</c> مطفأ (مصعد، مشهد). يُركّب نفسه.
/// </summary>
[DisallowMultipleComponent]
public class StuckWatch : MonoBehaviour
{
    private const float StuckAfter = 3.5f, StuckMove = 0.25f, HintLinger = 1.5f;
    private const float FallAfter = 5f, FallDepth = 30f, RescueCooldown = 6f;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static StuckWatch instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("StuckWatch") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<StuckWatch>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // اللاعب وما نقرؤه منه — يُلتقط مرّة لكل لاعب لا كل إطار
    private CharacterController body;
    private PlayerKillable killable;
    private Component mover, ladder, zip;
    private FieldInfo canMove, onLadder, onZip;
    private float nextFind;

    private float pushing, hintUntil, airborne, groundY, rescuedAt = -99f;
    private Vector3 pushFrom;

    private Canvas canvas;
    private CanvasGroup group;
    private TextMeshProUGUI text, shade;
    private float alpha;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        body = null;
        nextFind = 0f;
        pushing = airborne = 0f;
        hintUntil = 0f;
    }

    private void Update()
    {
        bool hint = Watch();
        Show(hint);
    }

    /// <summary>يرجع true ما دام التلميح يجب أن يظهر.</summary>
    private bool Watch()
    {
        if (ChromaEvents.Quiet || Time.timeScale <= 0f || !Bind()) { pushing = airborne = 0f; return false; }

        Vector3 p = body.transform.position;
        if (!Finite(p)) { Rescue(false); return false; }

        bool free = body.enabled && Read(mover, canMove, true) && !Read(ladder, onLadder, false) &&
                    !Read(zip, onZip, false) && !ChromaEmotes.Playing && !ChromaWardrobe.IsOpen &&
                    !ChromaPhotoMode.IsOpen && (killable == null || !killable.IsDead);

        // ---- سقوط من العالم ----
        bool grounded = JumpPolish.Active ? JumpPolish.Grounded : body.isGrounded;
        if (grounded || !free) { airborne = 0f; groundY = p.y; }
        else
        {
            airborne += Time.deltaTime;
            if (airborne > FallAfter && groundY - p.y > FallDepth) { Rescue(true); return false; }
        }

        // ---- عالق ----
        if (!free || !MoveHeld())
        {
            pushing = 0f;
            return Time.unscaledTime < hintUntil;
        }

        if (pushing <= 0f) pushFrom = p;
        pushing += Time.deltaTime;
        if (Vector3.ProjectOnPlane(p - pushFrom, Vector3.up).magnitude > StuckMove)
        {
            pushing = 0f;
            hintUntil = 0f;       // تحرّك: يذوب التلميح
            return false;
        }
        if (pushing > StuckAfter) hintUntil = Time.unscaledTime + HintLinger;
        return Time.unscaledTime < hintUntil;
    }

    private void Rescue(bool announce)
    {
        if (Time.unscaledTime - rescuedAt < RescueCooldown) return;
        rescuedAt = Time.unscaledTime;
        airborne = pushing = 0f;
        hintUntil = 0f;

        if (!StuckRescue.Respawn()) LevelRestart.Reload();
        if (!announce) return;
        ChromaFunEvents.Announce(new ChromaFunEvents.Banner
        {
            header = "OOPS",
            title = "BACK TO CHECKPOINT",
            line = "You slipped out of the world. We caught you.",
            letter = "!",
            accent = ChromaWardrobeArt.Paper,
            sfx = "Skin_Open",
        });
    }

    private bool Bind()
    {
        if (body != null && body.gameObject.activeInHierarchy) return true;
        if (Time.unscaledTime < nextFind) return false;
        nextFind = Time.unscaledTime + 1f;

        GameObject player = PlayerLocator.Find("Player");
        body = player != null ? player.GetComponentInParent<CharacterController>() : null;
        if (body == null) return false;

        killable = body.GetComponentInChildren<PlayerKillable>();
        mover = Script(body, "PlayerController");
        canMove = Field(mover, "CanMove");
        ladder = Script(body, "LadderController");
        onLadder = Field(ladder, "AlreadyOnLadderNow");
        zip = Script(body, "A_ZipLineSystem");
        onZip = Field(zip, "WeAreInZipLine");
        groundY = body.transform.position.y;
        return true;
    }

    private static Component Script(Component root, string typeName)
    {
        foreach (MonoBehaviour c in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    private static FieldInfo Field(Component c, string name)
    {
        FieldInfo f = c != null ? c.GetType().GetField(name, Any) : null;
        return f != null && f.FieldType == typeof(bool) ? f : null;
    }

    private static bool Read(Component c, FieldInfo f, bool fallback) =>
        c != null && f != null ? (bool)f.GetValue(c) : fallback;

    private static bool Finite(Vector3 v) =>
        !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
          float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

    private static bool MoveHeld()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                           kb.upArrowKey.isPressed || kb.downArrowKey.isPressed ||
                           kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed)) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.25f;
    }

    // ---------- التلميح ----------

    private void Show(bool on)
    {
        float target = on ? 1f : 0f;
        if (canvas == null && !on) return;
        Build();

        if (on) text.text = shade.text = "STUCK?   " + (InputScheme.UsingGamepad ? "OPTIONS" : "ESC") + "  >  RESPAWN";
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * (on ? 3f : 4f));
        group.alpha = alpha * alpha * (3f - 2f * alpha);
        bool visible = alpha > 0.001f;
        if (canvas.enabled != visible) canvas.enabled = visible;
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "StuckHint", -1, out group);
        Color paper = ChromaWardrobeArt.Paper, ink = ChromaWardrobeArt.Ink;

        shade = ChromaWardrobeArt.NewText(canvas.transform, "Shade", false, 32f, new Color(ink.r, ink.g, ink.b, 0.8f), TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(shade, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(3f, 21f), new Vector2(1000f, 44f));
        text = ChromaWardrobeArt.NewText(canvas.transform, "Hint", false, 32f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(text, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1000f, 44f));
        group.alpha = 0f;
        canvas.enabled = false;
    }
}

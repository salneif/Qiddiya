using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <b>تعليمٌ أوّل اللعبة</b> في ستيم تاون، بعد اسم المرحلة: سبع خطواتٍ واحدةً واحدة أسفل
/// الشاشة — مفتاحٌ ورقيّ بحبر، واسم الحركة، وسطرٌ يشرحها — والأزرار تتبع الجهاز (كيبورد/يد):
/// المشي، القفز، التفاعل، التلويح، التصفيق، الخزانة، التصوير.
///
/// كل خطوة تنتظر أن يفعلها اللاعب (أو مهلةً للتي لا تُفرض)، فتومض "NICE!" وتذوب وتأتي التالية.
/// مرّةً لكل لعبةٍ جديدة، ويختفي تحت الإيقاف والخزانة والتصوير ويكمل بعدها.
/// </summary>
[DisallowMultipleComponent]
public class ChromaTutorial : MonoBehaviour
{
    private struct Step
    {
        public string action, line, keys, pad;
        public float timeout;   // ٠ = ينتظر الفعل حتمًا
    }

    private static readonly Step[] Steps =
    {
        new Step { action = "MOVE",       line = "Walk around",                              keys = "W A S D",   pad = "LEFT STICK", timeout = 0f },
        new Step { action = "JUMP",       line = "Hop over gaps",                            keys = "SPACE",     pad = "CROSS",      timeout = 0f },
        new Step { action = "INTERACT",   line = "Levers, doors and flags",                  keys = "E",         pad = "SQUARE",     timeout = 7f },
        new Step { action = "WAVE",       line = "Say hi to the camera",                     keys = "Q",         pad = "D-PAD DOWN", timeout = 12f },
        new Step { action = "CLAP",       line = "Celebrate a little",                       keys = "R",         pad = "D-PAD UP",   timeout = 12f },
        new Step { action = "WARDROBE",   line = "Collect faces to unlock new looks",        keys = "TAB",       pad = "SELECT",     timeout = 10f },
        new Step { action = "PHOTO MODE", line = "Freeze the moment and take a picture",     keys = "P",         pad = "R3",         timeout = 10f },
    };

    private const string Level = "Steam_Final";
    private const float StartAfter = 6.2f;          // بعد اسم المرحلة (٤٫٦ ث + ستارة)
    private const float FadeIn = 0.35f, DoneHold = 0.55f, FadeOut = 0.35f, Gap = 0.5f;
    private const int Reward = 10;

    private static ChromaTutorial instance;
    private static bool doneThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaTutorial") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaTutorial>();
        DontDestroyOnLoad(host);
        instance.Begin(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        doneThisRun = false;
    }

    private bool active;
    private int step = -1;
    private float clock, stepAt, doneAt = -1f, waitUntil;
    private bool emoted, celebrate;
    private string emote;
    private Vector3 startPos;
    private float startY;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform cap, block;
    private TextMeshProUGUI capText, action, line, count, actionShade, lineShade;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ChromaBank.RunReset += OnRunReset;
        ChromaFunEvents.EmotePerformed += OnEmote;
        InputScheme.Changed += Refresh;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ChromaBank.RunReset -= OnRunReset;
        ChromaFunEvents.EmotePerformed -= OnEmote;
        InputScheme.Changed -= Refresh;
    }

    private void OnRunReset() => doneThisRun = false;

    /// <summary>«Continue»: من يكمل لعبته رأى التعليم من قبل.</summary>
    internal static void Skip()
    {
        doneThisRun = true;
        if (instance == null) return;
        instance.active = false;
        if (instance.canvas != null) instance.canvas.enabled = false;
    }
    private void OnEmote(string e) { emoted = true; emote = e; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Begin(scene);
    }

    private void Begin(Scene scene)
    {
        active = scene.name == Level && !doneThisRun;
        step = -1;
        clock = 0f;
        if (canvas != null) canvas.enabled = false;
    }

    private void Update()
    {
        if (!active) return;

        bool hidden = ChromaEvents.Quiet || Time.timeScale <= 0f || ChromaWardrobe.IsOpen || ChromaPhotoMode.IsOpen;
        // الخزانة والتصوير خطوتان: فتحهما يكفي لإتمامهما ولو والشاشة مخفيّة
        if (step == 5 && ChromaWardrobe.IsOpen) Complete();
        if (step == 6 && ChromaPhotoMode.IsOpen) Complete();
        if (hidden)
        {
            if (canvas != null && canvas.enabled) canvas.enabled = false;
            return;
        }

        clock += Time.unscaledDeltaTime;
        if (step < 0)
        {
            if (clock < StartAfter) return;
            Build();
            Next();
            return;
        }
        if (Time.unscaledTime < waitUntil) return;

        if (!canvas.enabled) canvas.enabled = true;
        if (celebrate) Celebrate();
        if (doneAt < 0f && (Achieved() || (Steps[step].timeout > 0f && clock - stepAt > Steps[step].timeout))) Complete();
        Animate();
    }

    private bool Achieved()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        switch (step)
        {
            case 0:
            {
                Transform p = Player();
                return p != null && Vector3.ProjectOnPlane(p.position - startPos, Vector3.up).magnitude > 2f;
            }
            case 1:
            {
                Transform p = Player();
                return (p != null && p.position.y - startY > 0.5f) ||
                       (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            }
            case 2: return InteractInput.Pressed(Key.E);
            case 3: return emoted && emote == "wave";
            case 4: return emoted && emote == "clap";
            default: return false;
        }
    }

    private void Complete()
    {
        if (doneAt >= 0f || step < 0) return;
        doneAt = clock;
        celebrate = true;   // يُحتفل به حين تظهر اللعبة (الخزانة والتصوير يُتمّان خطوتيهما وهما مفتوحان)
    }

    private void Celebrate()
    {
        celebrate = false;
        ChromaSfx.Play("Drop_Note", 0.5f, ChromaSfx.Semitones(step * 2f));
        bool last = step == Steps.Length - 1;
        ChromaFunEvents.RaiseTutorialStep(step, last);   // احتفالٌ صغير عند القدمين (ChromaJuice)
        if (last) Finish();
    }

    /// <summary>آخر خطوة: لافتة، وهديّة وجوهٍ تفتح أوّل زيّ أقرب — بدايةٌ تكافئ لا تُلقّن.</summary>
    private void Finish()
    {
        Transform p = Player();
        ChromaBank.Add(Reward, p != null ? p.position + Vector3.up * 1.2f : Vector3.zero);
        ChromaFunEvents.Announce(new ChromaFunEvents.Banner
        {
            header = "TUTORIAL",
            title = "YOU'RE READY!",
            line = "+" + Reward + " faces to start you off. Go bring the colour back!",
            letter = "!",
            accent = ChromaStyle.Get().gold,
            sfx = "Skin_Unlock",
        });
    }

    private void Next()
    {
        step++;
        emoted = celebrate = false;
        doneAt = -1f;
        stepAt = clock;
        if (step >= Steps.Length)
        {
            active = false;
            doneThisRun = true;
            canvas.enabled = false;
            return;
        }
        Transform p = Player();
        if (p != null) { startPos = p.position; startY = p.position.y; }
        Refresh();
    }

    private void Animate()
    {
        float t = clock - stepAt;
        float alpha = Mathf.Clamp01(t / FadeIn);
        if (doneAt >= 0f)
        {
            float d = clock - doneAt;
            actionShade.text = action.text = "NICE!";
            if (d > DoneHold) alpha *= 1f - Mathf.Clamp01((d - DoneHold) / FadeOut);
            if (d > DoneHold + FadeOut)
            {
                waitUntil = Time.unscaledTime + Gap;
                Next();
                return;
            }
        }
        group.alpha = alpha * alpha * (3f - 2f * alpha);
        float lift = (1f - Mathf.Clamp01(t / FadeIn)) * -14f;
        block.anchoredPosition = new Vector2(0f, 150f + lift);
    }

    private void Refresh()
    {
        if (canvas == null || step < 0 || step >= Steps.Length) return;
        Step s = Steps[step];
        string key = InputScheme.UsingGamepad ? s.pad : s.keys;
        capText.text = key;
        cap.sizeDelta = new Vector2(Mathf.Max(90f, 34f + key.Length * 24f), 76f);
        actionShade.text = action.text = s.action;
        lineShade.text = line.text = s.line;
        count.text = (step + 1) + " / " + Steps.Length;
    }

    private static Transform Player()
    {
        GameObject p = PlayerLocator.Find("Player");
        if (p == null) return null;
        var c = p.GetComponentInParent<CharacterController>();
        return c != null ? c.transform : p.transform;
    }

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "Tutorial", -1, out group);   // تحت ستائر السين
        Color paper = ChromaWardrobeArt.Paper, ink = ChromaWardrobeArt.Ink;
        Color shade = new Color(ink.r, ink.g, ink.b, 0.8f);

        block = ChromaWardrobeArt.NewRect(canvas.transform, "Block");
        ChromaWardrobeArt.Place(block, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 170f));

        // المفتاح: ورقةٌ بإطار حبر، والزرّ مكتوبٌ عليها
        cap = ChromaWardrobeArt.NewRect(block, "Key");
        ChromaWardrobeArt.Place(cap, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-18f, 0f), new Vector2(120f, 76f));
        ChromaWardrobeArt.Card(cap, 3f);
        capText = ChromaWardrobeArt.NewText(cap, "Key", false, 38f, ink, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Stretch(capText);

        // اسم الحركة بحروف الورق وظلّ الحبر، يمين المفتاح
        actionShade = ChromaWardrobeArt.NewText(block, "ActionShade", true, 64f, shade, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(actionShade, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(22f, -3f), new Vector2(560f, 80f));
        action = ChromaWardrobeArt.NewText(block, "Action", true, 64f, paper, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(action, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(18f, 0f), new Vector2(560f, 80f));

        lineShade = ChromaWardrobeArt.NewText(block, "LineShade", false, 30f, shade, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(lineShade, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(3f, 27f), new Vector2(1000f, 44f));
        line = ChromaWardrobeArt.NewText(block, "Line", false, 30f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(line, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 44f));

        count = ChromaWardrobeArt.NewText(block, "Count", false, 22f, new Color(paper.r, paper.g, paper.b, 0.7f), TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(count, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), new Vector2(300f, 30f));
    }
}

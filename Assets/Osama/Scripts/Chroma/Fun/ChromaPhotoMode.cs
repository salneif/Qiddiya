using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <b>وضع التصوير</b> — P / ضغطة العصا اليمنى: يتوقّف كل شيء، وتدور الكاميرا حول الولد
/// بنعومة، وEnter / Space / Cross تلتقط صورةً مضاعفة الدقّة بإطار ورقٍ واسم المرحلة، تُحفظ في
/// <c>Pictures/Chromafall</c>. P / R3 / Circle تُخرج، ويعود كل شيء كما كان تمامًا.
///
/// كاميرات المراحل يحرّكها <c>CameraFollow</c> و<c>TopDownCameraFollow</c> في LateUpdate كل
/// إطار ولو والزمن موقوف — فتُطفأ ما دام الوضع مفتوحًا ويُعاد تشغيلها ومكان الكاميرا بعده.
/// والواجهات تُطفأ وتُعاد <b>هي وحدها</b> (ما كان مطفأً يبقى مطفأً — لا تنفتح قائمة إيقافٍ بالغلط).
///
/// الحركة كلها بالزمن الحقيقي وبتخميد: الأصابع تحدّد الهدف والكاميرا تلحقه بلين.
/// </summary>
[DisallowMultipleComponent]
public class ChromaPhotoMode : MonoBehaviour
{
    private const int SortingOrder = 3700;
    private const float MinPitch = -10f, MaxPitch = 70f, MinDistance = 1.5f, MaxDistance = 12f;
    private const float StickYaw = 110f, StickPitch = 70f, MouseSens = 0.15f, ZoomSpeed = 5f;
    private const float Smooth = 10f;
    private const int SuperSize = 2;

    private static readonly string[] CameraMovers = { "CameraFollow", "TopDownCameraFollow", "CameraShake", "ChromaJuiceCamera" };

    public static bool IsOpen { get; private set; }

    private static ChromaPhotoMode instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var host = new GameObject("ChromaPhotoMode") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaPhotoMode>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsOpen = false;
    }

    // ما يُعاد عند الخروج
    private Transform cam, target;
    private Vector3 savedPos;
    private Quaternion savedRot;
    private float savedTimeScale = 1f;
    private readonly List<Behaviour> stoppedMovers = new List<Behaviour>();
    private readonly List<Canvas> hidden = new List<Canvas>();
    private readonly List<PlayerInput> muted = new List<PlayerInput>();
    private GameObject previousSelection;

    // المدار: الهدف (ما تطلبه الأصابع) والحالي (ما تلحقه الكاميرا)
    private float yaw, pitch, distance, goalYaw, goalPitch, goalDistance;
    private bool capturing;

    // الواجهة
    private Canvas canvas;
    private CanvasGroup group;
    private GameObject hints, frame;
    private TextMeshProUGUI caption, saved;
    private Image flash;
    private float flashAt = -10f, savedAt = -10f;

    private MonoBehaviour pauseMenu;
    private Canvas pauseCanvas;
    private float nextPauseScan;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (IsOpen) Close(true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOpen) Close(true);
        pauseMenu = null;
        pauseCanvas = null;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            if (TogglePressed() && CanOpen()) Open();
            return;
        }

        // قائمة الإيقاف أخذت الشاشة (Esc) أو غيّر أحدٌ الزمن: نخرج ولا نلمس الزمن
        if (PauseMenuOpen() || Time.timeScale != 0f) { Close(false); return; }
        if (target == null || cam == null) { Close(true); return; }
        if (capturing) { Animate(); return; }

        if (TogglePressed() || BackPressed()) { Close(true); return; }
        if (ShotPressed()) { StartCoroutine(Capture()); return; }
        if (HidePressed()) hints.SetActive(!hints.activeSelf);

        Steer();
        Place();
        Animate();
    }

    private bool CanOpen() =>
        !ChromaEvents.Quiet && !LoadingOverlay.IsBusy && Time.timeScale > 0f && !ChromaWardrobe.IsOpen &&
        !StuckRescue.SuppressPause && !PauseMenuOpen() && Camera.main != null && Player() != null;

    private static Transform Player()
    {
        GameObject p = PlayerLocator.Find("Player");
        if (p == null) return null;
        var controller = p.GetComponentInParent<CharacterController>();
        return controller != null ? controller.transform : p.transform;
    }

    // ---------- فتح وإغلاق ----------

    private void Open()
    {
        cam = Camera.main.transform;
        target = Player();
        savedPos = cam.position;
        savedRot = cam.rotation;

        stoppedMovers.Clear();
        for (Transform t = cam; t != null; t = t.parent)
            foreach (Behaviour b in t.GetComponents<Behaviour>())
                if (b != null && b.enabled && Array.IndexOf(CameraMovers, b.GetType().Name) >= 0)
                {
                    b.enabled = false;
                    stoppedMovers.Add(b);
                }

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Mute();
        HideCanvases();

        // المدار يبدأ من مكان الكاميرا الحالي — لا قفزة لحظة الفتح
        Vector3 offset = cam.position - Focus();
        goalDistance = distance = Mathf.Clamp(offset.magnitude, MinDistance, MaxDistance);
        Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
        goalYaw = yaw = flat.sqrMagnitude > 0.0001f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : cam.eulerAngles.y + 180f;
        goalPitch = pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(0.01f, offset.magnitude), -1f, 1f)) * Mathf.Rad2Deg,
                                        MinPitch, MaxPitch);

        Build();
        hints.SetActive(true);
        frame.SetActive(false);
        canvas.enabled = true;
        IsOpen = true;
        ChromaSfx.Play("Skin_Open", 0.6f);
    }

    private void Close(bool restoreTime)
    {
        StopAllCoroutines();
        capturing = false;

        if (cam != null) cam.SetPositionAndRotation(savedPos, savedRot);
        foreach (Behaviour b in stoppedMovers) if (b != null) b.enabled = true;
        stoppedMovers.Clear();

        foreach (Canvas c in hidden) if (c != null) c.enabled = true;
        hidden.Clear();
        Unmute();

        if (restoreTime && Time.timeScale == 0f) Time.timeScale = savedTimeScale;
        if (canvas != null) canvas.enabled = false;
        IsOpen = false;
        ChromaSfx.Play("Skin_Close", 0.5f);
    }

    private void HideCanvases()
    {
        hidden.Clear();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c == null || !c.enabled || !c.isRootCanvas || c.renderMode == RenderMode.WorldSpace || c == canvas) continue;
            c.enabled = false;
            hidden.Add(c);
        }
    }

    private void Mute()
    {
        muted.Clear();
        var players = PlayerInput.all;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == null || !players[i].inputIsActive) continue;
            players[i].DeactivateInput();
            muted.Add(players[i]);
        }
        EventSystem events = EventSystem.current;
        previousSelection = events != null ? events.currentSelectedGameObject : null;
        if (previousSelection != null) events.SetSelectedGameObject(null);
    }

    private void Unmute()
    {
        foreach (PlayerInput input in muted)
            if (input != null && input.isActiveAndEnabled) input.ActivateInput();
        muted.Clear();
        EventSystem events = EventSystem.current;
        if (events != null && previousSelection != null && previousSelection.activeInHierarchy)
            events.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    // ---------- الكاميرا ----------

    private Vector3 Focus() => target.position + Vector3.up * 1.1f * Mathf.Max(0.3f, target.lossyScale.y);

    private void Steer()
    {
        float dt = Time.unscaledDeltaTime;
        Vector2 look = Vector2.zero;
        float zoom = 0f;

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.rightStick.ReadValue() + pad.leftStick.ReadValue();
            look += new Vector2(stick.x * StickYaw, -stick.y * StickPitch) * dt;
            zoom += (pad.leftTrigger.ReadValue() - pad.rightTrigger.ReadValue()) * ZoomSpeed * dt;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            look += new Vector2(x * StickYaw, -y * StickPitch) * dt;
            zoom += ((kb.xKey.isPressed ? 1f : 0f) - (kb.zKey.isPressed ? 1f : 0f)) * ZoomSpeed * dt;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
                look += new Vector2(mouse.delta.x.ReadValue(), -mouse.delta.y.ReadValue()) * MouseSens;
            zoom -= mouse.scroll.y.ReadValue() * 0.004f;
        }

        goalYaw += look.x;
        goalPitch = Mathf.Clamp(goalPitch + look.y, MinPitch, MaxPitch);
        goalDistance = Mathf.Clamp(goalDistance + zoom, MinDistance, MaxDistance);
    }

    private void Place()
    {
        float k = 1f - Mathf.Exp(-Smooth * Time.unscaledDeltaTime);
        yaw = Mathf.LerpAngle(yaw, goalYaw, k);
        pitch = Mathf.Lerp(pitch, goalPitch, k);
        distance = Mathf.Lerp(distance, goalDistance, k);

        Vector3 focus = Focus();
        Vector3 dir = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;   // من البؤرة نحو الكاميرا
        float reach = distance;

        // جدارٌ بين الولد والكاميرا: تقترب قبله بدل أن تخترقه
        int mask = Physics.DefaultRaycastLayers & ~(1 << target.gameObject.layer);
        if (Physics.SphereCast(focus, 0.25f, dir, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            reach = Mathf.Max(0.6f, hit.distance - 0.1f);

        Vector3 pos = focus + dir * reach;
        cam.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, Vector3.up));
    }

    // ---------- الالتقاط ----------

    private IEnumerator Capture()
    {
        capturing = true;
        hints.SetActive(false);
        saved.gameObject.SetActive(false);
        caption.text = LevelName() + "   ·   " + DateTime.Now.ToString("dd.MM.yyyy");
        frame.SetActive(true);
        flash.color = new Color(1f, 1f, 1f, 0f);

        yield return new WaitForEndOfFrame();

        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Chromafall");
        string path = null;
        try
        {
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, $"Chromafall_{SceneManager.GetActiveScene().name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            ScreenCapture.CaptureScreenshot(path, SuperSize);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ChromaPhotoMode] ما نفع حفظ الصورة: " + e.Message);
            path = null;
        }

        // الصورة تُكتب في نهاية هذا الإطار — إطاران ثم نُعيد الواجهة
        yield return null;
        yield return null;

        frame.SetActive(false);
        flashAt = Time.unscaledTime;
        ChromaSfx.Play("Shutter", 0.8f);
        if (path != null)
        {
            saved.text = "SAVED TO PICTURES / CHROMAFALL";
            saved.gameObject.SetActive(true);
            savedAt = Time.unscaledTime;
            ChromaFunEvents.RaisePhotoTaken(path);
        }
        hints.SetActive(true);
        capturing = false;
    }

    private static string LevelName()
    {
        switch (SceneManager.GetActiveScene().name)
        {
            case "Steam_Final": return "STEAM TOWN";
            case "Hub": return "THE HUB";
            case "AliLvl2_SultanVersion": return "TWILIGHT";
            case "Main_Circus": return "THE CIRCUS";
            default: return "CHROMAFALL";
        }
    }

    private void Animate()
    {
        float f = Mathf.Clamp01(1f - (Time.unscaledTime - flashAt) / 0.35f);
        flash.color = new Color(1f, 1f, 1f, f * f * 0.85f);
        if (saved.gameObject.activeSelf && Time.unscaledTime - savedAt > 2f) saved.gameObject.SetActive(false);
    }

    // ---------- المدخلات ----------

    private static bool TogglePressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.pKey.wasPressedThisFrame) || (pad != null && pad.rightStickButton.wasPressedThisFrame);
    }

    private static bool BackPressed()
    {
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    private static bool ShotPressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
               (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool HidePressed()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && kb.hKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
    }

    private bool PauseMenuOpen()
    {
        if (pauseMenu == null && Time.unscaledTime >= nextPauseScan)
        {
            nextPauseScan = Time.unscaledTime + 1f;
            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (script == null || script.GetType().Name != "PauseMenuManager") continue;
                pauseMenu = script;
                pauseCanvas = script.GetComponent<Canvas>();
                break;
            }
        }
        return pauseMenu != null && pauseCanvas != null && pauseCanvas.enabled && pauseMenu.isActiveAndEnabled;
    }

    // ---------- الواجهة ----------

    private void Build()
    {
        if (canvas != null) return;
        canvas = ChromaWardrobeArt.NewCanvas(transform, "Photo", SortingOrder, out group);
        Color paper = ChromaWardrobeArt.Paper, ink = ChromaWardrobeArt.Ink;

        // إطار الصورة: ورقٌ حول الحوافّ، واسم المرحلة والتاريخ، ووجه الولد في الركن
        frame = ChromaWardrobeArt.NewRect(canvas.transform, "Frame").gameObject;
        ChromaWardrobeArt.Stretch(frame.GetComponent<RectTransform>());
        Edge(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 26), paper);   // أعلى
        Edge(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 74), paper);   // أسفل (أعرض للاسم)
        Edge(new Vector2(0, 0), new Vector2(0, 1), new Vector2(26, 0), paper);   // يسار
        Edge(new Vector2(1, 0), new Vector2(1, 1), new Vector2(26, 0), paper);   // يمين
        caption = ChromaWardrobeArt.NewText(frame.transform, "Caption", true, 40f, ink, TextAlignmentOptions.Left);
        ChromaWardrobeArt.Place(caption, new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(46, 37), new Vector2(1200, 60));
        Sprite face = Resources.Load<Sprite>("Chroma/Face/FaceIcon");
        if (face != null)
            ChromaWardrobeArt.Place(ChromaWardrobeArt.NewImage(frame.transform, "Face", face, Color.white),
                                    new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-40, 37), new Vector2(64, 64));

        // التلميحات أسفل الوسط بحروف الورق وظلّ الحبر
        hints = ChromaWardrobeArt.NewRect(canvas.transform, "Hints").gameObject;
        ChromaWardrobeArt.Stretch(hints.GetComponent<RectTransform>());
        const string help = "MOVE CAMERA: STICKS / WASD / MOUSE     ZOOM: TRIGGERS / Z X / WHEEL     " +
                            "PHOTO: CROSS / ENTER     HIDE: TRIANGLE / H     EXIT: CIRCLE / P";
        var shade = ChromaWardrobeArt.NewText(hints.transform, "HelpShade", false, 26f, new Color(ink.r, ink.g, ink.b, 0.8f), TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(shade, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(3, 27), new Vector2(1800, 40));
        shade.text = help;
        var text = ChromaWardrobeArt.NewText(hints.transform, "Help", false, 26f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(text, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1800, 40));
        text.text = help;
        var title = ChromaWardrobeArt.NewText(hints.transform, "Mode", true, 44f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(title, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(800, 60));
        title.text = "PHOTO MODE";

        saved = ChromaWardrobeArt.NewText(canvas.transform, "Saved", false, 30f, paper, TextAlignmentOptions.Center);
        ChromaWardrobeArt.Place(saved, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(900, 44));
        saved.gameObject.SetActive(false);

        flash = ChromaWardrobeArt.NewImage(canvas.transform, "Flash", null, new Color(1, 1, 1, 0));
        ChromaWardrobeArt.Stretch(flash);
    }

    private void Edge(Vector2 min, Vector2 max, Vector2 size, Color color)
    {
        Image edge = ChromaWardrobeArt.NewImage(frame.transform, "Edge", null, color);
        var r = edge.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.pivot = new Vector2(min.x == max.x ? min.x : 0.5f, min.y == max.y ? min.y : 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = size;
    }
}

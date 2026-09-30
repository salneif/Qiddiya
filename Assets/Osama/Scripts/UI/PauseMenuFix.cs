using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// يُصلح لوحة الإيقاف (<c>Pause-UI</c>) من خارجها: يد التحكّم تفتحها وتتنقّل فيها،
/// والإعدادات ترجع للوحة بدل أن تعلق فوق اللعب.
///
/// اللوحة نفسها سليمة، لكن ربطها فيه ثلاث علل حقيقية:
///
/// ١. <b>القفل التام.</b> زر الإعدادات يطفئ <c>Pause_Holder</c> ويشغّل
///    <c>Settings_Canvas</c>. وسكربت اللوحة يُخفيها بإطفاء <c>Canvas</c> الجذر —
///    و<c>Settings_Canvas</c> كانفس <b>متداخل</b>، والكانفس المتداخل يرسم نفسه ولا
///    يبالي بإطفاء الذي فوقه. فمن دخل الإعدادات وضغط ESC: رجع الزمن لكن الإعدادات
///    باقية على الشاشة، وضغطة ESC الثانية تُوقف الزمن ولا تُظهر شيئًا —
///    <c>Pause_Holder</c> ما زال مطفأً. اللعبة واقفة بلا زر واحد يُضغط.
///
/// ٢. <b>لا شيء ليد التحكّم.</b> السكربت يقرأ ESC وحده، و<c>UIPanel.firstSelected</c>
///    فارغ — فلا زر محدَّد عند الفتح، ومن بلا زر محدَّد لا تتنقّل العصا إطلاقًا.
///
/// ٣. <b>الزمن الواقف يعبر مع السين.</b> <c>ExitToMainMenu</c> يحمّل القائمة و
///    <c>Time.timeScale</c> صفر.
///
/// ولا نلمس سكربت علي ولا بريفابه: كل شيء من هنا. الوصول <b>بالاسم عبر الانعكاس</b>،
/// فلا نُترجَم مع ملف قد يُحذف أو يُعاد تسميته فيكسر بناء الجميع — وإن تغيّر طبعنا
/// سطرًا وعطّلنا أنفسنا.
///
/// ويُركّب نفسه تلقائيًا في كل سين: اللوحة موجودة في أربع سينات لثلاثة أشخاص، فوضعه
/// باليد يعني تعديل مشاهدهم.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuFix : MonoBehaviour
{
    private const string ManagerScriptName = "PauseMenuManager";
    private const string HolderName = "Pause_Holder";
    private const string SettingsName = "Settings_Canvas";
    private const string ResumeName = "Resume_Button";
    private const string ExitName = "Exit_Button";
    private const string AddedTag = " (Chroma)";
    private const string RestartLabel = "Restart", ConfirmLabel = "Sure?";
    private const float ConfirmWindow = 3f;

    private static PauseMenuFix instance;

    /// <summary>
    /// يركّب نفسه بعد تحميل أي سين. بلا بريفاب ولا كائن في المشهد — فلا تعديل على
    /// سينات أحد، ولا نسخة تُنسى في سين وتغيب عن آخر.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("PauseMenuFix") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<PauseMenuFix>();
        DontDestroyOnLoad(host);
    }

    /// <summary>يونيتي يُبقي الساكنات بين تشغيلتين في المحرر، فنصفّرها بأنفسنا.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private MonoBehaviour manager;
    private Canvas canvas;
    private GameObject holder;
    private GameObject settings;
    private MethodInfo toggle;

    private TMP_Text restartText;
    private float confirmUntil = -1f;

    private float nextBindAt;
    private bool wasOpen;
    private bool settingsWasOpen;
    private bool failed;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    /// <summary>
    /// السين الجديد يبدأ بزمن يمشي. الخروج للقائمة من لوحة موقفة للزمن كان يحمّلها
    /// مجمّدة، وسينات اللعب بلا <c>MenuManager</c> يُصلح ذلك عنها.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        manager = null;                    // لوحة السين السابق ذهبت معه
        nextBindAt = 0f;
        wasOpen = settingsWasOpen = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    /// <summary>
    /// بعد <c>Update</c> عمدًا: سكربت علي يقرأ ESC في <c>Update</c>، وترتيب السكربتات
    /// بينهما غير مضمون. في <c>LateUpdate</c> نرى نتيجة ضغطته لا نتسابق معها.
    /// </summary>
    private void LateUpdate()
    {
        if (failed) return;
        if (manager == null && !Bind()) return;

        if (Pad(canvas.enabled)) return;   // فُتحت الآن — نكمل في الإطار القادم

        bool open = canvas.enabled;

        if (open && !wasOpen) Opened();
        else if (!open && wasOpen) Closed();
        else if (open) KeepSelection();

        if (confirmUntil >= 0f && (!open || Time.unscaledTime > confirmUntil)) Unconfirm();

        wasOpen = canvas.enabled;
        settingsWasOpen = settings != null && settings.activeSelf;
    }

    // ---------- يد التحكّم ----------

    /// <summary>
    /// <b>Options/Start</b> يفتح ويُغلق، و<b>دائرة/B</b> يرجع: من الإعدادات إلى اللوحة،
    /// ومن اللوحة إلى اللعب. ترجع true إن فتحت اللوحة في هذي الضغطة.
    /// </summary>
    private bool Pad(bool open)
    {
        var pad = Gamepad.current;
        if (pad == null) return false;

        // خزانة الأزياء مفتوحة: الدائرة تُغلقها هي، وOptions لا يفتح لوحةً فوقها
        if (ChromaWardrobe.IsOpen) return false;
        if (ChromaPhotoMode.IsOpen) return false;   // وضع التصوير: الدائرة تُخرج منه هو

        // تركيبة الإنقاذ تنتهي بـOptions، فلولا هذا لمات اللاعب وفُتحت له القائمة معًا
        if (pad.startButton.wasPressedThisFrame && !StuckRescue.SuppressPause)
        {
            Toggle();
            return !open;
        }

        if (!open || !pad.buttonEast.wasPressedThisFrame) return false;

        if (settings != null && settings.activeSelf) ShowHolder();
        else Toggle();

        return false;
    }

    /// <summary>
    /// يفتح قائمة الإيقاف إن كانت مغلقة واللعب جارٍ — لانفصال يد التحكّم مثلًا. false إن تعذّر.
    /// </summary>
    public static bool RequestPause()
    {
        if (instance == null || instance.failed || instance.manager == null || instance.canvas == null) return false;
        if (instance.canvas.enabled || ChromaWardrobe.IsOpen || ChromaPhotoMode.IsOpen || Time.timeScale <= 0f) return false;
        instance.Toggle();
        return true;
    }

    private void Toggle()
    {
        try { toggle.Invoke(manager, null); }
        catch (System.Exception e) { Fail($"ما نفع نداء {ManagerScriptName}.TogglePause: {e.Message}"); }
    }

    // ---------- الفتح والإغلاق ----------

    private void Opened()
    {
        ShowHolder();
        Select(holder);
    }

    /// <summary>
    /// الإغلاق وفوقه الإعدادات ليس إغلاقًا بل رجوعًا: نُعيد فتح اللوحة على
    /// <c>Pause_Holder</c>. وإلا خرج اللاعب إلى لعبٍ تحجبه شاشة إعدادات لا تُغلق.
    ///
    /// وفي الإغلاق الحقيقي نُرجع اللوحة لحالة صالحة، فتُفتح المرة القادمة على أزرارها
    /// لا على فراغ.
    /// </summary>
    private void Closed()
    {
        ShowHolder();

        if (settingsWasOpen)
        {
            Toggle();                       // كانت الضغطة «رجوع» لا «إغلاق»
            Select(holder);
            return;
        }

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void ShowHolder()
    {
        if (settings != null && settings.activeSelf) settings.SetActive(false);
        if (holder != null && !holder.activeSelf) holder.SetActive(true);
    }

    // ---------- التحديد ----------

    /// <summary>
    /// بلا زر محدَّد لا تعمل العصا ولا الأسهم. ولا نفرضه إن كان اللاعب على زر صالح
    /// أصلًا — وإلا سحبنا التحديد من تحت يده كل إطار.
    /// </summary>
    private void KeepSelection()
    {
        var events = EventSystem.current;
        if (events == null) return;

        GameObject current = events.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        Select(settings != null && settings.activeSelf ? settings : holder);
    }

    private void Select(GameObject panel)
    {
        var events = EventSystem.current;
        if (events == null || panel == null || !panel.activeInHierarchy) return;

        foreach (Button button in panel.GetComponentsInChildren<Button>(false))
        {
            if (button == null || !button.interactable) continue;

            events.SetSelectedGameObject(button.gameObject);
            return;
        }
    }

    // ---------- الريست: Respawn وRestart ----------

    /// <summary>
    /// زرّان تحت Settings لمن علق أو أصابه قلتش: <b>Respawn</b> يرجعه لآخر نقطة حفظ، و<b>Restart</b>
    /// يعيد المرحلة (بضغطتين: الأولى تسأل "Sure?"). نسختان من زرّ Resume وقت التشغيل — بريفاب علي
    /// لا يُمسّ — وExit ينزل تحتهما بالخطوة نفسها التي بين أزراره.
    /// </summary>
    private void AddRescueButtons()
    {
        restartText = null;
        confirmUntil = -1f;
        if (holder == null) return;

        Transform resume = null, exit = null;
        foreach (Transform child in holder.transform)
        {
            string n = child.name.Trim();
            if (n.EndsWith(AddedTag)) return;           // أُضيفا من قبل في هذه اللوحة
            if (n == ResumeName) resume = child;
            else if (n == ExitName) exit = child;
        }
        var resumeRect = resume as RectTransform;
        var exitRect = exit as RectTransform;
        if (resumeRect == null || exitRect == null) return;

        // الخطوة بين الأزرار كما رصّها صاحب اللوحة (Resume → Settings → Exit)
        float step = (resumeRect.anchoredPosition.y - exitRect.anchoredPosition.y) * 0.5f;
        if (step <= 0f) return;

        Vector2 at = exitRect.anchoredPosition;
        Clone(resumeRect, "Respawn", at, OnRespawn);
        int moved = 1;
        if (LevelRestart.Available)
        {
            restartText = Clone(resumeRect, RestartLabel, at + Vector2.down * step, OnRestart);
            moved = 2;
        }
        exitRect.anchoredPosition = at + Vector2.down * step * moved;
    }

    private static TMP_Text Clone(RectTransform template, string label, Vector2 at, UnityAction click)
    {
        GameObject copy = Instantiate(template.gameObject, template.parent);
        copy.name = label + "_Button" + AddedTag;
        var rect = (RectTransform)copy.transform;
        rect.anchoredPosition = new Vector2(template.anchoredPosition.x, at.y);

        var button = copy.GetComponent<Button>();
        if (button != null)
        {
            // صوت النقرة يبقى، وResumeGame يُطفأ — ثم فعلنا نحن
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentMethodName(i) != "PlayOneShot")
                    button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            button.onClick.AddListener(click);
        }

        TMP_Text first = null;
        foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
            if (first == null) first = text;
        }
        return first;
    }

    private void OnRespawn()
    {
        if (canvas != null && canvas.enabled) Toggle();   // تُغلق اللوحة ويرجع الزمن
        if (!StuckRescue.Respawn()) LevelRestart.Reload();   // مشهدٌ بلا نظام موت (الهب): نعيد تحميله
    }

    private void OnRestart()
    {
        if (confirmUntil < 0f || Time.unscaledTime > confirmUntil)
        {
            confirmUntil = Time.unscaledTime + ConfirmWindow;
            if (restartText != null) restartText.text = ConfirmLabel;
            return;
        }
        Unconfirm();
        if (canvas != null && canvas.enabled) Toggle();
        LevelRestart.Go();
    }

    private void Unconfirm()
    {
        confirmUntil = -1f;
        if (restartText != null) restartText.text = RestartLabel;
    }

    // ---------- الربط ----------

    /// <summary>
    /// يلقى اللوحة بالاسم. غيابها ليس خطأً — أكثر السينات بلا لوحة إيقاف — فنصمت
    /// ونحاول في الإطار القادم.
    /// </summary>
    private bool Bind()
    {
        // سين القائمة الرئيسية له MenuManager يدير لوحاته بنفسه، فلا نزاحمه
        if (MenuManager.Instance != null) return false;

        // البحث يمرّ على كل سكربتات السين، ومن السينات ما لا لوحة فيه أصلًا (الانترو):
        // مرّة في الثانية تكفي، وكل إطار كانت ضريبة على لا شيء
        if (Time.unscaledTime < nextBindAt) return false;
        nextBindAt = Time.unscaledTime + 1f;

        foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null || component.GetType().Name != ManagerScriptName) continue;

            manager = component;
            break;
        }

        if (manager == null) return false;

        canvas = manager.GetComponent<Canvas>();
        if (canvas == null)
        {
            Fail($"{ManagerScriptName} على كائن بلا Canvas — ما نعرف متى تُفتح اللوحة.");
            return false;
        }

        toggle = manager.GetType().GetMethod("TogglePause",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, System.Type.EmptyTypes, null);

        if (toggle == null)
        {
            Fail($"ما لقيت {ManagerScriptName}.TogglePause().");
            return false;
        }

        holder = Child(manager.transform, HolderName);
        settings = Child(manager.transform, SettingsName);
        AddRescueButtons();

        wasOpen = canvas.enabled;
        settingsWasOpen = settings != null && settings.activeSelf;
        return true;
    }

    /// <summary>بحث بالاسم يشمل المطفأ — <c>Settings_Canvas</c> مطفأ في البداية.</summary>
    private static GameObject Child(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child.gameObject;

        return null;
    }

    private void Fail(string why)
    {
        failed = true;
        Debug.LogWarning($"[PauseMenuFix] {why} عُطّل الإصلاح، ولوحة الإيقاف تعمل كما كانت.", this);
    }
}

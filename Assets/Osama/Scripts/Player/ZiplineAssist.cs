using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// زيبلاين ستيم دقيقٌ وله نهاية — من الخارج، بلا سطرٍ في سكربت علي.
///
/// ١. <b>اللاعب يتشوّه ويعلق في غير مكانه.</b> السكربت يجعل اللاعب ابنًا للمقبض كما هو،
/// والمقبض مكبّرٌ بغير تساوٍ (٢٫٤ × ٤ × ٤) واللاعب مدوّرٌ لاتجاه الحبل — فيُمطّ ويميل.
/// ومن دخل البداية وهو يقفز عُلّق حيث كان: فوق الحبل. <b>الإصلاح</b>: نعيده لأبيه
/// الأصلي، ويتبع المقبض من تحته بالضبط (أسفل شكل المقبض الفعلي، يداه فوق رأسه).
///
/// ٢. <b>لا نهاية للزيبلاين.</b> المقبض يقف في آخر الحبل واللاعب معلّقٌ فيه بحركةٍ مقفلة
/// وبوضعية التعلّق للأبد. <b>الإصلاح</b>: عند الوصول يُترك فيسقط طبيعيًّا، ويعود المقبض
/// لبدايته بعد لحظة فيُركب ثانيةً (بعد موتٍ مثلًا).
///
/// ٣. <b>البعث وهو معلّق</b>: إن نقله شيءٌ غيرنا (الموت) نتركه ولا نسحبه للحبل.
///
/// كل الوصول <b>بالاسم عبر الانعكاس</b>: إن غُيّر اسمٌ سكت هذا السكربت وبقي الزيبلاين كما كان.
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DefaultExecutionOrder(650)]   // بعد سكربت الزيبلاين الذي يحرّك المقبض هذا الإطار
[DisallowMultipleComponent]
public class ZiplineAssist : MonoBehaviour
{
    private const string RideScript = "A_ZipLineSystem";
    private const string StartScript = "A_ZipLineBegin";
    private const string MovementScript = "PlayerController";

    /// <summary>بين قمّة رأسه وأسفل المقبض — مسافة اليدين الممدودتين.</summary>
    private const float HandsAboveHead = 0.15f;
    private const float ArriveDistance = 0.05f;
    private const float GripReturnDelay = 1f;
    /// <summary>ابتعادٌ عن حيث وضعناه أكبر من هذا = نقله غيرنا (بعث).</summary>
    private const float MovedByOthers = 1f;

    private static readonly int ZipTrigger = Animator.StringToHash("OnZiplineBegin");
    private static readonly int FallState = Animator.StringToHash("A_InAir_FallLarge_Masc");
    private static readonly int MoveState = Animator.StringToHash("Movement");

    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly object No = false;
    private static readonly object Yes = true;

    private static ZiplineAssist instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("ZiplineAssist") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ZiplineAssist>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private CharacterController controller;
    private Transform body, homeParent;
    private Component ride, movement;
    private FieldInfo riding, gripField, endField, canMove;
    private Animator animator;
    private bool bound, giveUp;
    private float nextBindAt;

    private readonly Dictionary<GameObject, Vector3> gripStarts = new Dictionary<GameObject, Vector3>();
    private GameObject returningGrip;
    private float returnAt;

    private bool following;
    private GameObject grip;
    private Vector3 offset, placedAt;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bound = giveUp = following = false;
        nextBindAt = 0f;
        controller = null;
        ride = movement = null;
        grip = returningGrip = null;
        gripStarts.Clear();
    }

    private void Update()
    {
        if (!bound && !Bind()) return;
        if (controller == null || body == null || ride == null) { bound = false; following = false; return; }

        ReturnGrip();

        bool ridingNow = (bool)riding.GetValue(ride);
        if (!ridingNow)
        {
            following = false;
            if (body.parent == null || !IsGrip(body.parent)) homeParent = body.parent;
            return;
        }

        if (!following) { Attach(); return; }

        if (grip == null) { Release(); return; }

        // نقله غيرنا منذ الإطار الماضي (بعث/نقل) — لا نسحبه للحبل
        if ((body.position - placedAt).sqrMagnitude > MovedByOthers * MovedByOthers) { Release(); return; }

        Place();

        Vector3 end = (Vector3)endField.GetValue(ride);
        if ((grip.transform.position - end).sqrMagnitude <= ArriveDistance * ArriveDistance) Release();
    }

    /// <summary>بداية الركوب: نفكّه من المقبض ونعلّقه تحته بالضبط.</summary>
    private void Attach()
    {
        grip = gripField.GetValue(ride) as GameObject;
        if (grip == null) return;

        if (body.parent != null && body.parent != homeParent) body.SetParent(homeParent, true);

        // أسفل شكل المقبض الفعلي، لا محوره — المحور قد يكون في وسط مجسّمٍ طويل
        Vector3 hang = grip.transform.position;
        Renderer shape = grip.GetComponentInChildren<Renderer>();
        if (shape != null)
        {
            Bounds b = shape.bounds;
            hang = new Vector3(b.center.x, b.min.y, b.center.z);
        }

        float top = (controller.center.y + controller.height * 0.5f) * body.lossyScale.y;
        Vector3 target = hang - Vector3.up * (top + HandsAboveHead);
        offset = target - grip.transform.position;

        following = true;
        Place();
    }

    private void Place()
    {
        body.position = grip.transform.position + offset;
        placedAt = body.position;
        Physics.SyncTransforms();   // CharacterController يبدأ حركته التالية من هنا لا من موضعه القديم
    }

    /// <summary>نهاية الحبل: يُترك فيسقط، وتعود حركته وأنميشنه، ويرجع المقبض بعد لحظة.</summary>
    private void Release()
    {
        following = false;
        riding.SetValue(ride, No);
        if (canMove != null && movement != null) canMove.SetValue(movement, Yes);

        if (body.parent != homeParent) body.SetParent(homeParent, true);
        Physics.SyncTransforms();

        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.ResetTrigger(ZipTrigger);
            int to = animator.HasState(0, FallState) ? FallState : MoveState;
            if (animator.HasState(0, to)) animator.CrossFadeInFixedTime(to, 0.2f, 0);
        }

        if (grip != null && gripStarts.ContainsKey(grip))
        {
            returningGrip = grip;
            returnAt = Time.time + GripReturnDelay;
        }
        grip = null;
    }

    private void ReturnGrip()
    {
        if (returningGrip == null || Time.time < returnAt || following) return;
        if (gripStarts.TryGetValue(returningGrip, out Vector3 start)) returningGrip.transform.position = start;
        returningGrip = null;
    }

    private bool IsGrip(Transform t)
    {
        foreach (GameObject g in gripStarts.Keys)
            if (g != null && (t == g.transform || t.IsChildOf(g.transform))) return true;
        return false;
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

        ride = FindScript(controller, RideScript);
        movement = FindScript(controller, MovementScript);
        if (ride == null || movement == null) { giveUp = true; return false; }

        riding = Field(ride, "WeAreInZipLine", typeof(bool));
        gripField = Field(ride, "currentGrip", typeof(GameObject));
        endField = Field(ride, "currentEndPoint", typeof(Vector3));
        canMove = Field(movement, "CanMove", typeof(bool));
        if (riding == null || gripField == null || endField == null || canMove == null)
        {
            Debug.LogWarning("[ZiplineAssist] تغيّرت أسماء سكربت الزيبلاين — عُطّل المساعد، والزيبلاين يعمل كما كان.", this);
            giveUp = true;
            return false;
        }

        FieldInfo animatorField = Field(ride, "animator", typeof(Animator));
        animator = animatorField != null ? animatorField.GetValue(ride) as Animator : null;
        if (animator == null) animator = controller.GetComponentInChildren<Animator>();

        // مواضع المقابض في بدايتها — لتعود إليها بعد كل ركوب
        gripStarts.Clear();
        foreach (MonoBehaviour c in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (c == null || c.GetType().Name != StartScript) continue;
            FieldInfo g = c.GetType().GetField("grip", Any);
            if (g != null && g.GetValue(c) is GameObject go && go != null && !gripStarts.ContainsKey(go))
                gripStarts.Add(go, go.transform.position);
        }

        homeParent = body.parent;
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

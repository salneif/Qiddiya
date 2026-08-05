using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// بوابة انتقال بين السينات — الباب الذي يدخل منه اللاعب للمرحلة، ومخرج المرحلة
/// الذي يرجّعه للهب.
///
/// ثلاث طرق للتشغيل، اختر ما يناسب:
///  1. <b>تريغر</b>: كولايدر (Is Trigger) + <c>Activation Key = None</c> → ينتقل بمجرد الدخول.
///  2. <b>زر</b>: نفس الشيء مع زر (E مثلًا) → يقف عند الباب ويضغط.
///  3. <b>حدث</b>: بلا كولايدر أصلًا، ونادِ <see cref="Go"/> من أي حدث.
///     هذي حالة السيرك: <c>FlagItem.On Picked Up</c> → <c>LevelPortal.Go</c>،
///     فتنتهي المرحلة لحظة أخذ العلم (استخدم <c>Delay</c> ليُسمع صوت الالتقاط).
///
/// يسجّل السين الحالي في <see cref="GameProgress"/> قبل المغادرة، فيعرف
/// <see cref="PlayerSpawnRouter"/> في الوجهة من أين جاء اللاعب.
///
/// ⚠️ كل سين وجهة لازم يكون مضافًا في <b>File → Build Profiles → Scene List</b>،
/// وإلا ما صار شيء أبدًا. السكربت يطبع خطأ صريحًا في هذي الحالة بدل الصمت.
/// </summary>
public class LevelPortal : MonoBehaviour
{
    [Header("الوجهة")]
    [Tooltip("اسم السين المطلوب تحميله (لازم يكون في قائمة مشاهد البناء)")]
    [SerializeField] private string sceneName;
    [Tooltip("تأخير قبل بدء الانتقال (ثواني) — يعطي وقتًا لصوت أو مؤثر")]
    [SerializeField] private float delay = 0f;

    [Header("الشروط")]
    [Tooltip("لا يفتح إلا إذا كان هذا العلم مزروعًا في الهب (بدون = بلا شرط). " +
             "بوابة التوايلايت مثلًا تشترط زرع علم ستيم.")]
    [SerializeField] private FlagId requirePlantedFlag = FlagId.None;
    [Tooltip("لا ينتقل إلا واللاعب حامل هذا العلم (بدون = بلا شرط). " +
             "مخرج المرحلة يشترط أنك أخذت علمها.")]
    [SerializeField] private FlagId requireCarriedFlag = FlagId.None;

    [Header("التشغيل بالتريغر")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("زر التفعيل وأنت داخل المنطقة (None = ينتقل تلقائيًا بمجرد الدخول)")]
    [SerializeField] private Key activationKey = Key.None;

    [Header("الانتقال")]
    [Tooltip("مموّه الشاشة — يُلتقط تلقائيًا من السين إذا تُرك فارغًا. " +
             "بدونه يُحمَّل السين بلا تعتيم.")]
    [SerializeField] private ScreenFader fader;

    [Header("أحداث")]
    [Tooltip("لحظة بدء الانتقال (أوقف حركة اللاعب، شغّل صوتًا...)")]
    public UnityEvent onTransitionStarted;
    [Tooltip("عند محاولة العبور والشرط غير متحقق (صوت باب مقفول، تلميح...)")]
    public UnityEvent onBlocked;

    private bool playerInside;
    private bool leaving;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Start()
    {
        if (fader == null) fader = FindFirstObjectByType<ScreenFader>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = true;

        if (activationKey == Key.None) TryGo();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = false;
    }

    private void Update()
    {
        if (!playerInside || leaving || activationKey == Key.None) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[activationKey].wasPressedThisFrame) TryGo();
    }

    /// <summary>يحاول الانتقال ويحترم الشروط — هذا ما يناديه التريغر والزر.</summary>
    public void TryGo()
    {
        if (leaving) return;

        var progress = GameProgress.Instance;

        if (requirePlantedFlag != FlagId.None && !progress.IsPlanted(requirePlantedFlag))
        {
            onBlocked?.Invoke();
            return;
        }

        if (requireCarriedFlag != FlagId.None && !progress.IsCarrying(requireCarriedFlag))
        {
            onBlocked?.Invoke();
            return;
        }

        Go();
    }

    /// <summary>ينتقل فورًا متجاهلًا الشروط — اربطه بالأحداث (نهاية مرحلة السيرك).</summary>
    public void Go()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(GoRoutine());
    }

    private IEnumerator GoRoutine()
    {
        onTransitionStarted?.Invoke();

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LevelPortal] اسم السين فارغ.", this);
            leaving = false;
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LevelPortal] السين \"{sceneName}\" غير موجود في قائمة مشاهد " +
                           $"البناء — أضِفه من File → Build Profiles → Scene List.", this);
            leaving = false;
            yield break;
        }

        // من أين جاء اللاعب — يقرأها PlayerSpawnRouter في الوجهة
        GameProgress.Instance.SetLastScene(SceneManager.GetActiveScene().name);

        if (fader != null) fader.FadeOutAndLoad(sceneName);
        else SceneManager.LoadSceneAsync(sceneName);
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

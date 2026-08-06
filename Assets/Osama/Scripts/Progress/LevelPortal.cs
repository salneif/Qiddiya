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
    [Tooltip("يمسح كل التقدّم قبل الانتقال — فعّله في بوابة العودة للقائمة الرئيسية. " +
             "بدونه تبدأ اللعبة التالية والأعلام الثلاثة مزروعة أصلًا، " +
             "لأن GameProgress يعيش بين السينات ولا يموت إلا بإغلاق اللعبة.")]
    [SerializeField] private bool resetProgressOnLeave = false;

    [Header("عند الاقتراب")]
    [Tooltip("مسافة الإحساس بالبوابة (متر). صفر = عطّل الاقتراب كله.")]
    [SerializeField] private float approachDistance = 6f;
    [Tooltip("مصدر الصوت — يُلتقط تلقائيًا من نفس الكائن إذا تُرك فارغًا")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت مرة واحدة عند الاقتراب والبوابة مفتوحة (رنّة دعوة)")]
    [SerializeField] private AudioClip approachSound;
    [Tooltip("صوت مرة واحدة عند الاقتراب والبوابة مقفولة (قعقعة قفل)")]
    [SerializeField] private AudioClip lockedApproachSound;
    [Tooltip("صوت محاولة العبور وهي مقفولة (ضغط الزر بلا فايدة)")]
    [SerializeField] private AudioClip blockedSound;
    [Tooltip("همهمة مستمرة تعلو كلما اقتربت وتخفت كلما ابتعدت")]
    [SerializeField] private AudioClip ambientLoop;
    [Tooltip("أعلى مستوى للهمهمة (عند الوقوف على البوابة)")]
    [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.6f;

    [Header("أحداث")]
    [Tooltip("لحظة بدء الانتقال (أوقف حركة اللاعب، شغّل صوتًا...)")]
    public UnityEvent onTransitionStarted;
    [Tooltip("عند محاولة العبور والشرط غير متحقق (صوت باب مقفول، تلميح...)")]
    public UnityEvent onBlocked;
    [Tooltip("عند دخول مدى الاقتراب — أظهر تلميح \"اضغط E\"")]
    public UnityEvent onPlayerApproached;
    [Tooltip("عند الابتعاد — أخفِ التلميح")]
    public UnityEvent onPlayerLeft;

    /// <summary>هل شرط الفتح متحقق؟ (لا يشمل شرط حمل العلم — ذاك شرط خروج لا فتح)</summary>
    public bool IsUnlocked => requirePlantedFlag == FlagId.None ||
                              GameProgress.Instance.IsPlanted(requirePlantedFlag);

    private bool playerInside;
    private bool leaving;
    private Transform player;
    private bool playerNear;
    private bool loopPlaying;
    private AudioSource ambientSource;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Start()
    {
        if (fader == null) fader = FindFirstObjectByType<ScreenFader>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
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
        UpdateApproach();

        if (!playerInside || leaving || activationKey == Key.None) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current[activationKey].wasPressedThisFrame) TryGo();
    }

    /// <summary>
    /// الإحساس بالبوابة بالمسافة لا بكولايدر ثانٍ — فيعمل حتى على بوابة بلا كولايدر
    /// أصلًا، ولا يتعارض مع كولايدر البيس المجاور.
    /// </summary>
    private void UpdateApproach()
    {
        if (approachDistance <= 0f) return;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        float dist = Vector3.Distance(player.position, transform.position);
        bool near = dist <= approachDistance;

        if (near && !playerNear)
        {
            playerNear = true;
            Play(IsUnlocked ? approachSound : lockedApproachSound);
            onPlayerApproached?.Invoke();
        }
        else if (!near && playerNear)
        {
            playerNear = false;
            onPlayerLeft?.Invoke();
        }

        UpdateAmbient(near, dist);
    }

    /// <summary>
    /// الهمهمة تخفت بالمسافة <b>بالكود</b> لا بالصوت ثلاثي الأبعاد — كاميرا اللعبة
    /// بعيدة عن اللاعب، و Spatial Blend = 1 معها يعني صوتًا لا يُسمع (خطأ ١١ في الريدمي).
    /// خلّ الـ AudioSource ثنائي الأبعاد ودع هذي الدالة تتولّى المسافة.
    /// </summary>
    private void UpdateAmbient(bool near, float dist)
    {
        if (ambientLoop == null) return;

        // مصدر مستقل للهمهمة: لو شاركت المصدر مع الأصوات اللحظية لضرب صوت
        // الاقتراب في حجم الهمهمة — وهو صفر تقريبًا عند حافة المدى، فما يُسمع أصلًا.
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f;
            ambientSource.clip = ambientLoop;
        }

        if (near && !loopPlaying)
        {
            ambientSource.Play();
            loopPlaying = true;
        }
        else if (!near && loopPlaying)
        {
            ambientSource.Stop();
            loopPlaying = false;
        }

        if (loopPlaying)
            ambientSource.volume = ambientVolume * (1f - Mathf.Clamp01(dist / approachDistance));
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    /// <summary>يحاول الانتقال ويحترم الشروط — هذا ما يناديه التريغر والزر.</summary>
    public void TryGo()
    {
        if (leaving) return;

        if (!IsUnlocked)
        {
            Play(blockedSound);
            onBlocked?.Invoke();
            return;
        }

        // العلم المزروع يُغني عن حمله: لو رجع اللاعب لمرحلة أنهاها، علمها مخفي
        // (مزروع في الهب) فلا يقدر يحمله — واشتراط الحمل هنا كان يحبسه في المرحلة
        // بلا مخرج. أنهاها مرة، فله أن يخرج متى شاء.
        var progress = GameProgress.Instance;
        if (requireCarriedFlag != FlagId.None &&
            !progress.IsCarrying(requireCarriedFlag) &&
            !progress.IsPlanted(requireCarriedFlag))
        {
            Play(blockedSound);
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

        if (resetProgressOnLeave)
            GameProgress.Instance.ResetProgress();   // عودة للقائمة = لعبة جديدة
        else
            // من أين جاء اللاعب — يقرأها PlayerSpawnRouter في الوجهة
            GameProgress.Instance.SetLastScene(SceneManager.GetActiveScene().name);

        if (fader != null) fader.FadeOutAndLoad(sceneName);
        else SceneManager.LoadSceneAsync(sceneName);
    }

    private void OnDrawGizmosSelected()
    {
        // الأزرق = منطقة التفعيل (الكولايدر)
        var c = GetComponent<Collider>();
        if (c != null)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }

        // البنفسجي = مدى الصوت والتلميح — دائمًا أوسع من منطقة التفعيل
        if (approachDistance > 0f)
        {
            Gizmos.color = new Color(0.7f, 0.5f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, approachDistance);
        }
    }
}

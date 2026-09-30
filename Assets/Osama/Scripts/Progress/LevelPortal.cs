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
    [Tooltip("يتجاهل التريغر أول هذي الثواني بعد تحميل السين. نقطة ظهور اللاعب عائدًا " +
             "من المرحلة تكون ملاصقة للباب، فبدونها يظهر داخل البوابة فترجّعه من حيث " +
             "أتى في نفس اللحظة — حلقة لا تنتهي. صفر = السلوك القديم.")]
    [SerializeField] private float ignoreTriggerAfterLoad = 1f;

    [Header("الانتقال")]
    [Tooltip("شاشة التحميل: صورة الوجهة وبار يركض عليه علي. إن أُطفئت رجع الانتقال " +
             "للتعتيم الأسود وحده.")]
    [SerializeField] private bool useLoadingScreen = true;
    [Tooltip("مموّه الشاشة — يُلتقط تلقائيًا من السين إذا تُرك فارغًا. " +
             "بدونه يُحمَّل السين بلا تعتيم. لا يُستعمل مع شاشة التحميل.")]
    [SerializeField] private ScreenFader fader;
    [Tooltip("يجمّد اللاعب لحظة بدء الانتقال، فلا يكمل مشيه أثناء التعتيم ويتعدّى الباب " +
             "قبل أن ينتقل. يستخدم قائمة Disable On Death في PlayerKillable، فلا يحتاج ضبطًا.")]
    [SerializeField] private bool freezePlayerOnTransition = true;
    [Tooltip("سكربتات إضافية تتعطّل لحظة الانتقال (متابعة الكاميرا مثلًا) — اختياري")]
    [SerializeField] private MonoBehaviour[] disableOnTransition;
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

    [Header("اهتزاز اليد")]
    [Tooltip("دمدمةٌ تشتدّ كلّما قرب اللاعب من البوابة المفتوحة. صفر = بلا اهتزاز")]
    [SerializeField] private float rumbleStrength = 0.35f;

    [Header("نبض الوهج")]
    [Tooltip("يجعل ضوء البوابة ومجسّماتها المضيئة تنبض بعد أن تُفتح — البوابة الساكنة " +
             "تُقرأ زينةً في المشهد لا بابًا يُدخل منه")]
    [SerializeField] private bool pulseGlow;
    [Tooltip("نصف قطر ما يُلتقط حول البوابة من أضواء ومجسّمات")]
    [SerializeField] private float pulseRadius = 3f;
    [Tooltip("نبضة كاملة في كم ثانية")]
    [SerializeField] private float pulsePeriod = 1.8f;
    [Tooltip("حدّ الخفوت في أضعف لحظة")]
    [Range(0.2f, 1f)] [SerializeField] private float pulseDim = 0.6f;
    [Tooltip("حدّ السطوع في أقواها")]
    [Range(1f, 2.5f)] [SerializeField] private float pulseBright = 1.3f;
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
    public bool IsUnlocked => (requirePlantedFlag == FlagId.None ||
                               GameProgress.Instance.IsPlanted(requirePlantedFlag)) && !DestinationDone;

    /// <summary>
    /// المرحلة التي تأخذ إليها أُنهيت: علمها محمول أو مزروع. البوابة تُغلق فلا يعود اللاعب إليها
    /// (أسامة: "بعد ما أخلص من السيرك أقدر أرجع"). بوابات العودة للهب لا تتأثّر — الهب بلا علم.
    /// </summary>
    private bool DestinationDone
    {
        get
        {
            FlagId flag = FlagOf(sceneName);
            if (flag == FlagId.None) return false;
            GameProgress progress = GameProgress.Instance;
            return progress.IsPlanted(flag) || progress.IsCarrying(flag);
        }
    }

    /// <summary>علم المرحلة التي هذا اسمها، وNone للهب وما سواه.</summary>
    internal static FlagId FlagOf(string scene) =>
        scene == "Steam_Final" ? FlagId.Steam
        : scene == "AliLvl2_SultanVersion" ? FlagId.Twilight
        : scene == "Main_Circus" ? FlagId.Circus : FlagId.None;

    private bool playerInside;
    private bool armed = true;
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

        // ظهر داخل البوابة لا مشى إليها — لا ترجّعه، وانتظر حتى يخرج ويعود بنفسه
        if (Time.timeSinceLevelLoad < ignoreTriggerAfterLoad)
        {
            armed = false;
            return;
        }

        if (activationKey == Key.None) TryGo();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerInside = false;
        armed = true;   // خرج ثم عاد = نيّة حقيقية للعبور
    }

    private void Update()
    {
        UpdateApproach();

        if (!playerInside || !armed || leaving || activationKey == Key.None) return;

        if (InteractInput.Pressed(activationKey)) TryGo();
    }

    /// <summary>
    /// الإحساس بالبوابة بالمسافة لا بكولايدر ثانٍ — فيعمل حتى على بوابة بلا كولايدر
    /// أصلًا، ولا يتعارض مع كولايدر البيس المجاور.
    /// </summary>
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Light[] glowLights;
    private float[] glowIntensities;
    private Renderer[] glowRenderers;
    private Color[] glowColors;
    private MaterialPropertyBlock glowBlock;

    private void UpdateApproach()
    {
        if (approachDistance <= 0f) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
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
        UpdateRumble(near, dist);
        UpdatePulse();
    }

    /// <summary>
    /// دمدمةٌ تشتدّ مع القرب — بوابةٌ تُحسّ قبل أن تُدخل.
    ///
    /// <c>Hold</c> لا <c>Play</c>: الأولى تُكتب كل إطار وتتبع المسافة لحظةً بلحظة،
    /// والثانية ضربةٌ لها مدّة تنتهي فلا تصلح لشيءٍ يشتدّ ويخفّ باستمرار.
    ///
    /// وللمغلقة نصف ما للمفتوحة: تُحسّ أن هناك شيئًا، ولا تُدعى إلى ما لا يُفتح.
    /// </summary>
    private void UpdateRumble(bool near, float dist)
    {
        // أسامة: المغلقة لا تهزّ أبدًا (كانت تهزّ واللغز لم يُحلّ بعد)، والمفتوحة من قربٍ فقط وبخفّة
        const float RumbleRadius = 3.5f;
        if (!near || leaving || rumbleStrength <= 0f || !IsUnlocked || dist > RumbleRadius) return;

        float closeness = 1f - Mathf.Clamp01(dist / RumbleRadius);
        closeness *= closeness;                       // تشتدّ في المتر الأخير لا في الطريق كله

        float strength = rumbleStrength * 0.5f * closeness;
        PadRumble.Hold(strength, strength * 0.35f);
    }

    /// <summary>
    /// نبضُ الوهج.
    ///
    /// يُلتقط ما حول البوابة من أضواء ومجسّمات <b>مرّة واحدة عند أول نبضة</b>، فلا
    /// يحتاج أحدٌ أن يربط شيئًا بيده — والبوابة موضوعةٌ وسط وهجها أصلًا.
    /// والالتقاط بنصف قطر صغير عمدًا: ما بعد عن البوابة ليس منها.
    /// </summary>
    private void UpdatePulse()
    {
        if (!pulseGlow || !IsUnlocked) return;

        if (glowLights == null) CollectGlow();
        if (glowLights.Length == 0 && glowRenderers.Length == 0) return;

        float wave = Mathf.Sin(Time.time / Mathf.Max(0.05f, pulsePeriod) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float strength = Mathf.Lerp(pulseDim, pulseBright, wave);

        for (int i = 0; i < glowLights.Length; i++)
        {
            if (glowLights[i] == null) continue;
            glowLights[i].intensity = glowIntensities[i] * strength;
        }

        if (glowRenderers.Length == 0) return;

        if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
        for (int i = 0; i < glowRenderers.Length; i++)
        {
            Renderer renderer = glowRenderers[i];
            if (renderer == null) continue;

            Color color = glowColors[i] * strength;
            color.a = glowColors[i].a;

            renderer.GetPropertyBlock(glowBlock);
            glowBlock.SetColor(BaseColorId, color);
            glowBlock.SetColor(ColorId, color);
            glowBlock.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(glowBlock);
        }
    }

    /// <summary>
    /// يلتقط الوهج حول البوابة. الشدّة واللون الأصليان يُقرآن <b>هنا مرّة</b>: لو
    /// قُرئا كل إطار لقرآ ما كتبناه نحن فتضاعف النبض حتى يبيضّ.
    /// </summary>
    private void CollectGlow()
    {
        var lights = new System.Collections.Generic.List<Light>();
        var renderers = new System.Collections.Generic.List<Renderer>();

        foreach (Collider hit in Physics.OverlapSphere(transform.position, pulseRadius,
                                                       ~0, QueryTriggerInteraction.Collide))
        {
            if (hit == null) continue;
            foreach (Light light in hit.GetComponentsInChildren<Light>(false))
                if (!lights.Contains(light)) lights.Add(light);
        }

        // الأضواء بلا كولايدر في الغالب، فنمرّ على أضواء المشهد بالمسافة كذلك
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light == null || light.type == LightType.Directional) continue;
            if (lights.Contains(light)) continue;
            if (Vector3.Distance(light.transform.position, transform.position) <= pulseRadius)
                lights.Add(light);
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(false))
            renderers.Add(renderer);

        glowLights = lights.ToArray();
        glowRenderers = renderers.ToArray();

        glowIntensities = new float[glowLights.Length];
        for (int i = 0; i < glowLights.Length; i++) glowIntensities[i] = glowLights[i].intensity;

        glowColors = new Color[glowRenderers.Length];
        for (int i = 0; i < glowRenderers.Length; i++)
        {
            Material material = glowRenderers[i].sharedMaterial;
            glowColors[i] = material != null && material.HasProperty(BaseColorId)
                ? material.GetColor(BaseColorId)
                : Color.white;
        }

        if (glowLights.Length == 0 && glowRenderers.Length == 0)
            Debug.LogWarning($"[LevelPortal] «{name}»: ما لقيت ضوءًا ولا مجسّمًا خلال " +
                             $"{pulseRadius} م — ارفع Pulse Radius أو أطفئ Pulse Glow.", this);
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

        // أولًا التجميد ثم التأخير: بدونه يكمل اللاعب مشيه طوال التأخير والتعتيم،
        // فيطلع من الباب ويقف بعيدًا عنه لحظة تحميل السين — يبدو كأنه ما انتقل
        FreezePlayer(true);

        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LevelPortal] اسم السين فارغ.", this);
            leaving = false;
            FreezePlayer(false);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LevelPortal] السين \"{sceneName}\" غير موجود في قائمة مشاهد " +
                           $"البناء — أضِفه من File → Build Profiles → Scene List.", this);
            leaving = false;
            FreezePlayer(false);
            yield break;
        }

        if (resetProgressOnLeave)
            GameProgress.Instance.ResetProgress();   // عودة للقائمة = لعبة جديدة
        else
            // من أين جاء اللاعب — يقرأها PlayerSpawnRouter في الوجهة
            GameProgress.Instance.SetLastScene(SceneManager.GetActiveScene().name);

        // الخروج مؤكَّد الآن (بعد كل الشروط): الميداليات والأوسمة تسمعه
        ChromaFunEvents.RaiseLevelLeft(SceneManager.GetActiveScene().name, sceneName);

        // الشاشة تتولّى التعتيم بنفسها، فلا نجمع تعتيمين فوق بعض
        if (useLoadingScreen && LoadingOverlay.Go(sceneName)) yield break;

        if (fader != null) fader.FadeOutAndLoad(sceneName);
        else SceneManager.LoadSceneAsync(sceneName);
    }

    /// <summary>
    /// يجمّد حركة اللاعب حول الانتقال ويرجّعها إذا فشل الانتقال، فلا يعلق اللاعب
    /// بلا حركة في سين لم يتغيّر.
    /// </summary>
    private void FreezePlayer(bool freeze)
    {
        if (disableOnTransition != null)
            foreach (var b in disableOnTransition)
                if (b != null) b.enabled = !freeze;

        if (!freezePlayerOnTransition) return;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;
            player = go.transform;
        }

        var killable = player.GetComponentInParent<PlayerKillable>();
        if (killable == null) return;

        if (freeze) killable.FreezeControl();
        else killable.UnfreezeControl();
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// نهاية اللعبة والكريديت — تُنادى من <c>FlagBase.On All Flags Planted</c> في
/// القاعدة الثالثة (علم السيرك).
///
/// الترتيب:
///  1. تجميد اللاعب، وتشغيل الأغنية.
///  2. الكاميرا تبعد بشويش، وفقرة لكل واحد من الفريق: اسمه ودوره، ومعه مودلاته
///     إن حطّيتها — تظهر أمام الكاميرا وتدور ببطء.
///  3. بعد فقرات العرض ترجع الكاميرا بشويش إلى مكانها.
///  4. تعتيم، ثم رجوع للقائمة الرئيسية والتقدّم مصفّر.
///
/// ⚠️ سكربت تتبّع الكاميرا لازم يكون في <see cref="disableOnCredits"/>، وإلا شدّ
/// الكاميرا لللاعب كل إطار وما بعدت خطوة.
///
/// كل شيء ثلاثي الأبعاد: الأسماء والأدوار على <b>منصّة</b> في العالم لا على واجهة
/// مسطّحة فوق الشاشة، والمودلات تقف معها على نفس المنصّة. المنصّة معلّقة بالكاميرا
/// فتبقى في الكادر وهي تبعد، بينما يتراجع الهب خلفها.
///
/// الأسماء لاتينية: خط يونيتي المدمج بلا حروف عربية، ويونيتي لا يشكّل العربية
/// (الحروف تنفصل وتنعكس). تبي عربي؟ اعمل صورة PNG لكل اسم وحطّها في
/// <see cref="Section.nameImage"/> — الصورة تسبق النص.
///
/// هذا السكربت بديل موسّع لـ<see cref="GameEndingSequence"/>. اربط واحدًا منهما
/// لا الاثنين.
/// </summary>
[DisallowMultipleComponent]
public class GameCredits : MonoBehaviour
{
    /// <summary>فقرة واحدة: اسم، دور، ومودلات تُعرض معه.</summary>
    [System.Serializable]
    public class Section
    {
        [Tooltip("الاسم الكبير — لاتيني، فالخط المدمج بلا عربية")]
        public string title = "NAME";

        [Tooltip("الدور تحت الاسم")]
        public string role = "";

        [Tooltip("صورة الاسم — تسبق النص إن حطّيتها (اكتب العربي في فوتوشوب وصدّره PNG)")]
        public Sprite nameImage;

        [Tooltip("مدة الفقرة بالثواني")]
        public float duration = 8f;

        [Tooltip("كائن فيه مودلات هذا الشخص — يُفعّل طوال الفقرة ثم يُطفأ")]
        public GameObject showcase;

        [Tooltip("يحطّ المودلات على منصّة الكريديت فتبقى في الكادر والكاميرا تبعد")]
        public bool showcaseInFrontOfCamera = true;

        [Tooltip("مكانها على المنصّة — صفر = مركز المنصّة فوق الاسم مباشرة")]
        public Vector3 showcaseOffset = Vector3.zero;

        [Tooltip("دوران المودلات حول نفسها (درجة/ثانية) — صفر يوقفه")]
        public float showcaseSpin = 14f;
    }

    [Header("التجميد")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("يجمّد اللاعب طوال النهاية. أطفئه ليمشي واللاعب يتفرّج على الكريديت — " +
             "الكاميرا تتابعه وهي تبعد فيطلع الطابع أحلى")]
    [SerializeField] private bool freezePlayer = false;
    [Tooltip("سكربتات حركة اللاعب — تُطفأ فقط إن كان Freeze Player مفعّلًا. " +
             "لاعب الهب بلا PlayerKillable، فهذي هي التي تجمّده هناك")]
    [SerializeField] private string[] freezeScriptsNamed =
    {
        "PlayerController",
        "A_CrouchAndJump",
        "A_ZipLineSystem",
        "LadderController",
        "BoxPusher",
    };
    [Tooltip("سكربتات تُطفأ دائمًا طوال الكريديت بالاسم — ما تحتاج تسحب شيئًا. " +
             "بالاسم عمدًا لا بمرجع مباشر: الربط بسكربت زميلك يكسر بناء الجميع لو " +
             "غيّر اسمه أو حذفه، وبالاسم يطبع تحذيرًا وكفى.")]
    [SerializeField] private string[] disableScriptsNamed = { "CameraFollow" };
    [Tooltip("سكربتات إضافية تُطفأ بالسحب — اتركها فارغة، القائمة أعلاه تكفي عادة")]
    [SerializeField] private MonoBehaviour[] disableOnCredits;

    [Header("الموسيقى")]
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.8f;
    [Tooltip("تخفّ الأغنية أثناء التعتيم الأخير")]
    [SerializeField] private bool fadeMusicOut = true;

    [Header("الكاميرا")]
    [Tooltip("الكاميرا — فارغة = الكاميرا الرئيسية")]
    [SerializeField] private Transform cameraToPull;
    [Tooltip("مقدار الابتعاد بفضاء الكاميرا: Z سالب = للخلف، Y موجب = للأعلى")]
    [SerializeField] private Vector3 pullOffset = new Vector3(0f, 4f, -14f);
    [Tooltip("توزّع حركة الكاميرا على طول الفقرات تلقائيًا — فتغيير مدة أي فقرة " +
             "لا يخرّب التناسق. أطفئه لتستعمل المدتين أدناه كما هما")]
    [SerializeField] private bool matchCameraToSections = true;
    [Tooltip("نصيب الابتعاد من الزمن الكلي — والباقي للرجوع")]
    [Range(0.1f, 0.9f)] [SerializeField] private float pullShare = 0.4f;
    [Tooltip("مدة الابتعاد يدويًا — تُستعمل فقط إن أُطفئ التوزيع التلقائي")]
    [SerializeField] private float pullDuration = 16f;
    [Tooltip("مدة الرجوع يدويًا — تُستعمل فقط إن أُطفئ التوزيع التلقائي")]
    [SerializeField] private float returnDuration = 24f;
    [Tooltip("تظل موجّهة لللاعب وهي تبعد")]
    [SerializeField] private bool keepLookingAtPlayer = true;

    [Header("منصّة الكريديت (3D)")]
    [Tooltip("مكان المنصّة أمام الكاميرا: Z موجب = أمامها. قرّبها إن تداخلت مع مبانٍ")]
    [SerializeField] private Vector3 stageOffset = new Vector3(0f, 0.2f, 6f);
    [Tooltip("حجم نص المنصّة في العالم — كبّره إن طلعت الأسماء صغيرة")]
    [SerializeField] private float stageScale = 0.0028f;
    [Tooltip("نزول الاسم تحت مركز المنصّة (متر) — المودلات فوقه")]
    [SerializeField] private float nameDrop = 1.3f;
    [Tooltip("حجم خط الاسم")]
    [SerializeField] private int nameFontSize = 120;
    [Tooltip("حجم خط الدور تحته")]
    [SerializeField] private int roleFontSize = 46;

    [Header("الأشكال الطائرة")]
    [Tooltip("أشكال تطلع من تحت لفوق وهي تدور — يُلتقط من نفس الكائن إن تُرك فارغًا")]
    [SerializeField] private FloatingProps floatingProps;

    [Header("الفقرات")]
    [Tooltip("تُملأ بالافتراضي إن تُركت فارغة")]
    [SerializeField] private List<Section> sections = new List<Section>();
    [Tooltip("ظهور واختفاء نص كل فقرة")]
    [SerializeField] private float textFade = 0.7f;

    [Header("النهاية")]
    [Tooltip("وقفة بعد آخر فقرة قبل بدء التعتيم — تريح الصورة بدل أن تسوّد فجأة")]
    [SerializeField] private float holdBeforeFade = 1.2f;
    [SerializeField] private float fadeDuration = 2.5f;
    [Tooltip("السين الذي يرجع له — لازم يكون في قائمة البناء")]
    [SerializeField] private string returnScene = "Hub-Menu";
    [Tooltip("يمسح الأعلام المزروعة، وإلا بدأت اللعبة التالية وكل شيء مفتوح")]
    [SerializeField] private bool resetProgress = true;

    [Header("أحداث")]
    [Tooltip("لحظة البدء — أوقف الموسيقى الأصلية، اكتم الأصوات...")]
    public UnityEvent onCreditsStarted;
    [Tooltip("بعد اكتمال التعتيم، قبل تحميل القائمة")]
    public UnityEvent onCreditsFinished;

    /// <summary>الفريق كما اتفقنا عليه — يُستعمل إن تُركت القائمة فارغة.</summary>
    private static Section[] DefaultSections() => new[]
    {
        new Section { title = "ABEER",  role = "ART & ENVIRONMENT DESIGN", duration = 8f },
        new Section { title = "RAZAN",  role = "ART & ENVIRONMENT DESIGN", duration = 8f },
        new Section { title = "SULTAN", role = "STEAM TOWN & THE CIRCUS",  duration = 8f },
        new Section { title = "OSAMA",  role = "THE HUB & THE CIRCUS",     duration = 8f },
        new Section { title = "ALI",    role = "TWILIGHT",                 duration = 8f },
    };

    private Transform stage;
    private Canvas nameCanvas;
    private Image blackout;
    private Image nameImage;
    private Text nameText;
    private Text roleText;
    private CanvasGroup nameGroup;
    private AudioSource musicSource;
    private bool playing;

    /// <summary>يشغّل النهاية — اربطه بـ <c>FlagBase.On All Flags Planted</c>.</summary>
    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Sequence());
    }

    [ContextMenu("تجربة: شغّل النهاية الآن")]
    private void DebugPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GameCredits] جرّبها أثناء التشغيل.", this);
            return;
        }

        Play();
    }

    private IEnumerator Sequence()
    {
        onCreditsStarted?.Invoke();

        if (sections == null || sections.Count == 0)
            sections = new List<Section>(DefaultSections());

        BuildOverlay();
        Transform player = FreezePlayer();
        PlayMusic();

        Transform cam = cameraToPull != null ? cameraToPull
                      : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null) BuildStage(cam);
        else Debug.LogWarning("[GameCredits] ما لقيت كاميرا — بلا كاميرا لا منصّة ولا حركة.", this);

        if (floatingProps == null) floatingProps = GetComponent<FloatingProps>();
        if (floatingProps != null && cam != null) floatingProps.Begin(cam);

        // الكاميرا تمشي على خطها الخاص بالتوازي مع الفقرات: تبعد أثناء فقرات
        // العرض ثم ترجع أثناء البقية، فلا تنتظر فقرة معيّنة ولا تتقطّع بينها
        if (cam != null) StartCoroutine(MoveCamera(cam, player, PullTime(), ReturnTime()));

        foreach (Section section in sections)
        {
            if (section != null) yield return PlaySection(section, cam);
        }

        if (holdBeforeFade > 0f) yield return new WaitForSeconds(holdBeforeFade);

        yield return FadeToBlack();

        if (floatingProps != null) floatingProps.Stop();

        onCreditsFinished?.Invoke();

        if (resetProgress) GameProgress.Instance.ResetProgress();

        ReturnToMenu();
    }

    // ───────────────────────────── الفقرات ─────────────────────────────

    private IEnumerator PlaySection(Section section, Transform cam)
    {
        Transform showcase = ShowModels(section, cam);

        SetName(section);
        yield return FadeGroup(nameGroup, 1f, textFade);

        float hold = Mathf.Max(0f, section.duration - textFade * 2f);
        float t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            TuneStage();   // تُقرأ كل إطار ليظهر أثر تعديل القيم أثناء التشغيل
            if (showcase != null && section.showcaseSpin != 0f)
                showcase.Rotate(Vector3.up, section.showcaseSpin * Time.deltaTime, Space.World);
            yield return null;
        }

        yield return FadeGroup(nameGroup, 0f, textFade);

        if (section.showcase != null)
        {
            if (showcase != null) showcase.SetParent(null, true);
            section.showcase.SetActive(false);
        }
    }

    /// <summary>يُظهر مودلات الفقرة على المنصّة، فتبقى في الكادر والكاميرا تبعد.</summary>
    private Transform ShowModels(Section section, Transform cam)
    {
        if (section.showcase == null) return null;

        Transform showcase = section.showcase.transform;
        section.showcase.SetActive(true);

        if (section.showcaseInFrontOfCamera && stage != null)
        {
            showcase.SetParent(stage, false);
            showcase.localPosition = section.showcaseOffset;
            showcase.localRotation = Quaternion.identity;
        }

        return showcase;
    }

    /// <summary>يعيد قراءة قيم المنصّة كل إطار — لا بريفاب، فالضبط يتم أثناء التشغيل.</summary>
    private void TuneStage()
    {
        if (stage == null) return;

        stage.localPosition = stageOffset;
        if (nameCanvas == null) return;

        Transform block = nameCanvas.transform;
        block.localPosition = new Vector3(0f, -nameDrop, 0f);
        block.localScale = Vector3.one * stageScale;
        LayoutText();
    }

    private void SetName(Section section)
    {
        bool hasImage = section.nameImage != null;

        if (nameImage != null)
        {
            nameImage.enabled = hasImage;
            if (hasImage)
            {
                nameImage.sprite = section.nameImage;
                // المقاس الأصلي للصورة حتى لا تتمدّد
                nameImage.rectTransform.sizeDelta =
                    new Vector2(section.nameImage.rect.width, section.nameImage.rect.height);
            }
        }

        if (nameText != null)
        {
            nameText.enabled = !hasImage;
            nameText.text = Spaced(section.title);
        }

        if (roleText != null) roleText.text = section.role;
    }

    // ───────────────────────────── الكاميرا ─────────────────────────────

    /// <summary>مجموع مدد الفقرات — الزمن الذي على الكاميرا أن تملأه.</summary>
    private float SectionsTime()
    {
        float total = 0f;
        if (sections != null)
            foreach (Section section in sections)
                if (section != null) total += section.duration;

        return total;
    }

    /// <summary>
    /// مدة الابتعاد. تُحسب من طول الفقرات لا تُكتب يدويًا: أي تعديل على مدة فقرة كان
    /// يترك الكاميرا راجعةً والتعتيم قد بدأ، أو واقفةً تنتظر نصف العرض.
    /// </summary>
    private float PullTime()
    {
        float total = SectionsTime();
        if (!matchCameraToSections || total < 0.1f) return pullDuration;
        return total * pullShare;
    }

    private float ReturnTime()
    {
        float total = SectionsTime();
        if (!matchCameraToSections || total < 0.1f) return returnDuration;
        return total - total * pullShare;
    }

    private IEnumerator MoveCamera(Transform cam, Transform player, float out_, float back)
    {
        Vector3 home = cam.position;
        Quaternion homeRot = cam.rotation;
        Vector3 away = home + homeRot * pullOffset;   // بفضاء الكاميرا لا العالم

        yield return Glide(cam, home, away, homeRot, player, out_, false);
        yield return Glide(cam, away, home, homeRot, player, back, true);
    }

    /// <summary>
    /// حركة ناعمة بين وضعين — البداية والنهاية بطيئتان فلا تبدأ بقفزة.
    ///
    /// <paramref name="settle"/> لرحلة العودة: الزاوية تلتقي بزاوية البداية تدريجيًا
    /// حتى تطابقها تمامًا عند الوصول. بدونها كانت متابعة اللاعب تسحب الزاوية بعيدًا
    /// طوال الرحلة ثم تُصحَّح بقفزة في آخر إطار — والتوقيت يصادف بداية التعتيم.
    /// </summary>
    private IEnumerator Glide(Transform cam, Vector3 from, Vector3 to, Quaternion homeRot,
                              Transform player, float duration, bool settle)
    {
        if (duration <= 0f)
        {
            cam.position = to;
            if (settle) cam.rotation = homeRot;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

            cam.position = Vector3.Lerp(from, to, k);

            Quaternion want = homeRot;
            if (keepLookingAtPlayer && player != null)
            {
                Vector3 dir = player.position - cam.position;
                if (dir.sqrMagnitude > 0.0001f)
                    want = Quaternion.Slerp(cam.rotation,
                        Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 2f);
                else
                    want = cam.rotation;
            }

            // k*k: الالتقاء متأخّر وهادئ بدل أن يشدّها نحو زاوية البداية من أول متر
            cam.rotation = settle ? Quaternion.Slerp(want, homeRot, k * k) : want;

            yield return null;
        }

        cam.position = to;
        if (settle) cam.rotation = homeRot;
    }

    // ───────────────────────────── التجميد والصوت ─────────────────────────────

    /// <summary>
    /// يوقف ما يجب إيقافه ويرجّع اللاعب. التجميد اختياري: اللاعب يمشي أثناء الكريديت
    /// افتراضيًا والكاميرا تتابعه وهي تبعد.
    /// </summary>
    private Transform FreezePlayer()
    {
        if (disableOnCredits != null)
            foreach (var b in disableOnCredits)
                if (b != null) b.enabled = false;

        int stopped = DisableByName(disableScriptsNamed);
        int dragged = disableOnCredits != null ? disableOnCredits.Length : 0;

        if (pullDuration > 0f && stopped == 0 && dragged == 0)
            Debug.LogWarning("[GameCredits] ما أطفيت أي سكربت متابعة كاميرا — تأكد أن اسم " +
                             "السكربت في Disable Scripts Named مطابق، وإلا شدّ الكاميرا " +
                             "لللاعب كل إطار وما بعدت خطوة.", this);

        var go = PlayerLocator.Find(playerTag);
        if (go == null) return null;

        if (!freezePlayer) return go.transform;

        var rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // PlayerKillable الطريق الأنظف حيث وُجد، ولاعب الهب بلا PlayerKillable
        // أصلًا — فقائمة الأسماء هي التي تجمّده هناك
        var killable = go.GetComponentInParent<PlayerKillable>();
        if (killable != null) killable.FreezeControl();

        if (DisableByName(freezeScriptsNamed) == 0 && killable == null)
            Debug.LogWarning("[GameCredits] ما جمّدت اللاعب: لا PlayerKillable عليه ولا طابق " +
                             "أي اسم في Freeze Scripts Named. حُطّ اسم سكربت حركته هناك، " +
                             "أو أطفئ Freeze Player إن كنت تريده يمشي.", this);

        return go.transform;
    }

    /// <summary>
    /// يطفئ السكربتات المسمّاة. البحث بالاسم لا بالنوع مقصود: النوع يعني
    /// اعتمادًا على ملف زميل في نفس التجميعة، فلو حذفه أو غيّر اسمه انكسر بناء
    /// الجميع — وهنا يطبع تحذيرًا ويكمل.
    /// </summary>
    private int DisableByName(string[] names)
    {
        if (names == null || names.Length == 0) return 0;

        int count = 0;
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
                                                   FindObjectsSortMode.None);
        foreach (var behaviour in all)
        {
            if (behaviour == null || behaviour == this || !behaviour.enabled) continue;

            string typeName = behaviour.GetType().Name;
            foreach (string wanted in names)
            {
                if (string.IsNullOrWhiteSpace(wanted)) continue;
                if (!string.Equals(typeName, wanted.Trim(),
                                   System.StringComparison.OrdinalIgnoreCase)) continue;

                behaviour.enabled = false;
                count++;
                Debug.Log($"[GameCredits] أطفأت {typeName} على «{behaviour.gameObject.name}».",
                          behaviour);
                break;
            }
        }

        return count;
    }

    private void PlayMusic()
    {
        if (music == null) return;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.volume = musicVolume;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;   // ثنائي الأبعاد: الكاميرا بعيدة عن اللاعب
        musicSource.playOnAwake = false;
        musicSource.Play();
    }

    // ───────────────────────────── النهاية ─────────────────────────────

    private IEnumerator FadeToBlack()
    {
        float startVolume = musicSource != null ? musicSource.volume : 0f;

        if (fadeDuration <= 0f)
        {
            blackout.color = new Color(0f, 0f, 0f, 1f);
            yield break;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            blackout.color = new Color(0f, 0f, 0f, k);
            if (fadeMusicOut && musicSource != null) musicSource.volume = startVolume * (1f - k);

            yield return null;
        }

        blackout.color = new Color(0f, 0f, 0f, 1f);
    }

    private void ReturnToMenu()
    {
        if (string.IsNullOrWhiteSpace(returnScene)) return;

        if (!Application.CanStreamedLevelBeLoaded(returnScene))
        {
            Debug.LogError($"[GameCredits] السين \"{returnScene}\" غير موجود في قائمة مشاهد " +
                           "البناء — أضِفه من File → Build Profiles → Scene List.", this);
            return;
        }

        // بلا شاشة تحميل: الشاشة سوداء أصلًا، وصورة وجهة فوقها تقطع الجو
        SceneManager.LoadSceneAsync(returnScene);
    }

    // ───────────────────────────── الواجهة ─────────────────────────────

    /// <summary>
    /// التعتيم الأخير وحده فوق الشاشة — هذا الشيء الوحيد الذي يجب أن يغطّيها كاملة،
    /// فلا معنى لجعله ثلاثي الأبعاد.
    /// </summary>
    private void BuildOverlay()
    {
        var root = new GameObject("GameCredits_Fade", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;   // تحت شاشة التحميل (5000) وفوق واجهات السين

        blackout = NewImage(root.transform, "Blackout", new Color(0f, 0f, 0f, 0f));
        Stretch(blackout.rectTransform);
    }

    /// <summary>
    /// منصّة الكريديت في العالم: الاسم والدور والمودلات كلها أجسام ثلاثية الأبعاد
    /// لها عمق ومنظور، لا واجهة مسطّحة ملصوقة على الشاشة.
    ///
    /// معلّقة بالكاميرا لا موضوعة في السين: الكاميرا تبعد عشرات الأمتار، ولو كانت
    /// المنصّة ثابتة في مكانها لصغرت حتى تختفي. هكذا تبقى في الكادر بحجمها بينما
    /// يتراجع الهب خلفها — وهذا هو المقصود من ابتعاد الكاميرا.
    ///
    /// النص بـ<c>UI.Text</c> على كانفس <c>World Space</c> وخط يونيتي المدمج، لا
    /// TextMeshPro: إعدادات TMP في هذا المشروع بلا خط افتراضي ومادّة خطّه بشيدر
    /// مفقود، فيرسم مربّعات وردية.
    /// </summary>
    private void BuildStage(Transform cam)
    {
        var root = new GameObject("GameCredits_Stage");
        stage = root.transform;
        stage.SetParent(cam, false);
        stage.localPosition = stageOffset;
        stage.localRotation = Quaternion.identity;

        var block = new GameObject("Names", typeof(RectTransform));
        block.transform.SetParent(stage, false);
        block.transform.localPosition = new Vector3(0f, -nameDrop, 0f);

        nameCanvas = block.AddComponent<Canvas>();
        nameCanvas.renderMode = RenderMode.WorldSpace;
        nameCanvas.worldCamera = cam.GetComponent<Camera>();

        var rect = (RectTransform)block.transform;
        rect.sizeDelta = new Vector2(1600f, 420f);
        rect.localScale = Vector3.one * stageScale;

        nameGroup = block.AddComponent<CanvasGroup>();
        nameGroup.alpha = 0f;

        nameImage = NewImage(block.transform, "NameImage", Color.white);
        nameImage.rectTransform.anchorMin = nameImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        nameImage.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameImage.enabled = false;

        Font font = BuiltinFont();
        nameText = NewText(block.transform, font, FontStyle.Bold,
                           new Color(1f, 1f, 1f, 0.95f));
        roleText = NewText(block.transform, font, FontStyle.Normal,
                           new Color(1f, 0.92f, 0.7f, 0.85f));
        LayoutText();
    }

    private static Text NewText(Transform parent, Font font, FontStyle style, Color color)
    {
        if (font == null) return null;

        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontStyle = style;
        text.alignment = TextAnchor.LowerCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        return text;
    }

    /// <summary>
    /// يرتّب السطرين حسب حجميهما: الدور في الأسفل والاسم فوقه بفراغ يتبع حجم الدور،
    /// فتغيير الحجم لا يجعلهما يتراكبان.
    /// </summary>
    private void LayoutText()
    {
        if (roleText != null)
        {
            roleText.fontSize = roleFontSize;
            roleText.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            roleText.rectTransform.sizeDelta = new Vector2(1600f, roleFontSize + 20f);
        }

        if (nameText != null)
        {
            nameText.fontSize = nameFontSize;
            nameText.rectTransform.anchoredPosition = new Vector2(0f, 20f + roleFontSize + 26f);
            nameText.rectTransform.sizeDelta = new Vector2(1600f, nameFontSize + 20f);
        }

        if (nameImage != null)
            nameImage.rectTransform.anchoredPosition = new Vector2(0f, 20f + roleFontSize + 26f);
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        if (group == null) yield break;

        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }

        group.alpha = target;
    }

    /// <summary>خط يونيتي المدمج — موجود في كل بناء بلا استيراد.</summary>
    private static Font BuiltinFont()
    {
        // الاسم تغيّر في 2022.2: Arial.ttf صار LegacyRuntime.ttf، وطلب الاسم القديم
        // يطبع خطأ في الكونسول — فلا نجرّبه، ونسقط على خط النظام إن غاب
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 68);
        if (font == null) Debug.LogWarning("[GameCredits] ما لقيت خطًا مدمجًا — الأسماء بلا نص.");
        return font;
    }

    /// <summary>مسافة بين الحروف — UI.Text بلا تباعد، فنفرّقها بأنفسنا.</summary>
    private static string Spaced(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return string.Join(" ", text.ToCharArray());
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        holdBeforeFade = Mathf.Max(0f, holdBeforeFade);
        nameFontSize = Mathf.Max(8, nameFontSize);
        roleFontSize = Mathf.Max(6, roleFontSize);
        textFade = Mathf.Max(0f, textFade);
        pullDuration = Mathf.Max(0f, pullDuration);
        returnDuration = Mathf.Max(0f, returnDuration);

        if (sections == null) return;
        foreach (Section s in sections)
            if (s != null) s.duration = Mathf.Max(textFade * 2f, s.duration);
    }
}

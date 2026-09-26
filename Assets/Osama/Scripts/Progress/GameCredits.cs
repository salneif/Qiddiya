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

        [Tooltip("يعلّق المودلات أمام الكاميرا فتمشي معها وهي تبعد")]
        public bool showcaseInFrontOfCamera = true;

        [Tooltip("مكان المودلات بالنسبة للكاميرا (Z موجب = أمامها)")]
        public Vector3 showcaseOffset = new Vector3(0f, -0.4f, 6f);

        [Tooltip("دوران المودلات حول نفسها (درجة/ثانية) — صفر يوقفه")]
        public float showcaseSpin = 14f;
    }

    [Header("التجميد")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("يجمّد حركة اللاعب عبر قائمة Disable On Death في PlayerKillable")]
    [SerializeField] private bool freezePlayer = true;
    [Tooltip("سكربتات تُطفأ طوال الكريديت: تتبّع الكاميرا وأي تحكّم آخر")]
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
    [Tooltip("مدة الابتعاد — خلّها بطول فقرات العرض (عبير + رزان)")]
    [SerializeField] private float pullDuration = 32f;
    [Tooltip("مدة الرجوع لمكانها بعد فقرات العرض")]
    [SerializeField] private float returnDuration = 24f;
    [Tooltip("تظل موجّهة لللاعب وهي تبعد")]
    [SerializeField] private bool keepLookingAtPlayer = true;

    [Header("الفقرات")]
    [Tooltip("تُملأ بالافتراضي إن تُركت فارغة")]
    [SerializeField] private List<Section> sections = new List<Section>();
    [Tooltip("ظهور واختفاء نص كل فقرة")]
    [SerializeField] private float textFade = 0.7f;

    [Header("النهاية")]
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
        new Section { title = "ABEER",  role = "ART & ENVIRONMENT DESIGN", duration = 16f },
        new Section { title = "RAZAN",  role = "ART & ENVIRONMENT DESIGN", duration = 16f },
        new Section { title = "SULTAN", role = "STEAM TOWN & THE CIRCUS",  duration = 8f  },
        new Section { title = "OSAMA",  role = "THE HUB & THE CIRCUS",     duration = 8f  },
        new Section { title = "ALI",    role = "TWILIGHT",                 duration = 8f  },
    };

    private Canvas canvas;
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

        Build();
        Transform player = FreezePlayer();
        PlayMusic();

        Transform cam = cameraToPull != null ? cameraToPull
                      : (Camera.main != null ? Camera.main.transform : null);

        // الكاميرا تمشي على خطها الخاص بالتوازي مع الفقرات: تبعد أثناء فقرات
        // العرض ثم ترجع أثناء البقية، فلا تنتظر فقرة معيّنة ولا تتقطّع بينها
        if (cam != null) StartCoroutine(MoveCamera(cam, player));

        foreach (Section section in sections)
        {
            if (section != null) yield return PlaySection(section, cam);
        }

        yield return FadeToBlack();

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

    /// <summary>يُظهر مودلات الفقرة، ويعلّقها بالكاميرا لتمشي معها وهي تبعد.</summary>
    private Transform ShowModels(Section section, Transform cam)
    {
        if (section.showcase == null) return null;

        Transform showcase = section.showcase.transform;
        section.showcase.SetActive(true);

        if (section.showcaseInFrontOfCamera && cam != null)
        {
            showcase.SetParent(cam, false);
            showcase.localPosition = section.showcaseOffset;
            showcase.localRotation = Quaternion.identity;
        }

        return showcase;
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

    private IEnumerator MoveCamera(Transform cam, Transform player)
    {
        Vector3 home = cam.position;
        Quaternion homeRot = cam.rotation;
        Vector3 away = home + homeRot * pullOffset;   // بفضاء الكاميرا لا العالم

        yield return Glide(cam, home, away, homeRot, player, pullDuration);
        yield return Glide(cam, away, home, homeRot, player, returnDuration);

        cam.position = home;
        cam.rotation = homeRot;
    }

    /// <summary>حركة ناعمة بين وضعين — البداية والنهاية بطيئتان فلا تبدأ بقفزة.</summary>
    private IEnumerator Glide(Transform cam, Vector3 from, Vector3 to,
                              Quaternion homeRot, Transform player, float duration)
    {
        if (duration <= 0f)
        {
            cam.position = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

            cam.position = Vector3.Lerp(from, to, k);

            if (keepLookingAtPlayer && player != null)
            {
                Vector3 dir = player.position - cam.position;
                if (dir.sqrMagnitude > 0.0001f)
                    cam.rotation = Quaternion.Slerp(cam.rotation,
                        Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 2f);
            }
            else
            {
                cam.rotation = homeRot;
            }

            yield return null;
        }

        cam.position = to;
    }

    // ───────────────────────────── التجميد والصوت ─────────────────────────────

    private Transform FreezePlayer()
    {
        if (disableOnCredits != null)
            foreach (var b in disableOnCredits)
                if (b != null) b.enabled = false;

        if (pullDuration > 0f && (disableOnCredits == null || disableOnCredits.Length == 0))
            Debug.LogWarning("[GameCredits] قائمة Disable On Credits فارغة — حُطّ فيها سكربت " +
                             "متابعة الكاميرا، وإلا رجعت الكاميرا لللاعب ولم تبعد.", this);

        var go = PlayerLocator.Find(playerTag);
        if (go == null) return null;

        var rb = go.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (freezePlayer)
        {
            var killable = go.GetComponentInParent<PlayerKillable>();
            if (killable != null) killable.FreezeControl();
            else Debug.LogWarning("[GameCredits] ما فيه PlayerKillable على اللاعب — " +
                                  "ما قدرت أجمّد حركته.", this);
        }

        return go.transform;
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
    /// كانفس يُبنى بالكود، فلا يحتاج بريفابًا ولا ربطًا في السين.
    ///
    /// النص بـ<c>UI.Text</c> وخط يونيتي المدمج لا TextMeshPro: إعدادات TMP في هذا
    /// المشروع بلا خط افتراضي ومادّة خطّه بشيدر مفقود، فيرسم مربّعات وردية.
    /// </summary>
    private void Build()
    {
        var root = new GameObject("GameCredits_UI", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;   // تحت شاشة التحميل (5000) وفوق واجهات السين

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var block = new GameObject("Names", typeof(RectTransform));
        block.transform.SetParent(root.transform, false);
        nameGroup = block.AddComponent<CanvasGroup>();
        nameGroup.alpha = 0f;
        Stretch((RectTransform)block.transform);

        nameImage = NewImage(block.transform, "NameImage", Color.white);
        nameImage.rectTransform.anchorMin = nameImage.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        nameImage.rectTransform.pivot = new Vector2(0.5f, 0f);
        nameImage.rectTransform.anchoredPosition = new Vector2(0f, 190f);
        nameImage.enabled = false;

        Font font = BuiltinFont();
        nameText = NewText(block.transform, font, 68, FontStyle.Bold, 250f,
                           new Color(1f, 1f, 1f, 0.95f));
        roleText = NewText(block.transform, font, 26, FontStyle.Normal, 190f,
                           new Color(1f, 0.92f, 0.7f, 0.8f));

        blackout = NewImage(root.transform, "Blackout", new Color(0f, 0f, 0f, 0f));
        Stretch(blackout.rectTransform);
    }

    private Text NewText(Transform parent, Font font, int size, FontStyle style,
                         float bottom, Color color)
    {
        if (font == null) return null;

        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.LowerCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.sizeDelta = new Vector2(1600f, size + 20f);
        return text;
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
        textFade = Mathf.Max(0f, textFade);
        pullDuration = Mathf.Max(0f, pullDuration);
        returnDuration = Mathf.Max(0f, returnDuration);

        if (sections == null) return;
        foreach (Section s in sections)
            if (s != null) s.duration = Mathf.Max(textFade * 2f, s.duration);
    }
}

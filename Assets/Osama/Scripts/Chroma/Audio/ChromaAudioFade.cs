using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// صوت اللعبة كلها يدخل بهدوء ويخرج بهدوء: يعلو من الصمت مع أول كل سين، وينزل إلى
/// الصمت حين تبدأ شاشة التحميل — فلا موسيقى تنقضّ على اللاعب لحظة وصوله (موسيقى الهب
/// كانت تبدأ بكامل قوّتها في الإطار الأول)، ولا أخرى تنقطع في منتصف نغمتها لحظة التبديل.
///
/// <b>المقبض الوحيد هو <see cref="AudioListener.volume"/></b>: لا ميكسر في المشروع، و
/// <see cref="AudioManager"/> يضع فيه الرئيسي من الإعدادات. فنكتب فيه <c>الرئيسي × منحنى</c>،
/// ونقرأ الرئيسي من <see cref="PlayerPrefs"/> في كل إطار نعمل فيه: إن حرّك اللاعب السلايدر
/// أثناء التلاشي تبعناه، وإن أعاد <c>AudioManager</c> تطبيقه (يفعلها لحظة يُنشأ) غلبناه في
/// <c>LateUpdate</c> من الإطار نفسه. وحين يكتمل الدخول نكتب الرئيسي بالضبط ونسكت — فلا
/// نتجاوزه أبدًا، ولا نزاحم أحدًا بعدها.
///
/// المستوى يمشي خطّيًا في الزمن والصوت = <b>مربّعه</b>: الأذن تسمع بالديسيبل، والخطّي يبدو
/// كأنه يقفز في أوّله ويتباطأ في آخره، والمربّع يُسمع صعودًا متّصلًا.
///
/// فيديو الانترو مخرجه Direct لا يمرّ بالسامع، فلا يمسّه شيء هنا — <see cref="HoldToSkip"/>
/// يُدخله ويخفته بيده، على الرئيسي نفسه.
///
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DisallowMultipleComponent]
public class ChromaAudioFade : MonoBehaviour
{
    /// <summary>
    /// دخول الصوت في كل سين. أطول من انكشاف شاشة التحميل (0.6 ث) عمدًا: الصورة تظهر أولًا
    /// والعالم يعلو بعدها، كمن يفتح نافذة.
    /// </summary>
    private const float SceneFadeIn = 2.2f;

    /// <summary>
    /// أطول مدّة يبقى فيها الصوت مُسكتًا بلا شاشة تحميل تعمل. من أسكته ثم لم ينتقل (سينٌ
    /// ناقص من قائمة البناء مثلًا) لا يترك اللعبة خرساء.
    /// </summary>
    private const float SilenceRescue = 4f;

    /// <summary>أطول خطوة للمنحنى في إطار: تهنيقة تحميل لا تقفز بالصوت دفعةً.</summary>
    private const float MaxStep = 1f / 20f;

    private static ChromaAudioFade instance;

    private float level = 1f;       // خطّي في الزمن، والصوت = مربّعه
    private float target = 1f;
    private float speed = 1f;       // وحدات مستوى في الثانية
    private bool driving;           // نملك AudioListener.volume إلى أن يكتمل الدخول
    private float silencedAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("ChromaAudioFade") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<ChromaAudioFade>();
        DontDestroyOnLoad(host);

        // أول سين حُمّل قبل أن نوجد فلم يصلنا sceneLoaded له — ندخله بأنفسنا
        instance.EnterScene();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // ---------- الواجهة ----------

    /// <summary>
    /// يُنزل صوت اللعبة كلها إلى الصمت خلال <paramref name="seconds"/>، ويبقيه صامتًا حتى
    /// يُحمَّل سينٌ جديد (فيدخل وحده) أو يُنادى <see cref="FadeIn"/>.
    /// </summary>
    public static void FadeOut(float seconds)
    {
        if (instance == null) return;

        instance.Go(0f, seconds);
        instance.silencedAt = Time.unscaledTime;
    }

    /// <summary>يعيد الصوت إلى مستوى الرئيسي خلال <paramref name="seconds"/>.</summary>
    public static void FadeIn(float seconds)
    {
        if (instance == null) return;

        instance.Go(1f, seconds);
        instance.silencedAt = -1f;
    }

    // ---------- الداخل ----------

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (instance != this) return;

        // الخروج من التشغيل في منتصف تلاشٍ لا يترك السامع على نصف صوت
        if (driving) AudioListener.volume = Master;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) EnterScene();
    }

    /// <summary>
    /// سينٌ جديد يبدأ صامتًا ثم يعلو. نكتب الصمت فورًا لا في <c>LateUpdate</c>: مصادر
    /// <c>Play On Awake</c> بدأت للتو، وأوّل إطار منها هو الضربة التي نمنعها.
    /// </summary>
    private void EnterScene()
    {
        level = 0f;
        silencedAt = -1f;
        Go(1f, SceneFadeIn);
        Apply();
    }

    private void Go(float to, float seconds)
    {
        target = Mathf.Clamp01(to);
        speed = 1f / Mathf.Max(0.01f, seconds);
        driving = true;
    }

    private void LateUpdate()
    {
        if (!driving) return;

        if (target <= 0f && silencedAt >= 0f && !LoadingOverlay.IsBusy &&
            Time.unscaledTime - silencedAt > SilenceRescue)
            FadeIn(1f);

        // غير متأثّر بالإيقاف: الانتقال من لوحة الإيقاف يبدأ والوقت متجمّد
        level = Mathf.MoveTowards(level, target, Mathf.Min(Time.unscaledDeltaTime, MaxStep) * speed);
        Apply();

        if (level >= 1f && target >= 1f) driving = false;
    }

    private void Apply() =>
        AudioListener.volume = level >= 1f ? Master : Master * level * level;

    /// <summary>الرئيسي كما حفظه السلايدر — بنفس مفتاح <see cref="AudioManager"/> وافتراضه.</summary>
    public static float Master => Mathf.Clamp01(PlayerPrefs.GetFloat(AudioManager.MasterKey, 1f));
}

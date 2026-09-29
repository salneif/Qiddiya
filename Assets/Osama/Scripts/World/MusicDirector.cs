using UnityEngine;

/// <summary>
/// قائد الموسيقى — واحد فقط في المشهد. يدير طبقتين لا تتعارضان:
///
///  • <b>طبقة المنطقة</b>: موسيقى المكان (باك ستيج / سيرك / هَب). تتبدّل بمزج
///    متقاطع (Crossfade) عبر مصدرين يتبادلان، فلا يوجد صمت بين مقطع وآخر.
///
///  • <b>طبقة التجاوز</b>: موسيقى المطاردة. حين تشتغل تنخفض موسيقى المنطقة
///    إلى <see cref="duckedAreaVolume"/> بدل أن تُقطع، وحين تنتهي ترجع كما كانت
///    ومن نفس موضعها في المقطع — فيحس اللاعب أن العالم استأنف حياته لا أنه بدأ من جديد.
///
/// <b>المزج بقوّةٍ ثابتة</b>: لكل مصدرٍ تقدّمٌ خطّيٌّ في الزمن، ومستواه = جيبُه. الداخل
/// يعلو بالجيب والخارج يخفت بجيب التمام، فمجموع طاقتهما ثابت طوال المزج. المزج الخطّي
/// القديم كان يُسقط الصوت نحو ٣ ديسيبل في منتصفه — حفرةٌ تُسمع بين كل منطقتين.
///
/// <b>والرجوع لا يبدأ من الصفر</b>: من عاد لمنطقةٍ ما زالت موسيقاها تخفت تعود هي نفسها
/// من حيث وصلت. كان المصدر الخافت يُعاد استعماله فيُقطع صوته دفعةً ويبدأ مقطعه من أوّله.
///
/// الزمن غير متأثّر بالإيقاف: الموسيقى تعزف ولوحة الإيقاف مفتوحة، فمزجها يكمل معها ولا
/// يتجمّد في منتصفه.
///
/// كل الخلط يتم في Update بأهداف مستوى (لا Coroutines)، فأي تبديل جديد يقاطع
/// السابق فورًا بلا تراكم ولا تعارض.
///
/// التركيب: كائن فارغ في السين + هذا السكربت. المصادر تُنشأ تلقائيًا.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    public static MusicDirector Instance { get; private set; }

    [Header("المستويات")]
    [Tooltip("مستوى موسيقى المنطقة العادي")]
    [Range(0f, 1f)]
    [SerializeField] private float areaVolume = 0.45f;
    [Tooltip("مستوى موسيقى المطاردة")]
    [Range(0f, 1f)]
    [SerializeField] private float overrideVolume = 0.6f;
    [Tooltip("مستوى موسيقى المنطقة أثناء المطاردة. صفر = تصمت تمامًا، " +
             "ورقم صغير (0.1) يُبقيها تحت المطاردة فيحس اللاعب أن المكان ما زال موجودًا.")]
    [Range(0f, 1f)]
    [SerializeField] private float duckedAreaVolume = 0f;

    [Header("التوقيت")]
    [Tooltip("مدة المزج بين موسيقى منطقة وأخرى (ثواني)")]
    [SerializeField] private float crossfadeTime = 2.5f;
    [Tooltip("مدة دخول موسيقى المطاردة — أقصر ليكون دخولها مفاجئًا")]
    [SerializeField] private float overrideFadeTime = 0.8f;
    [Tooltip("مدة خروج موسيقى المطاردة حين تنتهي — أطول من دخولها: النهاية ارتياحٌ لا قطع")]
    [SerializeField] private float overrideReleaseTime = 2f;

    /// <summary>أطول خطوة مزج في إطار: تهنيقةٌ (تحميل، نافذة تُسحب) لا تقفز بالمستوى.</summary>
    private const float MaxStep = 0.1f;

    /// <summary>مصدرٌ وتقدّم مزجه: خطّيٌّ في الزمن من 0 إلى 1، والمستوى = القاعدة × جيبه.</summary>
    private sealed class Layer
    {
        public AudioSource source;
        public float fade;

        public float Audible => source != null && source.isPlaying ? fade : 0f;
    }

    private Layer areaA, areaB, overrideLayer;
    private Layer activeArea, fadingArea;
    private AudioClip currentAreaClip;
    private bool overrideActive;
    private float duck = 1f;    // 1 = المنطقة بمستواها، 0 = منخفضة تحت المطاردة

    private void Awake()
    {
        // آخر واحد يفوز: لو كان في المشهد اثنان، الأحدث يحل محل القديم بدل التعارض
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;

        areaA = CreateLayer("Music_AreaA");
        areaB = CreateLayer("Music_AreaB");
        overrideLayer = CreateLayer("Music_Override");
        activeArea = areaA;
        fadingArea = areaB;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Layer CreateLayer(string sourceName)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // الموسيقى ثنائية الأبعاد دائمًا، لا تخفت بالمسافة
        src.volume = 0f;
        return new Layer { source = src };
    }

    /// <summary>
    /// يبدّل موسيقى المنطقة بمزج متقاطع. إعادة نفس المقطع لا تفعل شيئًا،
    /// فالمرور مرتين في نفس المنطقة لا يعيد المقطع من أوله.
    /// </summary>
    public void PlayArea(AudioClip clip)
    {
        // activeArea فارغ = لم يمرّ Awake (القائد على كائن مطفأ) — حدثٌ يصل مبكرًا لا يكسر شيئًا
        if (clip == null || clip == currentAreaClip || activeArea == null) return;
        currentAreaClip = clip;

        // المطلوبة ما زالت تعزف وهي تخفت (بعد StopAll أو بعد تبديل): تعود هي من حيث وصلت،
        // بلا قطعٍ ولا نسخةٍ ثانيةٍ تبدأ من أوّلها فوقها
        if (activeArea.source.clip == clip && activeArea.source.isPlaying) return;
        if (fadingArea.source.clip == clip && fadingArea.source.isPlaying)
        {
            (activeArea, fadingArea) = (fadingArea, activeArea);
            return;
        }

        // المقطع الجديد يأخذ المصدر الأخفت، والأعلى يكمل خفوته من حيث هو. في العادة
        // الأخفت هو الصامت أصلًا؛ وفي تبديلٍ سريعٍ بين ثلاث مناطق يُقطع الأهدأ لا الأعلى
        Layer incoming = activeArea.Audible <= fadingArea.Audible ? activeArea : fadingArea;
        fadingArea = incoming == activeArea ? fadingArea : activeArea;
        activeArea = incoming;

        activeArea.fade = 0f;
        activeArea.source.volume = 0f;
        activeArea.source.clip = clip;
        activeArea.source.Play();
    }

    /// <summary>يشغّل موسيقى المطاردة فوق موسيقى المنطقة (التي تنخفض تلقائيًا).</summary>
    public void PlayOverride(AudioClip clip)
    {
        if (clip == null || overrideLayer == null) return;

        overrideActive = true;
        AudioSource src = overrideLayer.source;
        if (src.clip != clip)
        {
            overrideLayer.fade = 0f;
            src.volume = 0f;
            src.clip = clip;
        }
        // مطاردةٌ تعود وهي ما زالت تخفت: تعلو من حيث هي بلا بدايةٍ جديدة
        if (!src.isPlaying) src.Play();
    }

    /// <summary>ينهي موسيقى المطاردة وتعود موسيقى المنطقة لمستواها.</summary>
    public void ClearOverride() => overrideActive = false;

    /// <summary>يوقف كل شيء بهدوء — لباب الانتقال بين المراحل.</summary>
    public void StopAll()
    {
        currentAreaClip = null;
        overrideActive = false;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
        float areaStep = dt / Mathf.Max(0.01f, crossfadeTime);
        float overrideStep = dt / Mathf.Max(0.01f, overrideActive ? overrideFadeTime : overrideReleaseTime);

        // المنطقة تنخفض بخطى المطاردة نفسها: تخرج بجيب التمام ما تدخل هي بالجيب، فتتبادلان
        // المكان بقوّةٍ ثابتة — لا تزاحم المطاردةَ أولها، ولا تقفز عائدةً بعد نهايتها.
        // وبعد StopAll لا ترتفع ثانيةً وهي خارجة: تخفت من حيث هي فقط
        float duckTarget = overrideActive ? 0f : 1f;
        if (currentAreaClip != null || duckTarget < duck)
            duck = Mathf.MoveTowards(duck, duckTarget, overrideStep);
        float areaLevel = Mathf.Lerp(duckedAreaVolume, areaVolume, Shape(duck));

        Move(activeArea, currentAreaClip != null ? 1f : 0f, areaStep, areaLevel);
        Move(fadingArea, 0f, areaStep, areaLevel);
        Move(overrideLayer, overrideActive ? 1f : 0f, overrideStep, overrideVolume);
    }

    private static void Move(Layer layer, float to, float step, float level)
    {
        if (layer == null || layer.source == null) return;

        layer.fade = Mathf.MoveTowards(layer.fade, to, step);
        layer.source.volume = level * Shape(layer.fade);

        // نوقف المصدر حين يصل الصمت المطلوب ليوفّر قناة — لا وهو ما زال يخفت، ولا
        // المنطقةَ الساكتةَ تحت المطاردة: تلك تنتظر لتعود من موضعها
        if (to <= 0f && layer.fade <= 0f && layer.source.isPlaying) layer.source.Stop();
    }

    /// <summary>منحنى القوّة الثابتة: جيب التقدّم. الداخل والخارج معًا مجموع مربّعيهما واحد.</summary>
    private static float Shape(float fade) => Mathf.Sin(Mathf.Clamp01(fade) * Mathf.PI * 0.5f);

    private void OnValidate()
    {
        crossfadeTime = Mathf.Max(0f, crossfadeTime);
        overrideFadeTime = Mathf.Max(0f, overrideFadeTime);
        overrideReleaseTime = Mathf.Max(0f, overrideReleaseTime);
    }
}

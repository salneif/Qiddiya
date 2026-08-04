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
    [Tooltip("مدة دخول وخروج موسيقى المطاردة — أقصر ليكون دخولها مفاجئًا")]
    [SerializeField] private float overrideFadeTime = 0.8f;

    private AudioSource areaA, areaB, overrideSource;
    private AudioSource activeArea, fadingArea;
    private AudioClip currentAreaClip;
    private bool overrideActive;

    private void Awake()
    {
        // آخر واحد يفوز: لو كان في المشهد اثنان، الأحدث يحل محل القديم بدل التعارض
        if (Instance != null && Instance != this) Destroy(Instance.gameObject);
        Instance = this;

        areaA = CreateSource("Music_AreaA");
        areaB = CreateSource("Music_AreaB");
        overrideSource = CreateSource("Music_Override");
        activeArea = areaA;
        fadingArea = areaB;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private AudioSource CreateSource(string sourceName)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // الموسيقى ثنائية الأبعاد دائمًا، لا تخفت بالمسافة
        src.volume = 0f;
        return src;
    }

    /// <summary>
    /// يبدّل موسيقى المنطقة بمزج متقاطع. إعادة نفس المقطع لا تفعل شيئًا،
    /// فالمرور مرتين في نفس المنطقة لا يعيد المقطع من أوله.
    /// </summary>
    public void PlayArea(AudioClip clip)
    {
        if (clip == null || clip == currentAreaClip) return;
        currentAreaClip = clip;

        // نبدّل الأدوار: الحالي يبدأ بالخفوت، والآخر يحمل المقطع الجديد
        (activeArea, fadingArea) = (fadingArea, activeArea);

        activeArea.clip = clip;
        activeArea.volume = 0f;
        activeArea.Play();
    }

    /// <summary>يشغّل موسيقى المطاردة فوق موسيقى المنطقة (التي تنخفض تلقائيًا).</summary>
    public void PlayOverride(AudioClip clip)
    {
        if (clip == null) return;

        overrideActive = true;
        if (overrideSource.clip != clip)
        {
            overrideSource.clip = clip;
            overrideSource.volume = 0f;
        }
        if (!overrideSource.isPlaying) overrideSource.Play();
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
        float areaRate = Rate(crossfadeTime);
        float overrideRate = Rate(overrideFadeTime);

        // المنطقة النشطة تنخفض أثناء المطاردة بدل أن تُقطع
        float areaTarget = currentAreaClip == null ? 0f
                         : (overrideActive ? duckedAreaVolume : areaVolume);

        Drive(activeArea, areaTarget, areaRate);
        Drive(fadingArea, 0f, areaRate, stopWhenSilent: true);
        Drive(overrideSource, overrideActive ? overrideVolume : 0f, overrideRate,
              stopWhenSilent: !overrideActive);
    }

    private static float Rate(float seconds) =>
        Time.deltaTime / Mathf.Max(0.01f, seconds);

    private static void Drive(AudioSource src, float target, float rate,
                              bool stopWhenSilent = false)
    {
        if (src == null) return;

        src.volume = Mathf.MoveTowards(src.volume, target, rate);

        // نوقف المصدر الصامت لتوفير قناة صوت، ولا نوقفه وهو ما زال يخفت
        if (stopWhenSilent && src.isPlaying && src.volume <= 0.001f) src.Stop();
    }
}

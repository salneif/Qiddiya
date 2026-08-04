using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// مدير الصوت العام: يحفظ مستويات الصوت في PlayerPrefs ويعيد تطبيقها في كل مشهد،
/// ويشغّل أصوات الواجهة (Hover / Click) من مصدر واحد بدل AudioSource على كل زر.
///
/// الإعداد: ضعه على كائن اسمه "AudioManager" في أول مشهد (Hub-Menu) — سيبقى بين المشاهد.
/// </summary>
[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public const string MasterKey = "vol_master";
    public const string MusicKey = "vol_music";
    public const string SfxKey = "vol_sfx";

    public static AudioManager Instance { get; private set; }

    [Header("الميكسر")]
    [Tooltip("AudioMixer فيه Exposed Parameters بالأسماء أدناه — إن تُرك فارغًا يتحكم Master بصوت اللعبة كاملًا")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string musicParam = "MusicVolume";
    [SerializeField] private string sfxParam = "SFXVolume";

    [Header("أصوات الواجهة")]
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioClip defaultHoverClip;
    [SerializeField] private AudioClip defaultClickClip;
    [SerializeField] private AudioClip defaultBackClip;

    public AudioClip DefaultHoverClip => defaultHoverClip;
    public AudioClip DefaultClickClip => defaultClickClip;
    public AudioClip DefaultBackClip => defaultBackClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f; // صوت واجهة ثنائي الأبعاد، لا يتأثر بمكان الكاميرا
        }

        LoadSavedVolumes();
    }

    // ---------- مستويات الصوت (القيمة 0..1 كما في السلايدر) ----------

    public void SetMasterVolume(float value) => Apply(masterParam, MasterKey, value);
    public void SetMusicVolume(float value) => Apply(musicParam, MusicKey, value);
    public void SetSfxVolume(float value) => Apply(sfxParam, SfxKey, value);

    public float GetMasterVolume() => PlayerPrefs.GetFloat(MasterKey, 1f);
    public float GetMusicVolume() => PlayerPrefs.GetFloat(MusicKey, 1f);
    public float GetSfxVolume() => PlayerPrefs.GetFloat(SfxKey, 1f);

    private void LoadSavedVolumes()
    {
        SetMasterVolume(GetMasterVolume());
        SetMusicVolume(GetMusicVolume());
        SetSfxVolume(GetSfxVolume());
    }

    private void Apply(string parameter, string prefsKey, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(prefsKey, value);

        if (mixer != null && !string.IsNullOrEmpty(parameter))
        {
            // الأذن تسمع لوغاريتميًا: تحويل 0..1 إلى ديسيبل يجعل السلايدر يبدو خطيًا
            mixer.SetFloat(parameter, LinearToDecibel(value));
        }
        else if (prefsKey == MasterKey)
        {
            AudioListener.volume = value; // بديل بسيط عند عدم وجود ميكسر
        }
    }

    private static float LinearToDecibel(float linear)
    {
        return linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
    }

    // ---------- أصوات الواجهة ----------

    public void PlayUI(AudioClip clip, float pitch = 1f)
    {
        if (clip == null || uiSource == null) return;

        uiSource.pitch = pitch;
        uiSource.PlayOneShot(clip);
    }

    public void PlayHover() => PlayUI(defaultHoverClip);
    public void PlayClick() => PlayUI(defaultClickClip);
    public void PlayBack() => PlayUI(defaultBackClip);

    private void OnApplicationQuit() => PlayerPrefs.Save();
}

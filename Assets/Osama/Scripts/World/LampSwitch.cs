using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// لمبة تبدّل العالم بين حالتين بأسلوب مسرحي:
///  - حالة الإطفاء (OFF): تنطفئ أغلب الأضواء، تختفي أشياء (كالعلم الأصفر)،
///    وصوت اللمبة يكون فيه صدى بسيط.
///  - حالة الإشعال (ON): يتحوّل العالم إلى أبيض وأسود، مع صوت إضاءة وموسيقى مختلفة.
///
/// تبدأ اللعبة (اختياريًا) واللمبة مطفأة فيطفى كل شيء ويختفي العلم، ثم عند إشعالها
/// ينقلب العالم أبيض وأسود.
///
/// نادِ <see cref="Toggle"/> أو <see cref="SetLamp"/> من زر تفاعل/تريجر، أو استخدم
/// زر الاختبار في المحرر.
/// </summary>
public class LampSwitch : MonoBehaviour
{
    [Header("الإضاءة")]
    [Tooltip("ضوء اللمبة نفسها")]
    [SerializeField] private Light lampLight;
    [Tooltip("أضواء العالم التي تنطفئ عند إطفاء اللمبة")]
    [SerializeField] private Light[] worldLights;

    [Header("أشياء تختفي عند الإطفاء")]
    [Tooltip("العلم الأصفر وأي أشياء تختفي في الظلام")]
    [SerializeField] private GameObject[] hideWhenOff;

    [Header("الأبيض والأسود")]
    [SerializeField] private WorldBWController worldBW;
    [Tooltip("عند الإشعال يتحوّل العالم إلى أبيض وأسود")]
    [SerializeField] private bool blackAndWhiteWhenOn = true;

    [Header("صوت اللمبة")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("صوت إشعال اللمبة (طقّة/أزيز إضاءة)")]
    [SerializeField] private AudioClip lampOnSfx;
    [Tooltip("صوت إطفاء اللمبة")]
    [SerializeField] private AudioClip lampOffSfx;
    [Tooltip("فلتر صدى يُفعّل في حالة الإطفاء لإعطاء إحساس الصدى البسيط")]
    [SerializeField] private AudioReverbFilter offReverb;

    [Header("الموسيقى")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("موسيقى حالة الأبيض والأسود (اللمبة مشتعلة)")]
    [SerializeField] private AudioClip onMusic;
    [Tooltip("موسيقى/جو الحالة المظلمة (اللمبة مطفأة)")]
    [SerializeField] private AudioClip offMusic;

    [Header("البداية")]
    [Tooltip("تبدأ اللعبة واللمبة مطفأة (يطفى كل شيء ويختفي العلم)")]
    [SerializeField] private bool startOff = true;

    [Header("اختبار (في المحرر)")]
    [Tooltip("زر لوحة مفاتيح لتبديل اللمبة أثناء التجربة (None لتعطيله)")]
    [SerializeField] private Key testToggleKey = Key.F;

    [Header("أحداث")]
    public UnityEvent onLampOn;
    public UnityEvent onLampOff;

    private bool isOn;

    /// <summary>هل اللمبة مشتعلة الآن؟</summary>
    public bool IsOn => isOn;

    private void Start()
    {
        // ضبط الحالة الابتدائية فورًا وبلا أصوات
        ApplyState(!startOff, playSfx: false);
    }

    private void Update()
    {
        if (testToggleKey != Key.None && Keyboard.current != null &&
            Keyboard.current[testToggleKey].wasPressedThisFrame)
            Toggle();
    }

    /// <summary>يبدّل حالة اللمبة (مع الأصوات).</summary>
    public void Toggle() => ApplyState(!isOn, playSfx: true);

    /// <summary>يضبط حالة اللمبة صراحةً (مع الأصوات).</summary>
    public void SetLamp(bool on) => ApplyState(on, playSfx: true);

    private void ApplyState(bool on, bool playSfx)
    {
        isOn = on;

        // اللمبة وأضواء العالم
        if (lampLight != null) lampLight.enabled = on;
        foreach (var l in worldLights)
            if (l != null) l.enabled = on;

        // أشياء تختفي في الظلام (تظهر فقط عند الإشعال)
        foreach (var go in hideWhenOff)
            if (go != null) go.SetActive(on);

        // تحويل العالم لأبيض وأسود
        if (worldBW != null)
            worldBW.SetBlackAndWhite(blackAndWhiteWhenOn && on);

        // الصوت: صدى في حالة الإطفاء
        if (offReverb != null) offReverb.enabled = !on;
        if (playSfx) PlaySfx(on ? lampOnSfx : lampOffSfx);

        // الموسيقى تتبدّل حسب الحالة
        SwitchMusic(on ? onMusic : offMusic);

        if (on) onLampOn?.Invoke();
        else onLampOff?.Invoke();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    private void SwitchMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}

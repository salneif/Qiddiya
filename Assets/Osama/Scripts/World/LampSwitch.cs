using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// لمبة تبدّل العالم بين حالتين بأسلوب مسرحي:
///  - حالة الإطفاء (OFF): تنطفئ أغلب الأضواء، والعالم ملوّن/مظلم، وصوت اللمبة فيه صدى بسيط.
///  - حالة الإشعال (ON): يتحوّل العالم إلى أبيض وأسود، مع صوت إضاءة وموسيقى مختلفة.
///
/// عند التبديل: تومض الشاشة إلى الأسود (~ثانية) لتخفي لحظة التحويل، وأثناء السواد
/// يُبدّل العالم فعليًا، ثم تنكشف الشاشة على العالم الجديد.
///
/// الأشياء في <see cref="hideInBlackAndWhite"/> تختفي عندما يصير العالم أبيض وأسود.
///
/// نادِ <see cref="Toggle"/> أو <see cref="SetLamp"/> من تفاعل/تريجر، أو استخدم زر الاختبار.
/// </summary>
public class LampSwitch : MonoBehaviour
{
    [Header("الإضاءة")]
    [Tooltip("ضوء اللمبة نفسها")]
    [SerializeField] private Light lampLight;
    [Tooltip("أضواء العالم التي تنطفئ عند إطفاء اللمبة")]
    [SerializeField] private Light[] worldLights;

    [Header("أشياء تختفي في الأبيض والأسود")]
    [Tooltip("العلم الأصفر وأي أشياء ملوّنة تختفي عندما يصير العالم أبيض وأسود")]
    [SerializeField] private GameObject[] hideInBlackAndWhite;

    [Header("الأبيض والأسود")]
    [SerializeField] private WorldBWController worldBW;
    [Tooltip("عند الإشعال يتحوّل العالم إلى أبيض وأسود")]
    [SerializeField] private bool blackAndWhiteWhenOn = true;

    [Header("وميض الشاشة السوداء عند التحويل")]
    [Tooltip("صورة سوداء تغطّي الشاشة عليها Canvas Group — تومض عند التبديل")]
    [SerializeField] private CanvasGroup blackScreen;
    [Tooltip("سرعة تعتيم/كشف الشاشة (ثواني)")]
    [SerializeField] private float blinkFadeTime = 0.15f;
    [Tooltip("مدة بقاء الشاشة سوداء قبل كشف العالم الجديد (ثواني)")]
    [SerializeField] private float blackHoldTime = 1f;

    [Header("صوت اللمبة")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("صوت إشعال اللمبة (طقّة/أزيز إضاءة)")]
    [SerializeField] private AudioClip lampOnSfx;
    [Tooltip("صوت إطفاء اللمبة")]
    [SerializeField] private AudioClip lampOffSfx;
    [Tooltip("صوت اختفاء الأشياء عند التحوّل إلى الأبيض والأسود")]
    [SerializeField] private AudioClip vanishSfx;
    [Tooltip("فلتر صدى يُفعّل في حالة الإطفاء لإعطاء إحساس الصدى البسيط")]
    [SerializeField] private AudioReverbFilter offReverb;

    [Header("الموسيقى")]
    [SerializeField] private AudioSource musicSource;
    [Tooltip("موسيقى حالة الأبيض والأسود (اللمبة مشتعلة)")]
    [SerializeField] private AudioClip onMusic;
    [Tooltip("موسيقى/جو الحالة المظلمة (اللمبة مطفأة)")]
    [SerializeField] private AudioClip offMusic;

    [Header("البداية")]
    [Tooltip("تبدأ اللعبة واللمبة مطفأة")]
    [SerializeField] private bool startOff = true;

    [Header("اختبار (في المحرر)")]
    [Tooltip("زر لوحة مفاتيح لتبديل اللمبة أثناء التجربة (None لتعطيله)")]
    [SerializeField] private Key testToggleKey = Key.F;

    [Header("أحداث")]
    public UnityEvent onLampOn;
    public UnityEvent onLampOff;

    private bool isOn;
    private Coroutine blinkRoutine;

    /// <summary>هل اللمبة مشتعلة الآن؟</summary>
    public bool IsOn => isOn;

    private void Start()
    {
        if (blackScreen != null) blackScreen.alpha = 0f;
        // ضبط الحالة الابتدائية فورًا وبلا وميض ولا أصوات
        ApplyVisualState(!startOff, bwInstant: true, silent: true);
    }

    private void Update()
    {
        if (testToggleKey != Key.None && Keyboard.current != null &&
            Keyboard.current[testToggleKey].wasPressedThisFrame)
            Toggle();
    }

    /// <summary>يبدّل حالة اللمبة (مع الوميض والأصوات).</summary>
    public void Toggle() => SetLamp(!isOn);

    /// <summary>يضبط حالة اللمبة صراحةً (مع الوميض والأصوات).</summary>
    public void SetLamp(bool on)
    {
        // صوت الطقّة فورًا عند لمس المفتاح
        PlaySfx(on ? lampOnSfx : lampOffSfx);
        if (offReverb != null) offReverb.enabled = !on;

        if (blackScreen != null && isActiveAndEnabled)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(BlinkThenApply(on));
        }
        else
        {
            ApplyVisualState(on, bwInstant: false, silent: false);
        }
    }

    private IEnumerator BlinkThenApply(bool on)
    {
        // 1) تعتيم الشاشة إلى الأسود
        yield return FadeScreen(1f, blinkFadeTime);

        // 2) أثناء السواد: بدّل العالم فعليًا (لا يراه اللاعب)
        ApplyVisualState(on, bwInstant: true, silent: false);

        // 3) ابقَ على السواد لحظة
        if (blackHoldTime > 0f)
            yield return new WaitForSeconds(blackHoldTime);

        // 4) اكشف العالم الجديد
        yield return FadeScreen(0f, blinkFadeTime);
        blinkRoutine = null;
    }

    private IEnumerator FadeScreen(float target, float duration)
    {
        if (blackScreen == null) yield break;
        float start = blackScreen.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            blackScreen.alpha = Mathf.Lerp(start, target, duration > 0f ? t / duration : 1f);
            yield return null;
        }
        blackScreen.alpha = target;
        blackScreen.blocksRaycasts = target > 0.5f;
    }

    private void ApplyVisualState(bool on, bool bwInstant, bool silent)
    {
        isOn = on;
        bool bw = blackAndWhiteWhenOn && on;

        // اللمبة وأضواء العالم
        if (lampLight != null) lampLight.enabled = on;
        foreach (var l in worldLights)
            if (l != null) l.enabled = on;

        // الأشياء الملوّنة تختفي في الأبيض والأسود
        bool anyHidden = false;
        foreach (var go in hideInBlackAndWhite)
            if (go != null)
            {
                if (bw && go.activeSelf) anyHidden = true;
                go.SetActive(!bw);
            }

        // صوت اختفاء الأشياء
        if (!silent && anyHidden)
            PlaySfx(vanishSfx);

        // تحويل العالم لأبيض وأسود
        if (worldBW != null)
        {
            if (bwInstant) worldBW.SetInstant(bw);
            else worldBW.SetBlackAndWhite(bw);
        }

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

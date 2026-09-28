using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// صوت خطوات اللاعب، في كل سين، بلا ربط.
///
/// الهب ومرحلة التوايلايت والسيرك كانت صامتة تحت قدمي اللاعب، فيبدو أنه <b>يطفو</b>
/// لا يمشي. وستيم وحدها فيها صوت — سكربت علي، مربوطٌ فيها بيده.
///
/// <b>نوعان من الأصوات، ولكلٍّ طريقة:</b>
/// <list type="bullet">
/// <item><b>الخشب والحجر تسجيلا مشيٍ طويلان</b> (١١ و٨ ثوانٍ). يُشغَّلان حلقةً ما دام
/// يمشي، ويخفتان حين يقف — و<b>يبدأ كلّ مشيٍ من نقطةٍ عشوائية</b> في التسجيل. فلا
/// تُسمع نفس الخطوة أوّلًا كل مرّة، ولا يتكرّر شيءٌ لأن التسجيل نفسه خطواتٌ حقيقيّة
/// مختلفة.</item>
/// <item><b>العشب خطوةٌ واحدة</b> (نصف ثانية)، فتُشغَّل خطوةً خطوة كل مترين من المسافة
/// المقطوعة، بطبقةٍ وشدّةٍ مختلفتين كل مرّة.</item>
/// </list>
///
/// <b>السطح:</b> لكل سين أرضه (<see cref="DefaultFor"/>)، ومناطق
/// <see cref="FootstepSurface"/> تغيّره حيث وُضعت — كالعشب على جزيرة السيرك في الهب.
///
/// <b>ولا يعمل حيث يوجد صوتٌ غيره</b> (<c>A_FootSounds</c> في ستيم).
///
/// يُركّب نفسه، والأصوات في <c>Osama/Resources/Footsteps</c> فتدخل البلد يقينًا.
/// </summary>
[DisallowMultipleComponent]
public class FootstepSounds : MonoBehaviour
{
    private const string Folder = "Footsteps/";

    /// <summary>كم يمشي بين خطوتي عشب. طول خطوة إنسانٍ بالغٍ تقريبًا.</summary>
    private const float StepLength = 2.1f;

    /// <summary>أقلّ سرعة تُعدّ مشيًا — دون ذلك انزلاقٌ أو دفعُ جدار.</summary>
    private const float MoveThreshold = 0.6f;

    /// <summary>السرعة التي يُشغَّل عندها التسجيل بطبقته الأصلية.</summary>
    private const float NaturalSpeed = 4.5f;

    private const float LoopVolume = 0.5f;
    private const float StepVolume = 0.6f;
    private const float LandVolume = 0.65f;
    private const float FadeIn = 0.08f;       // يبدأ الصوت مع أوّل خطوة، لا بعدها
    private const float FadeOut = 0.18f;      // ويسكت بلا قطعٍ حادّ حين يقف

    /// <summary>اسم سكربت الخطوات الآخر. حيث وُجد نسكت له.</summary>
    private const string OtherFootsteps = "A_FootSounds";

    private static FootstepSounds instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("FootstepSounds") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<FootstepSounds>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>
    /// أرض السين حيث لا منطقة: <b>خشب</b> في كل مكان.
    ///
    /// في الهب لأن ما خارج الجزر هو الجسور، والجسور خشب — والجزر نفسها مناطق
    /// <see cref="FootstepSurface"/> (حجرٌ لستيم والوسط والسيرك، وعشبٌ للتوايلايت).
    /// وفي التوايلايت والسيرك لأن أرضهما خشب.
    /// </summary>
    private static FootstepSurface.Kind DefaultFor(string scene) => FootstepSurface.Kind.Wood;

    private AudioClip loopWood, loopPlate, stepGrass, land;
    private AudioSource loop;       // الحلقة: خشب أو حجر
    private AudioSource shots;      // الخطوات المفردة والهبوط

    private CharacterController controller;
    private Transform body;
    private Vector3 previous;
    private float travelled;
    private bool wasGrounded = true;
    private bool silenced;
    private bool scanned;
    private float nextScanAt;
    private FootstepSurface.Kind sceneDefault;

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        controller = null;
        body = null;
        scanned = false;
        silenced = false;
        travelled = 0f;
        nextScanAt = 0f;
        sceneDefault = DefaultFor(scene.name);
        if (loop != null) { loop.Stop(); loop.volume = 0f; }
    }

    private void Update()
    {
        if (silenced) return;
        if (!scanned && !Scan()) return;
        if (body == null) { scanned = false; return; }

        bool grounded = controller == null || controller.isGrounded;

        if (grounded && !wasGrounded && land != null)
        {
            Shot(land, LandVolume);
            travelled = 0f;                      // لا يخطو فور هبوطه
        }
        wasGrounded = grounded;

        Vector3 now = body.position;
        Vector3 step = Vector3.ProjectOnPlane(now - previous, Vector3.up);   // الأفقي وحده
        previous = now;

        // السرعة من المسافة المقطوعة لا من الإدخال: تعمل مع أي سكربت حركة، ومع
        // اليد والكيبورد، ومع من يُدفع أو يُسحب
        float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
        bool walking = grounded && speed >= MoveThreshold;

        FootstepSurface.Kind surface = FootstepSurface.TryGet(now, out var zone) ? zone : sceneDefault;
        AudioClip want = surface == FootstepSurface.Kind.Plate ? loopPlate
                       : surface == FootstepSurface.Kind.Wood ? loopWood
                       : null;                                   // العشب خطواتٌ مفردة

        Loop(walking ? want : null, speed);

        if (!walking || want != null) { travelled = 0f; return; }

        // العشب: خطوةٌ كل مترين
        travelled += step.magnitude;
        if (travelled < StepLength) return;

        travelled -= StepLength;
        Shot(stepGrass, StepVolume);
    }

    /// <summary>
    /// يُبقي الحلقة على التسجيل المطلوب، يعلو ويخفت بتدرّج. والطبقة تتبع السرعة قليلًا
    /// فالجري أسرع إيقاعًا من المشي — بحدٍّ ضيّق كي لا يصير الصوت كرتونيًّا.
    /// </summary>
    private void Loop(AudioClip clip, float speed)
    {
        if (loop == null) return;

        if (clip != null && loop.clip != clip)
        {
            loop.clip = clip;
            loop.volume = 0f;
            loop.Stop();
        }

        bool on = clip != null;
        if (on && !loop.isPlaying)
        {
            // من موضعٍ عشوائيّ في التسجيل: كل مشيٍ يبدأ بخطوةٍ غير التي قبلها
            loop.time = Random.Range(0f, Mathf.Max(0f, loop.clip.length - 0.5f));
            loop.Play();
        }

        float target = on ? LoopVolume : 0f;
        float rate = Time.deltaTime / (on ? FadeIn : FadeOut);
        loop.volume = Mathf.MoveTowards(loop.volume, target, rate * LoopVolume);

        if (on) loop.pitch = Mathf.Clamp(speed / NaturalSpeed, 0.9f, 1.12f);
        else if (loop.isPlaying && loop.volume <= 0.001f) loop.Pause();
    }

    /// <summary>
    /// اختلافٌ طفيف في الطبقة والشدّة كل خطوة: الملفّ نفسه بنفس الطبقة مرّتين يُسمع
    /// نقرةً آليّة لا قدمًا.
    /// </summary>
    private void Shot(AudioClip clip, float volume)
    {
        if (clip == null || shots == null) return;

        shots.pitch = Random.Range(0.9f, 1.1f);
        shots.PlayOneShot(clip, volume * Random.Range(0.85f, 1f));
    }

    /// <summary>
    /// يلقى اللاعب والأصوات. المحاولة مرّة في الثانية لا كل إطار: بعض السينات بلا
    /// لاعبٍ أصلًا (القائمة، الانترو).
    /// </summary>
    private bool Scan()
    {
        if (Time.unscaledTime < nextScanAt) return false;
        nextScanAt = Time.unscaledTime + 1f;

        foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (script == null || script.GetType().Name != OtherFootsteps) continue;

            silenced = true;
            return false;
        }

        GameObject player = PlayerLocator.Find("Player");
        if (player == null) return false;

        controller = player.GetComponentInParent<CharacterController>();
        body = controller != null ? controller.transform : player.transform;

        if (!LoadClips()) { silenced = true; return false; }

        if (loop == null)
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            loop.spatialBlend = 0f;   // كاميرا اللعبة بعيدة عن اللاعب، والمجسّم لا يُسمع
            loop.volume = 0f;

            shots = gameObject.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.spatialBlend = 0f;
        }

        sceneDefault = DefaultFor(SceneManager.GetActiveScene().name);
        previous = body.position;
        travelled = 0f;
        wasGrounded = true;
        scanned = true;
        return true;
    }

    private bool LoadClips()
    {
        if (loopWood != null || loopPlate != null || stepGrass != null) return true;

        loopWood = Resources.Load<AudioClip>(Folder + "Loop_Wood");
        loopPlate = Resources.Load<AudioClip>(Folder + "Loop_Plate");
        stepGrass = Resources.Load<AudioClip>(Folder + "Step_Grass");
        land = Resources.Load<AudioClip>(Folder + "Step_Land");

        if (loopWood != null || loopPlate != null || stepGrass != null) return true;

        Debug.LogWarning($"[FootstepSounds] ما لقيت أصوات خطوات في Osama/Resources/{Folder} " +
                         "— لا صوت للخطوات.", this);
        return false;
    }
}

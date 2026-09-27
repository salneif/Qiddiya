using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// صوت خطوات اللاعب، في كل سين، بلا ربط.
///
/// الهب ومرحلة التوايلايت والسيرك كانت صامتة تحت قدمي اللاعب: يمشي على الجسر وعلى
/// الحجر وعلى الخشب فلا يُسمع شيء، فيبدو أنه <b>يطفو</b> لا يمشي. وستيم وحدها فيها
/// صوت — سكربت علي، مربوطٌ فيها بيده.
///
/// <b>الخطوة بالمسافة لا بالزمن.</b> مؤقّتٌ ثابت يُسمع آليًّا: خطواتٌ متساوية مهما
/// كانت سرعة اللاعب، وتستمرّ وهو واقف يدفع جدارًا. وخطوةٌ كل مترين من المسافة
/// المقطوعة فعلًا تتسارع معه وتسكت حين يقف، بلا أن نعرف شيئًا عن سكربت حركته.
///
/// <b>ولا يعمل حيث يوجد صوتٌ غيره</b> (<c>A_FootSounds</c> في ستيم): صوتان لخطوةٍ
/// واحدة أسوأ من لا صوت.
///
/// يُركّب نفسه، بلا كائن في أي مشهد. والأصوات من <c>Osama/Resources/Footsteps</c>
/// فتدخل البلد يقينًا.
/// </summary>
[DisallowMultipleComponent]
public class FootstepSounds : MonoBehaviour
{
    private const string Folder = "Footsteps/";

    /// <summary>كم يمشي بين خطوةٍ وأخرى. طول خطوة إنسانٍ بالغٍ تقريبًا.</summary>
    private const float StepLength = 2.1f;

    /// <summary>أقلّ سرعة تُعدّ مشيًا — دون ذلك انزلاقٌ أو دفعُ جدار.</summary>
    private const float MoveThreshold = 0.6f;

    private const float Volume = 0.55f;
    private const float LandVolume = 0.7f;

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

    private readonly List<AudioClip> steps = new List<AudioClip>();
    private AudioClip land;
    private AudioSource source;

    private CharacterController controller;
    private Transform body;
    private Vector3 previous;
    private float travelled;
    private bool wasGrounded = true;
    private bool silenced;
    private bool scanned;
    private float nextScanAt;

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
    }

    private void Update()
    {
        if (silenced) return;
        if (!scanned && !Scan()) return;
        if (body == null) { scanned = false; return; }

        bool grounded = controller == null || controller.isGrounded;

        // الهبوط: صوتٌ واحد عند لمس الأرض بعد طيران
        if (grounded && !wasGrounded && land != null)
        {
            Play(land, LandVolume);
            travelled = 0f;                      // لا يخطو فور هبوطه
        }

        wasGrounded = grounded;

        Vector3 now = body.position;
        Vector3 step = Vector3.ProjectOnPlane(now - previous, Vector3.up);   // الأفقي وحده
        previous = now;

        if (!grounded) return;

        // السرعة من المسافة المقطوعة لا من الإدخال: تعمل مع أي سكربت حركة، ومع
        // اليد والكيبورد، ومع من يُدفع أو يُسحب
        float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
        if (speed < MoveThreshold) { travelled = 0f; return; }

        travelled += step.magnitude;
        if (travelled < StepLength) return;

        travelled -= StepLength;
        Play(steps[Random.Range(0, steps.Count)], Volume);
    }

    /// <summary>
    /// اختلافٌ طفيف في الطبقة والشدّة كل خطوة.
    ///
    /// الملفّ نفسه بنفس الطبقة مرّتين يُسمع نقرةً آليّة لا قدمًا — والأذن تلتقط
    /// التكرار التامّ قبل أن تلتقط الصوت.
    /// </summary>
    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || source == null) return;

        source.pitch = Random.Range(0.92f, 1.08f);
        source.PlayOneShot(clip, volume * Random.Range(0.85f, 1f));
    }

    /// <summary>
    /// يلقى اللاعب والأصوات. المحاولة مرّة في الثانية لا كل إطار: بعض السينات بلا
    /// لاعبٍ أصلًا (القائمة، الانترو)، والبحث فيها كل إطار ثمنٌ بلا مقابل.
    /// </summary>
    private bool Scan()
    {
        if (Time.unscaledTime < nextScanAt) return false;
        nextScanAt = Time.unscaledTime + 1f;

        // سكربت خطواتٍ آخر في السين: نسكت له
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

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // كاميرا اللعبة بعيدة عن اللاعب، والمجسّم لا يُسمع
        }

        previous = body.position;
        travelled = 0f;
        wasGrounded = true;
        scanned = true;
        return true;
    }

    private bool LoadClips()
    {
        if (steps.Count > 0) return true;

        foreach (AudioClip clip in Resources.LoadAll<AudioClip>(Folder.TrimEnd('/')))
        {
            if (clip == null) continue;

            if (clip.name.Contains("Land")) land = clip;
            else steps.Add(clip);
        }

        if (steps.Count > 0) return true;

        Debug.LogWarning($"[FootstepSounds] ما لقيت أصوات خطوات في Osama/Resources/{Folder} " +
                         "— لا صوت للخطوات.", this);
        return false;
    }
}

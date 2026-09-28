using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// صوت خطوات اللاعب، في كل سين، بلا ربط.
///
/// الهب ومرحلة التوايلايت والسيرك كانت صامتة تحت قدمي اللاعب، فيبدو أنه <b>يطفو</b>
/// لا يمشي. وستيم وحدها فيها صوت — سكربت علي، مربوطٌ فيها بيده.
///
/// <b>صوتٌ واحد: الحجر المبلّط.</b> تسجيل مشيٍ طويل (٨ ثوانٍ) يُشغَّل حلقةً ما دام
/// اللاعب يمشي، ويخفت حين يقف — و<b>يبدأ كلّ مشيٍ من نقطةٍ عشوائية</b> في التسجيل.
/// فلا تُسمع نفس الخطوة أوّلًا كل مرّة، ولا يتكرّر شيءٌ لأن التسجيل نفسه خطواتٌ
/// حقيقيّة مختلفة. وعند الهبوط بعد قفزة صوتٌ واحد.
///
/// <b>ولا يعمل حيث يوجد صوتٌ غيره</b> (<c>A_FootSounds</c> في ستيم).
///
/// يُركّب نفسه، والأصوات في <c>Osama/Resources/Footsteps</c> فتدخل البلد يقينًا.
/// </summary>
[DisallowMultipleComponent]
public class FootstepSounds : MonoBehaviour
{
    private const string Folder = "Footsteps/";

    /// <summary>أقلّ سرعة تُعدّ مشيًا — دون ذلك انزلاقٌ أو دفعُ جدار.</summary>
    private const float MoveThreshold = 0.6f;

    /// <summary>السرعة التي يُشغَّل عندها التسجيل بطبقته الأصلية.</summary>
    private const float NaturalSpeed = 4.5f;

    private const float LoopVolume = 0.5f;
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

    private AudioClip walk, land;
    private AudioSource loop;       // المشي حلقةً
    private AudioSource shots;      // الهبوط

    private CharacterController controller;
    private Transform body;
    private Vector3 previous;
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
        nextScanAt = 0f;
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
            shots.pitch = Random.Range(0.9f, 1.1f);
            shots.PlayOneShot(land, LandVolume * Random.Range(0.85f, 1f));
        }
        wasGrounded = grounded;

        Vector3 now = body.position;
        Vector3 step = Vector3.ProjectOnPlane(now - previous, Vector3.up);   // الأفقي وحده
        previous = now;

        // السرعة من المسافة المقطوعة لا من الإدخال: تعمل مع أي سكربت حركة، ومع
        // اليد والكيبورد، ومع من يُدفع أو يُسحب
        float speed = Time.deltaTime > 0f ? step.magnitude / Time.deltaTime : 0f;
        Walk(grounded && speed >= MoveThreshold, speed);
    }

    /// <summary>
    /// يعلو التسجيل ويخفت بتدرّج. والطبقة تتبع السرعة قليلًا فالجري أسرع إيقاعًا من
    /// المشي — بحدٍّ ضيّق كي لا يصير الصوت كرتونيًّا.
    /// </summary>
    private void Walk(bool on, float speed)
    {
        if (loop == null || walk == null) return;

        if (on && !loop.isPlaying)
        {
            // من موضعٍ عشوائيّ في التسجيل: كل مشيٍ يبدأ بخطوةٍ غير التي قبلها
            loop.time = Random.Range(0f, Mathf.Max(0f, walk.length - 0.5f));
            loop.Play();
        }

        float target = on ? LoopVolume : 0f;
        float rate = Time.deltaTime / (on ? FadeIn : FadeOut);
        loop.volume = Mathf.MoveTowards(loop.volume, target, rate * LoopVolume);

        if (on) loop.pitch = Mathf.Clamp(speed / NaturalSpeed, 0.9f, 1.12f);
        else if (loop.isPlaying && loop.volume <= 0.001f) loop.Pause();
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

        if (walk == null)
        {
            walk = Resources.Load<AudioClip>(Folder + "Loop_Plate");
            land = Resources.Load<AudioClip>(Folder + "Step_Land");
        }

        if (walk == null)
        {
            Debug.LogWarning($"[FootstepSounds] ما لقيت Loop_Plate في Osama/Resources/{Folder} " +
                             "— لا صوت للخطوات.", this);
            silenced = true;
            return false;
        }

        if (loop == null)
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            loop.spatialBlend = 0f;   // كاميرا اللعبة بعيدة عن اللاعب، والمجسّم لا يُسمع
            loop.volume = 0f;
            loop.clip = walk;

            shots = gameObject.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.spatialBlend = 0f;
        }

        previous = body.position;
        wasGrounded = true;
        scanned = true;
        return true;
    }
}

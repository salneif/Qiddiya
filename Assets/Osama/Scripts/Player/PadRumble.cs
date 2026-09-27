using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// اهتزاز يد التحكّم: ضربةٌ عند الموت، ونبضةٌ عند العلم، ودمدمةٌ تعلو عند الاقتراب
/// من بوابة.
///
/// السقوط في اللعبة كان صامتًا في اليد — يموت اللاعب فلا يحسّ شيئًا، والشاشة وحدها
/// تخبره. وضربةٌ قصيرة في يده تُشعره بالخطأ قبل أن يقرأه.
///
/// <b>والخلط في مكان واحد.</b> المصادر نوعان: <b>ضربةٌ</b> لها مدّة تنتهي، و<b>حَملٌ
/// مستمرّ</b> يُكتب كل إطار ويسكت إن كفّ كاتبه. ولو كتب كلٌّ منهما على المحرّكات
/// مباشرةً لتصارعا — بوابةٌ تُمسك اليد على دمدمةٍ خفيفة تمحو ضربة الموت، أو ضربةٌ
/// تُنهي دمدمة البوابة وهي لا تزال أمامك. فيكتبان هنا، وهذا يأخذ <b>الأقوى</b> ويكتبه
/// على المحرّكات مرّة واحدة في الإطار.
///
/// <b>والاهتزاز خطرٌ إن نُسي</b>: محرّكات اليد تدور حتى يوقفها أحد، ولا يوقفها تغييرُ
/// مشهد ولا إيقافُ اللعبة ولا حتى الخروج من وضع التشغيل في المحرر — تبقى اليد ترتجف
/// في يد صاحبها. فكلُّ طريقٍ للخروج هنا يمرّ على الإطفاء.
///
/// يُركّب نفسه، بلا كائن في أي مشهد ولا ربط.
/// </summary>
[DisallowMultipleComponent]
public class PadRumble : MonoBehaviour
{
    /// <summary>
    /// كم يبقى الحَمل المستمرّ حيًّا بعد آخر كتابةٍ له.
    ///
    /// إطارٌ واحد لا يكفي: من يكتبه في <c>Update</c> قد يتأخّر إطارًا عن هذا السكربت
    /// فينقطع الاهتزاز ويعود كل إطارين. وعُشر ثانية أطول من أي تأخّرٍ معقول، وأقصر من
    /// أن يبقى الاهتزاز بعد أن يبتعد اللاعب.
    /// </summary>
    private const float HoldGrace = 0.1f;

    /// <summary>
    /// مُعامل عامّ لكل الاهتزازات.
    ///
    /// الشدّات مكتوبةٌ بنسبٍ بعضها إلى بعض — الموت أقوى من نقطة الحفظ، والبوابة بينهما —
    /// فخفضها واحدةً واحدةً يُضيّع تلك النسب. وهذا يخفضها كلّها ويُبقيها.
    /// </summary>
    private const float Strength = 0.55f;

    private static PadRumble instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("PadRumble") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<PadRumble>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // ---------- الواجهة ----------

    /// <summary>
    /// ضربة.
    ///
    /// <paramref name="low"/> المحرّك الثقيل — دمدمةٌ تُحسّ في الكفّ، للضربات.
    /// <paramref name="high"/> الخفيف — طقطقةٌ حادّة، للتنبيهات الصغيرة.
    /// </summary>
    public static void Play(float low, float high, float seconds)
    {
        if (!Ready()) return;

        instance.Shot(Mathf.Clamp01(low), Mathf.Clamp01(high), Mathf.Max(0f, seconds));
    }

    /// <summary>ضربةُ الموت.</summary>
    public static void Hit() => Play(0.85f, 0.45f, 0.32f);

    /// <summary>نقرةٌ خفيفة — نقطة حفظ، أو تأكيدٌ صغير.</summary>
    public static void Tick() => Play(0f, 0.35f, 0.09f);

    /// <summary>التقاط العلم: طقطقةٌ ثم دمدمةٌ قصيرة — يُحسّ أنه أخذ شيئًا له وزن.</summary>
    public static void Pickup()
    {
        if (!Ready()) return;

        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.PickupBeat());
    }

    /// <summary>
    /// شيءٌ يُفتح: دمدمةٌ تعلو ثم تُفلت — حاجزٌ ينزاح، بوابةٌ تنفتح.
    ///
    /// الصعود هو الفرق: الضربة تقول «حدث شيء»، والصعود يقول «شيءٌ يفتح الآن»،
    /// فيرفع اللاعب رأسه قبل أن ينتهي.
    /// </summary>
    public static void Open(float seconds = 0.8f)
    {
        if (!Ready()) return;

        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.Swell(Mathf.Max(0.05f, seconds)));
    }

    /// <summary>
    /// ضربةٌ في المكان: يُحسّها من كان قريبًا، ويخفّ أثرها كلّما بعد.
    ///
    /// وبلا المسافة كانت فأسٌ في الطرف الآخر من المرحلة تهزّ يد اللاعب كأنها فوق رأسه.
    /// </summary>
    public static void At(Vector3 position, float low, float high, float seconds, float range)
    {
        float near = Nearness(position, range);
        if (near > 0f) Play(low * near, high * near, seconds);
    }

    /// <summary>
    /// اهتزازٌ مستمرّ: <b>يُكتب كل إطار</b> ما دام سببه قائمًا، ويسكت وحده بعد
    /// عُشر ثانية من آخر كتابة. لِما يشتدّ ويخفّ باستمرار — كالاقتراب من بوابة.
    /// </summary>
    public static void Hold(float low, float high)
    {
        if (!Ready()) return;

        instance.holdLow = Mathf.Clamp01(low);
        instance.holdHigh = Mathf.Clamp01(high);
        instance.holdUntil = Time.unscaledTime + HoldGrace;
    }

    public static void Stop()
    {
        if (instance == null) return;

        instance.StopAllCoroutines();
        instance.Silence();
    }

    /// <summary>
    /// قوّة القرب من نقطة، تربيعيًّا — قويّةٌ عن قرب وتذوب بسرعة مع البعد.
    /// صفر إن لم يكن هناك لاعبٌ أو يدٌ أو كان خارج المدى.
    /// </summary>
    public static float Nearness(Vector3 position, float range)
    {
        if (!Ready() || range <= 0f) return 0f;

        Transform player = instance.Player();
        if (player == null) return 0f;

        float distance = Vector3.Distance(player.position, position);
        if (distance >= range) return 0f;

        float near = 1f - distance / range;
        return near * near;
    }

    /// <summary>
    /// هل يُهتزّ أصلًا؟ يدٌ موصولة، وهي التي يلعب بها.
    ///
    /// من يلعب بالكيبورد ويدُه على الطاولة لا يريدها تزحف عليها كلّما مات. ومن
    /// وصلها للتوّ سيضغط عليها، فيصله ما بعدها.
    /// </summary>
    private static bool Ready() =>
        instance != null && Gamepad.current != null && InputScheme.UsingGamepad;

    // ---------- الخلط ----------

    private float shotLow, shotHigh, shotLeft;
    private float holdLow, holdHigh, holdUntil;
    private bool humming;
    private Transform player;

    private void Shot(float low, float high, float seconds)
    {
        if (seconds <= 0f) return;

        shotLow = low;
        shotHigh = high;
        shotLeft = seconds;
    }

    /// <summary>
    /// اللعبة قد تكون موقوفة حين يقع الموت (لوحة تحذير، قائمة إيقاف)، والزمن الموقوف
    /// لا يُنقص عدّادًا يقرأ <c>deltaTime</c> — فتبقى اليد ترتجف حتى يرفع أحدٌ الإيقاف.
    /// </summary>
    private void Update()
    {
        if (shotLeft > 0f)
        {
            shotLeft -= Time.unscaledDeltaTime;
            if (shotLeft <= 0f) shotLow = shotHigh = 0f;
        }

        bool held = Time.unscaledTime <= holdUntil;

        // الأقوى يفوز لا المجموع: الجمع يشبع المحرّكين عند أول تراكب فيصير كل شيء
        // بنفس الشدّة، وتضيع الفروق التي من أجلها فُرّق بينها
        float low = Mathf.Max(shotLow, held ? holdLow : 0f) * Strength;
        float high = Mathf.Max(shotHigh, held ? holdHigh : 0f) * Strength;

        Write(low, high);
    }

    private void Write(float low, float high)
    {
        var pad = Gamepad.current;
        if (pad == null) { humming = false; return; }

        if (low <= 0.001f && high <= 0.001f)
        {
            if (!humming) return;       // ساكنةٌ أصلًا — لا نكتب صفرًا كل إطار
            Silence();
            return;
        }

        pad.SetMotorSpeeds(low, high);
        humming = true;
    }

    private void Silence()
    {
        shotLow = shotHigh = shotLeft = 0f;
        holdLow = holdHigh = holdUntil = 0f;
        humming = false;

        // ResetHaptics لا SetMotorSpeeds(0,0) وحدها: الأولى تُسكت كل يدٍ موصولة
        // وتُنسي النظام ما كان مضبوطًا، والثانية تترك يدًا فُصلت ثم عادت على آخر ما سمعت
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        InputSystem.ResetHaptics();
    }

    // ---------- الأشكال ----------

    /// <summary>
    /// نبضتان لا واحدة: الأولى حادّة كأنّ شيئًا أُمسك، والثانية ثقيلةٌ تتلاشى كأنّ له
    /// وزنًا. والفاصل بينهما بزمنٍ غير متأثّر بالتوقّف، فالتقاطٌ أثناء مشهدٍ موقوف
    /// لا يُعلّق النبضة الأولى.
    /// </summary>
    private IEnumerator PickupBeat()
    {
        Shot(0.15f, 0.55f, 0.07f);
        yield return new WaitForSecondsRealtime(0.07f);

        Shot(0.6f, 0.2f, 0.22f);
    }

    /// <summary>دمدمةٌ تعلو ثلاثة أرباع المدّة ثم تُفلت في الربع الأخير.</summary>
    private IEnumerator Swell(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / seconds);
            float strength = k < 0.75f ? k / 0.75f : 1f - (k - 0.75f) / 0.25f;

            // كضربةٍ تُكتب كل إطار: تنتهي وحدها إن توقّف هذا الكوروتين لأي سبب
            Shot(0.55f * strength, 0.2f * strength, 0.06f);
            yield return null;
        }
    }

    // ---------- الربط ----------

    private void OnEnable()
    {
        PlayerKillable.Died += Hit;
        Checkpoint.Activated += OnCheckpoint;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        PlayerKillable.Died -= Hit;
        Checkpoint.Activated -= OnCheckpoint;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Silence();
    }

    private static void OnCheckpoint(Checkpoint point) => Tick();

    /// <summary>سينٌ جديد يبدأ ساكنًا — لا نُورّث اهتزازًا من الذي قبله.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;      // لاعب السين السابق ذهب معه
        StopAllCoroutines();
        Silence();
    }

    private void OnApplicationQuit() => Silence();

    private void OnApplicationPause(bool paused)
    {
        if (paused) Silence();
    }

    /// <summary>اللاعب، يُبحث عنه مرّة لكل سين.</summary>
    private Transform Player()
    {
        if (player != null) return player;

        GameObject go = PlayerLocator.Find("Player");
        if (go == null) return null;

        var controller = go.GetComponentInParent<CharacterController>();
        player = controller != null ? controller.transform : go.transform;
        return player;
    }
}

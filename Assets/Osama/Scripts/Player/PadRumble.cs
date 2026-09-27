using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// اهتزاز يد التحكّم: ضربةٌ عند الموت، ونقرةٌ خفيفة عند نقطة الحفظ.
///
/// السقوط في اللعبة صامتٌ في اليد — يموت اللاعب فلا يحسّ شيئًا، والشاشة وحدها تخبره.
/// وضربةٌ قصيرة في يده تُشعره بالخطأ قبل أن يقرأه.
///
/// <b>والاهتزاز خطرٌ إن نُسي</b>: محرّكات اليد تدور حتى يوقفها أحد، ولا يوقفها تغييرُ
/// مشهد ولا إيقافُ اللعبة ولا حتى الخروج من وضع التشغيل في المحرر — تبقى اليد ترتجف
/// في يد صاحبها. فكلُّ طريقٍ للخروج هنا يمرّ على الإطفاء: انتهاء المدّة، وتحميل سين،
/// وإيقاف اللعبة، والخروج، وتعطيل السكربت نفسه.
///
/// يُركّب نفسه، بلا كائن في أي مشهد ولا ربط.
/// </summary>
[DisallowMultipleComponent]
public class PadRumble : MonoBehaviour
{
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

    /// <summary>
    /// يهزّ اليد.
    ///
    /// <paramref name="low"/> المحرّك الثقيل — دمدمةٌ تُحسّ في الكفّ، للضربات.
    /// <paramref name="high"/> الخفيف — طقطقةٌ حادّة، للتنبيهات الصغيرة.
    /// </summary>
    public static void Play(float low, float high, float seconds)
    {
        if (instance == null) return;      // ما زال المحرك يُقلع

        instance.Begin(Mathf.Clamp01(low), Mathf.Clamp01(high), Mathf.Max(0f, seconds));
    }

    /// <summary>ضربةُ الموت.</summary>
    public static void Hit() => Play(0.85f, 0.45f, 0.32f);

    /// <summary>نقرةٌ خفيفة — نقطة حفظ، أو تأكيدٌ صغير.</summary>
    public static void Tick() => Play(0f, 0.35f, 0.09f);

    public static void Stop()
    {
        if (instance != null) instance.End();
    }

    private float remaining;
    private bool running;

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
        End();
    }

    private static void OnCheckpoint(Checkpoint point) => Tick();

    /// <summary>سينٌ جديد يبدأ ساكنًا — لا نُورّث اهتزازًا من الذي قبله.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => End();

    private void OnApplicationQuit() => End();

    private void OnApplicationPause(bool paused)
    {
        if (paused) End();
    }

    /// <summary>
    /// اللعبة قد تكون موقوفة حين يقع الموت (لوحة تحذير، قائمة إيقاف)، والزمن الموقوف
    /// لا يُنقص عدّادًا يقرأ <c>deltaTime</c> — فتبقى اليد ترتجف حتى يرفع أحدٌ الإيقاف.
    /// </summary>
    private void Update()
    {
        if (!running) return;

        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f) End();
    }

    private void Begin(float low, float high, float seconds)
    {
        var pad = Gamepad.current;
        if (pad == null || seconds <= 0f) return;

        pad.SetMotorSpeeds(low, high);
        remaining = seconds;
        running = true;
    }

    private void End()
    {
        running = false;
        remaining = 0f;

        // ResetHaptics لا SetMotorSpeeds(0,0): الأولى تُسكت كل يدٍ موصولة وتُنسي
        // النظام ما كان مضبوطًا، والثانية تترك يدًا فُصلت ثم عادت على آخر ما سمعت
        if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        InputSystem.ResetHaptics();
    }
}

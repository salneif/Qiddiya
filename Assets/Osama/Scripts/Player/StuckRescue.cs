using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// مخرجٌ للاعب إذا علق: يقتل نفسه في مكانه فيعود من آخر نقطة حفظ.
///
/// في ستيم تاون يعلق اللاعب أحيانًا وهو منحنٍ بعد الروبوت — لا يقف ولا يقفز —
/// فلا يبقى أمامه إلا إعادة اللعبة من أوّلها. والموت رجوعٌ إلى نقطة حفظ، أي خسارةٌ
/// بثوانٍ بدل خسارة الجلسة.
///
/// <b>التركيبة مقصودة الصعوبة:</b> <c>مثلث</c> مضغوطًا ثم <c>Options</c>. لا تقع
/// صدفةً في لعبٍ عادي — مثلث وحده لا يفعل شيئًا، وOptions وحده يفتح الإيقاف كما
/// كان — ولا تحتاج قائمةً ولا زرًّا في الواجهة. وعلى الكيبورد: <c>Ctrl</c> مضغوطًا
/// ثم <c>K</c>.
///
/// ويُعطّل الإيقاف في تلك الضغطة وحدها: <c>Options</c> جزءٌ من التركيبة، فلولا ذلك
/// لمات اللاعب وفُتحت له القائمة في اللحظة نفسها.
///
/// والقتل عبر <c>PlayerKillable</c> إن وُجد — وهو نظام الموت في ستيم والسيرك —
/// وإلّا بحثنا عن دالّة موتٍ على اللاعب بالاسم. فإن لم نجد شيئًا قلناها في الكونسول
/// ولم نفعل شيئًا: مخرجٌ لا يعمل خيرٌ من مخرجٍ يكسر شيئًا.
///
/// يُركّب نفسه، بلا كائن في أي مشهد.
/// </summary>
[DefaultExecutionOrder(-200)]   // يضبط SuppressPause قبل أن يقرأه PauseMenuFix في الإطار نفسه
[DisallowMultipleComponent]
public class StuckRescue : MonoBehaviour
{
    /// <summary>أسماء دوالّ الموت التي نجرّبها حين لا يوجد <c>PlayerKillable</c>.</summary>
    private static readonly string[] DeathMethods = { "Kill", "Die", "PlayerDeath", "KillPlayer" };

    private static StuckRescue instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("StuckRescue") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<StuckRescue>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        SuppressPause = false;
    }

    /// <summary>
    /// حقيقيٌّ في الإطار الذي تُنفَّذ فيه التركيبة. يقرأه <c>PauseMenuFix</c> فيتجاهل
    /// ضغطة <c>Options</c> تلك وحدها.
    /// </summary>
    public static bool SuppressPause { get; private set; }

    private void LateUpdate()
    {
        // يُصفَّر في آخر الإطار لا في أوّله: من يقرأه قد يسبقنا أو يتأخّر عنّا
        SuppressPause = false;

        if (!Combo()) return;

        SuppressPause = true;
        Rescue();
    }

    private static bool Combo()
    {
        var pad = Gamepad.current;
        if (pad != null && pad.buttonNorth.isPressed && pad.startButton.wasPressedThisFrame)
            return true;

        var keyboard = Keyboard.current;
        return keyboard != null &&
               (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) &&
               keyboard.kKey.wasPressedThisFrame;
    }

    private void Rescue()
    {
        GameObject player = PlayerLocator.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("[StuckRescue] ما لقيت اللاعب — ما صار شيء.", this);
            return;
        }

        var killable = player.GetComponentInParent<PlayerKillable>();
        if (killable != null)
        {
            if (killable.IsDead) return;   // ميّتٌ أصلًا: لا نقتله مرّتين

            Debug.Log("[StuckRescue] أنقذنا اللاعب — يرجع من آخر نقطة حفظ.", this);
            killable.Kill();
            return;
        }

        if (Reflected(player)) return;

        Debug.LogWarning("[StuckRescue] هذا السين بلا نظام موت نعرفه — ما صار شيء. " +
                         "لو تكرّر التعليق هنا، يحتاج نقطة حفظ أو PlayerKillable.", this);
    }

    /// <summary>
    /// سينٌ بنظام موتٍ آخر: نبحث عن دالّة موتٍ بلا معاملات على سكربتات اللاعب.
    ///
    /// بالاسم لا بمرجع، فلا نُترجَم مع سكربت أحد ولا ينكسر بناؤه إن حُذف.
    /// </summary>
    private bool Reflected(GameObject player)
    {
        foreach (MonoBehaviour script in player.GetComponentsInParent<MonoBehaviour>(true))
        {
            if (script == null || script == this) continue;

            foreach (string name in DeathMethods)
            {
                MethodInfo method = script.GetType().GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, System.Type.EmptyTypes, null);

                if (method == null || method.ReturnType != typeof(void)) continue;

                try
                {
                    method.Invoke(script, null);
                    Debug.Log($"[StuckRescue] أنقذنا اللاعب عبر " +
                              $"{script.GetType().Name}.{name}().", this);
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[StuckRescue] {script.GetType().Name}.{name}() رفض: " +
                                     $"{e.Message}", this);
                }
            }
        }

        return false;
    }
}

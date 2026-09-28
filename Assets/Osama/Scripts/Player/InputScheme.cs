using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// بأيّ شيء يلعب اللاعب <b>الآن</b>: كيبورد أم يد تحكّم.
///
/// اللعبة تقبل الاثنين، فالتلميحات لا تصلح ثابتة: من يُمسك اليد ويقرأ «اضغط E» لا
/// يجد E، ومن على الكيبورد ويرى رمز المربّع لا يعرفه. والجواب ليس سؤالًا في البداية
/// بل مراقبةً مستمرّة — يبدّل اللاعب الجهاز في منتصف اللعب، فتبدّل الصور معه.
///
/// والحكم <b>بضغطةٍ حقيقية</b> لا بـ<c>lastUpdateTime</c>: الأخير يتحرّك مع أي ضجيج
/// في اليد — وعصا مائلة بشعرة تُحدِّث الجهاز كل إطار — فكانت الصور تتذبذب بين
/// الاثنين ويد التحكّم موضوعة على الطاولة.
///
/// يُركّب نفسه، فلا كائن في أي مشهد.
/// </summary>
[DisallowMultipleComponent]
// قبل كل شيء في الإطار: من يسأل «يدٌ أم كيبورد؟» يجب أن يسمع جواب هذا الإطار لا
// الذي قبله. بلا هذا، ESC على الكيبورد بعد لمسةٍ لليد كان يقرأه MenuManager بحالة
// «يد» القديمة فيُعيد تحديد الزرّ الذي خرج منه اللاعب — فيولع
[DefaultExecutionOrder(-1000)]
public class InputScheme : MonoBehaviour
{
    /// <summary>ميلٌ يُقصد به التحريك — أقلّ منه انحرافُ عصا لا يدٌ تلعب.</summary>
    private const float StickWake = 0.5f;

    /// <summary>وحركةُ فأرةٍ دون هذا اهتزازُ طاولة لا يدٌ عليها.</summary>
    private const float MouseWake = 8f;

    private static InputScheme instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;

        var host = new GameObject("InputScheme") { hideFlags = HideFlags.HideInHierarchy };
        instance = host.AddComponent<InputScheme>();
        DontDestroyOnLoad(host);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        UsingGamepad = false;
        Changed = null;
    }

    /// <summary>
    /// هل اليد هي التي تقود الآن؟
    ///
    /// يبدأ بالكيبورد لا باليد: من يوصل يدًا سيضغط عليها، ومن لا يملك واحدة لا يرى
    /// رموزًا لا تعنيه.
    /// </summary>
    public static bool UsingGamepad { get; private set; }

    /// <summary>يُطلق لحظة التبديل — لمن يحتاج أن يرسم شيئًا مرّة لا كل إطار.</summary>
    public static event System.Action Changed;

    private void Update()
    {
        if (Gamepad.current != null && PadAwake()) Set(true);
        else if (KeyboardAwake()) Set(false);
    }

    private static void Set(bool pad)
    {
        if (UsingGamepad == pad) return;

        UsingGamepad = pad;
        Changed?.Invoke();
    }

    /// <summary>ضغطةُ زرّ، أو عصا مالت قصدًا.</summary>
    private static bool PadAwake()
    {
        var pad = Gamepad.current;

        if (pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
            pad.buttonNorth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
            pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
            pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
            pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
            pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame)
            return true;

        return pad.leftStick.ReadValue().magnitude > StickWake ||
               pad.rightStick.ReadValue().magnitude > StickWake;
    }

    /// <summary>أي مفتاح، أو نقرةُ فأرة، أو تحريكها تحريكًا يُقصد.</summary>
    private static bool KeyboardAwake()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse == null) return false;

        return mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
               mouse.delta.ReadValue().magnitude > MouseWake;
    }
}

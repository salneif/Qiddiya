using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// قراءة زر التفاعل من لوحة المفاتيح <b>ويد التحكّم معًا</b>، في مكان واحد.
///
/// كانت كل سكربتات التفاعل تقرأ <c>Keyboard.current</c> مباشرة — تشتغل على
/// الكيبورد وحده، وتغيير زر التفاعل يعني تعديل ستة ملفات. هذا يجمعها:
/// السكربتات تسأل هنا، والإجابة واحدة للجميع.
///
/// الزر: <c>E</c> على لوحة المفاتيح، و<b>مربّع</b> على يد التحكّم.
///
/// ⚠️ خريطة <c>Player.inputactions</c> تربط <c>Interact</c> بـ<c>buttonNorth</c>
/// (مثلث) لا بـ<c>buttonWest</c> (مربّع). المستعمل هنا هو المربّع كما طُلب —
/// فليُوحَّد الاثنان، وإلا صار زرّان لفعل واحد.
///
/// ولا نقرأ من ملف الخريطة نفسه عمدًا: قراءته تحتاج <c>PlayerInput</c> مربوطًا في
/// كل سين وتسمية ثابتة للحدث، وأي تغيير في اسم الخريطة يكسر كل شيء بصمت. القراءة
/// من الجهاز مباشرة تعمل في أي سين بلا ربط، وتبقى مطابقة ما دام الزر هو الزر.
/// </summary>
public static class InteractInput
{
    /// <summary>
    /// زرّ التفاعل في يد التحكّم: الغربي — <b>مربّع</b> على بلايستيشن، X على إكس بوكس.
    /// </summary>
    private static ButtonControl PadInteract => Gamepad.current?.buttonWest;

    /// <summary>هل ضُغط زر التفاعل هذا الإطار؟ يشمل يد التحكّم.</summary>
    public static bool Pressed(Key key)
    {
        if (key != Key.None && Keyboard.current != null &&
            Keyboard.current[key].wasPressedThisFrame) return true;

        return PadInteract != null && PadInteract.wasPressedThisFrame;
    }

    /// <summary>
    /// زر التفاعل في يد التحكّم وحده، بلا الكيبورد.
    ///
    /// لِـ<c>LegacyInteractBridge</c>: سكربتات غيرنا تقرأ الكيبورد بنفسها، فمن قرأ
    /// الاثنين هناك شغّل التفاعل مرّتين في ضغطة واحدة.
    /// </summary>
    public static bool PadPressed => PadInteract != null && PadInteract.wasPressedThisFrame;

    /// <summary>هل زر التفاعل مضغوط الآن؟ للأشياء التي تُمسك لا تُنقر.</summary>
    public static bool Held(Key key)
    {
        if (key != Key.None && Keyboard.current != null &&
            Keyboard.current[key].isPressed) return true;

        return PadInteract != null && PadInteract.isPressed;
    }

    /// <summary>
    /// محور أفقي من زرّين، أو من العصا اليسرى ليد التحكّم.
    ///
    /// العصا تتقدّم على الأزرار: من يمسك اليد لا يلمس الكيبورد، ولو جمعناهما
    /// لتنازعا حين يكون أحدهما في المنتصف.
    /// </summary>
    public static float Horizontal(Key left, Key right)
    {
        var stick = Gamepad.current?.leftStick;
        if (stick != null)
        {
            float x = stick.ReadValue().x;
            if (Mathf.Abs(x) > 0.2f) return Mathf.Clamp(x, -1f, 1f);   // منطقة ميتة
        }

        if (Keyboard.current == null) return 0f;

        float dir = 0f;
        if (left != Key.None && Keyboard.current[left].isPressed) dir -= 1f;
        if (right != Key.None && Keyboard.current[right].isPressed) dir += 1f;
        return dir;
    }

    /// <summary>
    /// أي زرّ في يد التحكّم — لِما يُغلق بـ«اضغط أي زر» لا بزرّ معيّن.
    ///
    /// أزرارٌ معدودة لا <c>allControls</c>: الأخيرة تمرّ على كل محاور اليد وعصيّها
    /// في كل إطار، وتُحسب العصا حركةً فتُغلق اللوحة بمجرّد أن يلمس اللاعب العصا.
    /// </summary>
    public static bool AnyPadButton
    {
        get
        {
            var pad = Gamepad.current;
            if (pad == null) return false;

            return pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                   pad.buttonNorth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                   pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                   pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
        }
    }

    /// <summary>
    /// أي زرّ في يد التحكّم <b>مضغوط الآن</b> — لِما يُمسك لا لِما يُنقر.
    ///
    /// الأزرار وحدها بلا العصيّ والزنادات: <c>HoldToSkip</c> يعدّ الثواني وهو مضغوط،
    /// ولمسةٌ عابرة للعصا ما ينبغي أن تُحسب إمساكًا.
    /// </summary>
    public static bool AnyPadButtonHeld
    {
        get
        {
            var pad = Gamepad.current;
            if (pad == null) return false;

            return pad.buttonSouth.isPressed || pad.buttonWest.isPressed ||
                   pad.buttonNorth.isPressed || pad.buttonEast.isPressed ||
                   pad.startButton.isPressed || pad.selectButton.isPressed ||
                   pad.leftShoulder.isPressed || pad.rightShoulder.isPressed;
        }
    }

    /// <summary>هل اللاعب يستعمل يد التحكّم الآن؟ لاختيار أيقونة التلميح المناسبة.</summary>
    public static bool UsingGamepad =>
        Gamepad.current != null && Gamepad.current.lastUpdateTime >
        (Keyboard.current != null ? Keyboard.current.lastUpdateTime : 0d);
}

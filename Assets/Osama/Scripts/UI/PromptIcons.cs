using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// أيقونة الزرّ المناسبة للجهاز الذي يلعب به اللاعب الآن.
///
/// التلميحات في العالم كانت تحمّل <c>"Key" + الزر</c> مرّة في <c>Awake</c> وتثبت
/// عليها — فمن بيده يد التحكّم يقرأ طول اللعبة «اضغط E» وما عنده E. وهنا يُسأل
/// <c>InputScheme</c> كل مرّة، فتتبدّل الصورة في نفس اللحظة التي يبدّل فيها جهازه.
///
/// الأيقونات في <c>Osama/Resources</c>: <c>KeyE</c> و<c>KeyA</c> و<c>KeyD</c> للكيبورد،
/// و<c>PadE</c> و<c>PadA</c> و<c>PadD</c> لليد — ومقاسها وطرازها واحد فلا يقفز شيء
/// في المشهد حين تتبدّل.
///
/// والنتيجة محفوظة: <c>Resources.Load</c> في كل إطار على كل تلميح ثمنٌ بلا مقابل.
/// </summary>
public static class PromptIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    /// <summary>أيقونة هذا الزرّ لجهاز اللاعب الحالي.</summary>
    public static Sprite For(Key key) => For(key, InputScheme.UsingGamepad);

    /// <summary>
    /// تكبير علامات اليد: رسمة زرّ اليد فيها فراغٌ حوله أكثر من رسمة الكيبورد، فكانت تبدو
    /// أصغر بكثير بنفس الحجم (أسامة: "الهنت صغير حق المربع").
    /// </summary>
    public const float PadBoost = 1.7f;

    /// <summary>ما يُضرب فيه حجم العلامة الآن حسب الجهاز.</summary>
    public static float Scale => InputScheme.UsingGamepad ? PadBoost : 1f;

    /// <summary>
    /// ومثلها لجهازٍ بعينه.
    ///
    /// وإن لم توجد صورة اليد رجعنا لصورة الكيبورد: تلميحٌ بزرٍّ غير الذي في يده خيرٌ
    /// من لا تلميح — والأول يُلاحَظ فيُصلَح، والثاني يمرّ صامتًا.
    /// </summary>
    public static Sprite For(Key key, bool gamepad)
    {
        if (key == Key.None) return null;

        if (gamepad)
        {
            Sprite pad = Load("Pad" + key);
            if (pad != null) return pad;
        }

        return Load("Key" + key);
    }

    private static Sprite Load(string name)
    {
        if (cache.TryGetValue(name, out Sprite found)) return found;

        found = Resources.Load<Sprite>(name);
        cache[name] = found;           // الغياب يُحفظ أيضًا فلا نعيد البحث عنه كل إطار
        return found;
    }
}

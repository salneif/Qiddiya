using System.Reflection;
using UnityEngine;

/// <summary>
/// لمن يضيف أزرارًا إلى لوحةٍ وقت التشغيل (Continue في القائمة، Respawn وRestart في الإيقاف).
///
/// <see cref="UIPanel"/> يلتقط صورة كل زرّ مرّةً في Awake — موضعه وحجمه — ويعيدها كلّما فُتحت اللوحة
/// أو أُغلقت. فزرٌّ نُقل بعدها يرجع لموضعه القديم عند أوّل رجوعٍ من الإعدادات، فيقع فوق الزرّ الجديد،
/// والجديد لا صورة له فيعلق منتفخًا إن أُغلقت اللوحة وهو مُبرَز. هنا:
/// <see cref="Settle"/> قبل التعديل (كل زرٍّ في حاله الطبيعية)، ثم <see cref="Recapture"/> بعده
/// (صورةٌ جديدة تشمل الجديد والمنقول).
/// </summary>
internal static class MenuBaseline
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly FieldInfo Captured = typeof(UIPanel).GetField("baselineCaptured", Any);
    private static readonly FieldInfo First = typeof(UIPanel).GetField("firstSelected", Any);

    /// <summary>كل لوحةٍ فوق هذا الكائن تعيد أزرارها لحالها الطبيعية. يرجع اللوحات لتُمرَّر لـ<see cref="Recapture"/>.</summary>
    internal static UIPanel[] Settle(Transform inside)
    {
        UIPanel[] panels = inside != null ? inside.GetComponentsInParent<UIPanel>(true) : new UIPanel[0];
        foreach (UIPanel panel in panels) if (panel != null) panel.ResetVisualStates();
        return panels;
    }

    /// <summary>صورةٌ جديدة لكل لوحة: المواضع الجديدة هي الطبيعية الآن.</summary>
    internal static void Recapture(UIPanel[] panels)
    {
        if (Captured == null || panels == null) return;
        foreach (UIPanel panel in panels)
        {
            if (panel == null) continue;
            Captured.SetValue(panel, false);
            panel.CaptureBaseline();
        }
    }

    /// <summary>أوّل زرٍّ يُحدَّد حين تُفتح اللوحة (يد التحكّم).</summary>
    internal static void SetFirst(UIPanel panel, GameObject first)
    {
        if (panel != null && First != null && First.FieldType == typeof(GameObject)) First.SetValue(panel, first);
    }
}

using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// لوحة تشخيص لنظام الأبيض/الأسود والدوائر الملوّنة — تطبع الحالة الحقيقية على
/// الشاشة وقت اللعب، فتعرف أي حلقة مكسورة بدل التخمين.
///
/// الاستخدام: حُطّها على أي كائن في المشهد → Play → اقرأ اللوحة في نافذة <b>Game</b>
/// (مو Scene). احذفها بعد ما تخلص.
///
/// كيف تقرأ النتيجة:
///  • _WorldBWAmount = 0  → WorldBWController ما يرسل القيمة (غير موجود/مطفي/الوضع غلط)
///  • _InteractorCount = 0 → InteractorManager ما يشوف أي WorldInteractor فعّال
///  • الاثنان سليمان والشاشة ملوّنة → المشكلة في الـ Full Screen Pass أو الماتيريال
/// </summary>
public class WorldBWDebugOverlay : MonoBehaviour
{
    [Tooltip("إظهار اللوحة")]
    [SerializeField] private bool show = true;
    [SerializeField] private int fontSize = 16;

    private static readonly int BWAmountId = Shader.PropertyToID("_WorldBWAmount");
    private static readonly int CountId = Shader.PropertyToID("_InteractorCount");

    private GUIStyle style;

    private void OnGUI()
    {
        if (!show) return;

        style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true,
            normal = { textColor = Color.white }
        };

        float bwAmount = Shader.GetGlobalFloat(BWAmountId);
        int shaderCount = Shader.GetGlobalInteger(CountId);

        var interactors = FindObjectsByType<WorldInteractor>(FindObjectsSortMode.None);
        var manager = FindFirstObjectByType<InteractorManager>();
        var pipeline = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.defaultRenderPipeline;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<b>=== تشخيص نظام الأبيض/الأسود ===</b>");
        sb.AppendLine(Line("_WorldBWAmount", bwAmount.ToString("0.00"), bwAmount > 0.001f));
        sb.AppendLine(Line("WorldBWController", WorldBWController.Instance != null ? "موجود" : "مفقود",
                           WorldBWController.Instance != null));
        sb.AppendLine(Line("InteractorManager", manager != null ? "موجود" : "مفقود", manager != null));
        sb.AppendLine(Line("_InteractorCount (للشيدر)", shaderCount.ToString(), shaderCount > 0));
        sb.AppendLine(Line("Render Pipeline", pipeline != null ? pipeline.name : "null",
                           pipeline != null));
        sb.AppendLine();
        sb.AppendLine($"<b>WorldInteractor في المشهد: {interactors.Length}</b>");

        foreach (var it in interactors)
        {
            bool ok = it.isActiveAndEnabled && it.Radius > 0.001f;
            string state = it.isActiveAndEnabled ? $"Radius = {it.Radius:0.00}" : "معطّل";
            sb.AppendLine(Line("  " + it.name, state, ok));
        }

        GUI.Box(new Rect(10, 10, 460, 40 + (interactors.Length + 7) * (fontSize + 6)), GUIContent.none);
        GUI.Label(new Rect(20, 20, 440, 2000), sb.ToString(), style);
    }

    private static string Line(string label, string value, bool ok)
    {
        string color = ok ? "#7CFF7C" : "#FF7C7C";
        return $"{label}: <color={color}>{value}</color>";
    }
}

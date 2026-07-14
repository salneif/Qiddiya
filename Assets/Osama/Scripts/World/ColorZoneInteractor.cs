using UnityEngine;

/// <summary>
/// يجعل هذا الكائن (الآيتم) مركز "منطقة ملوّنة" في العالم الأبيض والأسود.
/// يرسل موقعه ونصف قطر التأثير كقيم Global لشيدر الفولسكرين
/// (Osama/BWColorZoneFullscreen) كل إطار، فتتحرك المنطقة الملوّنة معه.
///
/// حُطّه على الآيتم/الفانوس/اللاعب — أي شيء تريد أن يلوّن ما حوله.
/// عند تعطيل الكائن تختفي المنطقة (نصف القطر يصير صفرًا).
/// </summary>
public class ColorZoneInteractor : MonoBehaviour
{
    [Tooltip("نصف قطر المنطقة الملوّنة حول الكائن (متر)")]
    [SerializeField] private float radius = 5f;

    [Tooltip("عرض الحافة الناعمة بين الملوّن والأبيض/الأسود (متر)")]
    [SerializeField] private float softness = 2f;

    private static readonly int PositionId = Shader.PropertyToID("_ZonePosition");
    private static readonly int RadiusId = Shader.PropertyToID("_ZoneRadius");
    private static readonly int SoftnessId = Shader.PropertyToID("_ZoneSoftness");

    /// <summary>نصف قطر المنطقة — عدّله وقت اللعب لو تبي المنطقة تكبر/تصغر.</summary>
    public float Radius
    {
        get => radius;
        set => radius = Mathf.Max(0f, value);
    }

    private void LateUpdate()
    {
        Shader.SetGlobalVector(PositionId, transform.position);
        Shader.SetGlobalFloat(RadiusId, radius);
        Shader.SetGlobalFloat(SoftnessId, softness);
    }

    private void OnDisable()
    {
        // اختفاء المنطقة عند تعطيل/التقاط الآيتم
        Shader.SetGlobalFloat(RadiusId, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, radius + softness);
    }
}

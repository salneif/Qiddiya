using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// منطقةٌ لها صوت خطواتٍ غير صوت السين العامّ — عشبٌ وسط أرضٍ حجرية مثلًا.
///
/// أرضيّات اللعبة ملوّنة بأطلسٍ واحد يجمع العشب والحجر والخشب في صورةٍ واحدة، فلا
/// اسمَ مادّةٍ ولا وسمَ يقول «هذا عشب». والمنطقة هي الجواب الذي لا يحتاج شيئًا من
/// ذلك: دائرةٌ على الأرض، من وقف فيها سُمع صوتها.
///
/// حُطّه على كائنٍ فارغ في وسط المنطقة، واضبط نصف القطر حتى تطابق الدائرة الخضراء
/// في المشهد حوافّ الأرض.
/// </summary>
public class FootstepSurface : MonoBehaviour
{
    public enum Kind { Grass, Wood, Plate }

    [Tooltip("صوت الخطوات داخل هذي المنطقة")]
    [SerializeField] private Kind surface = Kind.Grass;
    [Tooltip("نصف قطر المنطقة أفقيًّا (متر)")]
    [SerializeField] private float radius = 3.6f;
    [Tooltip("كم فوقها وتحتها يُعدّ داخلها — جزيرةٌ فوق جزيرة لا تتداخلان")]
    [SerializeField] private float height = 3f;

    private static readonly List<FootstepSurface> all = new List<FootstepSurface>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    /// <summary>
    /// سطح هذا الموضع إن وقع في منطقةٍ ما. المناطق قليلة — واحدة أو اثنتان في
    /// السين — فالمرور عليها كلّها كل خطوةٍ لا يكلّف شيئًا.
    /// </summary>
    public static bool TryGet(Vector3 position, out Kind kind)
    {
        foreach (FootstepSurface zone in all)
        {
            if (zone == null) continue;

            Vector3 d = position - zone.transform.position;
            if (Mathf.Abs(d.y) > zone.height) continue;
            if (d.x * d.x + d.z * d.z > zone.radius * zone.radius) continue;

            kind = zone.surface;
            return true;
        }

        kind = default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.35f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, radius);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        height = Mathf.Max(0.1f, height);
    }
}

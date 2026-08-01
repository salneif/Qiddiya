using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// منطقة لا تدخلها الفئران — منصّة مرتفعة، ممر حجري، ماء، أو أي حدّ فنّي تريده.
///
/// عكس <see cref="SafeZone"/>: لا تضيء ولا تحمي من بقية الأعداء ولا يراها اللاعب.
/// وظيفتها الوحيدة رسم حدود السرب، فيمر اللاعب فوقها بحرية بلا أي مانع فيزيائي.
///
/// التركيب: كائن فيه Collider (Is Trigger) بأي شكل — Box لممر، Sphere لبقعة —
/// وهذا السكربت. الشكل والدوران يُحترمان لأن الفحص يستخدم الكولايدر نفسه.
///
/// الارتفاع يُتجاهَل عمدًا (المقارنة أفقية) لأن الفئران تلتصق بالأرض،
/// فالمنطقة تعمل كأسطوانة ممتدة رأسيًا.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RatBlocker : MonoBehaviour
{
    private static readonly List<RatBlocker> blockers = new List<RatBlocker>();

    [Tooltip("هامش تتوقف عنده الفئران قبل الحدّ (متر) — يمنعها من الالتصاق بالحافة")]
    [SerializeField] private float margin = 0.4f;

    private Collider area;

    /// <summary>مركز المنطقة — تهرب الفئران بعيدًا عنه.</summary>
    public Vector3 Center => area != null ? area.bounds.center : transform.position;

    private void Reset()
    {
        // تريغر حتى لا يصير جدارًا يوقف اللاعب — المنع للفئران فقط
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake() => area = GetComponent<Collider>();

    private void OnEnable() => blockers.Add(this);

    private void OnDisable() => blockers.Remove(this);

    /// <summary>هل هذه النقطة داخل المنطقة (مع الهامش)؟</summary>
    public bool Contains(Vector3 point)
    {
        if (area == null) return false;

        // نسوّي الارتفاع مع مستوى الكولايدر حتى يكون الفحص أفقيًا بحتًا
        Vector3 flat = new Vector3(point.x, area.bounds.center.y, point.z);
        return (area.ClosestPoint(flat) - flat).sqrMagnitude <= margin * margin;
    }

    /// <summary>المنطقة المانعة التي تغطي هذه النقطة، أو null.</summary>
    public static RatBlocker At(Vector3 point)
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            var b = blockers[i];
            if (b != null && b.isActiveAndEnabled && b.Contains(point)) return b;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        var c = area != null ? area : GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

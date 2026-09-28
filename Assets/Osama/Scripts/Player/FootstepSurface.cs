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

    /// <summary>
    /// <b>منطقة</b>: دائرةٌ على الأرض، لما لا كولايدر له يخصّه (جزر الهب).
    /// <b>أرض</b>: كل كولايدرٍ تحت هذا الكائن — لمبنى له أرضيّته كالباك ستيج: يشمل
    /// الأرضيّة والدرج والطابق الأعلى معًا، بلا قياسٍ ولا دائرةٍ تخطئ حدوده.
    /// </summary>
    public enum Mode { Area, Ground }

    [Tooltip("منطقة = دائرة حول هذا الكائن. أرض = أي كولايدر تحته يقف عليه اللاعب")]
    [SerializeField] private Mode mode = Mode.Area;
    [Tooltip("صوت الخطوات هنا")]
    [SerializeField] private Kind surface = Kind.Grass;
    [Tooltip("نصف قطر المنطقة أفقيًّا (متر)")]
    [SerializeField] private float radius = 3.6f;
    [Tooltip("كم فوقها وتحتها يُعدّ داخلها — جزيرةٌ فوق جزيرة لا تتداخلان")]
    [SerializeField] private float height = 3f;

    [Header("الطوابق (لوضع «أرض» فقط)")]
    [Tooltip("ما علا أرضيّة المبنى بأكثر من هذا يأخذ الصوت أدناه — للدرج والطابق الأعلى " +
             "حين يكون المبنى كلّه كولايدرًا واحدًا. صفر = يُطفئه")]
    [SerializeField] private float aboveHeight = 0f;
    [Tooltip("صوت ما فوق الأرضيّة")]
    [SerializeField] private Kind aboveSurface = Kind.Wood;

    /// <summary>
    /// أخفض نقطةٍ وطئها اللاعب هنا — أرضيّة المبنى، تُعرَف بالمشي عليها لا بمحور
    /// المجسّم: المحور قد يكون في وسط المبنى أو تحت أساسه، ولا يدلّ على الأرضيّة.
    /// </summary>
    private float lowest = float.MaxValue;

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
            if (zone == null || zone.mode != Mode.Area) continue;

            Vector3 d = position - zone.transform.position;
            if (Mathf.Abs(d.y) > zone.height) continue;
            if (d.x * d.x + d.z * d.z > zone.radius * zone.radius) continue;

            kind = zone.surface;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary>
    /// سطح الأرض التي يقف عليها — إن كان الكولايدر داخل كائنٍ عليه هذا السكربت
    /// بوضع «أرض».
    /// </summary>
    public static bool TryGetGround(Collider ground, float footY, out Kind kind)
    {
        FootstepSurface owner = ground != null ? ground.GetComponentInParent<FootstepSurface>() : null;
        if (owner != null && owner.mode == Mode.Ground)
        {
            // الباك ستيج مبنى كامل في كولايدرٍ واحد — أرضيّته ودرجه وطابقه الأعلى —
            // فلا فرق بينها إلا الارتفاع. اللاعب يدخل من الأسفل، فأوّل ما يطؤه هو الأرضيّة
            owner.lowest = Mathf.Min(owner.lowest, footY);
            bool above = owner.aboveHeight > 0f && footY > owner.lowest + owner.aboveHeight;

            kind = above ? owner.aboveSurface : owner.surface;
            return true;
        }

        kind = default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (mode != Mode.Area) return;

        Gizmos.color = new Color(0.3f, 1f, 0.35f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, new Vector3(1f, 0.02f, 1f));
        Gizmos.DrawWireSphere(Vector3.zero, radius);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.1f, radius);
        height = Mathf.Max(0.1f, height);
        aboveHeight = Mathf.Max(0f, aboveHeight);
    }
}

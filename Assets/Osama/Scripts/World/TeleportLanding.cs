using UnityEngine;

/// <summary>
/// يضع اللاعب على الأرض بعد أي انتقال، أيًّا كان السكربت الذي نقله.
///
/// نقاط الوصول توضع بالعين فتكون فوق الأرض بقليل دائمًا، فيصل اللاعب معلّقًا ثم
/// يسقط — والسقوط أول ما يفتح التعتيم هو أول ما يراه. وسكربت الانتقال في السيرك
/// (<c>TeleportTent</c>) ملك غيرنا فلا نعدّله.
///
/// فبدل إصلاح كل ناقل على حدة، هذا يراقب اللاعب: قفزةٌ كبيرة في إطار واحد لا
/// تكون مشيًا ولا سقوطًا — تكون نقلًا. فيُنزله على الأرض في نفس الإطار.
///
/// واحد في السين يكفي. حُطّه على أي كائن.
/// </summary>
[DisallowMultipleComponent]
public class TeleportLanding : MonoBehaviour
{
    [Header("الكشف")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("انتقال في إطار واحد يتجاوز هذي المسافة يُعدّ نقلًا. السقوط الطبيعي " +
             "أقلّ من متر في الإطار، فثلاثة أمتار تفرّق بينهما بأمان")]
    [SerializeField] private float jumpDistance = 3f;

    [Header("الهبوط")]
    [Tooltip("يبدأ البحث عن الأرض من هذا الارتفاع فوق نقطة الوصول")]
    [SerializeField] private float rise = 2f;
    [Tooltip("وينزل حتى هذا العمق تحتها")]
    [SerializeField] private float drop = 15f;
    [Tooltip("لا ينزل إلا إن كان معلّقًا بأكثر من هذا — فلا يهتزّ لسنتيمترات")]
    [SerializeField] private float minGap = 0.05f;
    [Tooltip("طبقات الأرض")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private bool log;

    private Transform player;
    private CharacterController controller;
    private Vector3 previous;
    private bool hasPrevious;

    private void LateUpdate()
    {
        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return;

            player = go.transform;
            controller = go.GetComponentInParent<CharacterController>();
            if (controller != null) player = controller.transform;

            previous = player.position;
            hasPrevious = true;
            return;
        }

        Vector3 now = player.position;

        if (hasPrevious && Vector3.Distance(now, previous) > jumpDistance)
            now = Land(now);

        previous = now;
        hasPrevious = true;
    }

    /// <summary>
    /// ينزل بنقطة الوصول إلى الأرض تحتها.
    ///
    /// الحساب من أسفل الكبسولة لا من مركزها: مرجع <c>CharacterController</c> في
    /// منتصفها، فوضع المركز على الأرض يدفن نصف اللاعب فيها.
    ///
    /// ويُطفأ الـ<c>CharacterController</c> لحظة النقل وإلا قاوم تغيير الموضع.
    /// </summary>
    private Vector3 Land(Vector3 arrival)
    {
        Vector3 from = arrival + Vector3.up * rise;
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, rise + drop,
                             groundLayers, QueryTriggerInteraction.Ignore))
            return arrival;

        Vector3 place = hit.point;
        if (controller != null)
        {
            float bottom = controller.center.y - controller.height * 0.5f;
            place += Vector3.up * (controller.skinWidth - bottom);
        }

        if (arrival.y - place.y <= minGap) return arrival;   // واقف على الأرض أصلًا

        bool was = controller != null && controller.enabled;
        if (was) controller.enabled = false;
        player.position = place;
        if (was) controller.enabled = true;

        if (log)
            Debug.Log($"[TeleportLanding] أنزلته {(arrival.y - place.y):0.00} م على " +
                      $"«{hit.collider.name}».", this);

        return place;
    }

    private void OnValidate()
    {
        jumpDistance = Mathf.Max(0.5f, jumpDistance);
        rise = Mathf.Max(0f, rise);
        drop = Mathf.Max(0.5f, drop);
        minGap = Mathf.Max(0f, minGap);
    }
}

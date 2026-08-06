using UnityEngine;

/// <summary>
/// منطقة موسيقى: أول ما يدخلها اللاعب تتبدّل موسيقى المكان بمزج متقاطع
/// عبر <see cref="MusicDirector"/>.
///
/// حُطّ واحدة لكل منطقة (باك ستيج / سيرك / هَب). لا تحتاج مصادر صوت ولا إعدادًا —
/// مقطع واحد وكولايدر يغطي المنطقة.
///
/// لا تتعارض مع موسيقى المطاردة: القائد يضعها في طبقة منفصلة تنخفض تحتها
/// ثم تعود، فلا يتنازع مقطعان على السماعة أبدًا.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MusicZone : MonoBehaviour
{
    [Header("الموسيقى")]
    [Tooltip("مقطع موسيقى هذه المنطقة")]
    [SerializeField] private AudioClip music;

    [Header("الشروط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>
    /// Stay لا Enter: لو بدأ اللاعب اللعبة واقفًا داخل المنطقة (أو انتقل إليها
    /// بـ TP أو ريسبون بلا عبور الحافة)، تشتغل موسيقاها بأي حال.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (music == null || !other.CompareTag(playerTag)) return;
        if (MusicDirector.Instance == null) return;

        // القائد يتجاهل إعادة نفس المقطع، فالنداء المتكرر هنا بلا تكلفة
        MusicDirector.Instance.PlayArea(music);
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

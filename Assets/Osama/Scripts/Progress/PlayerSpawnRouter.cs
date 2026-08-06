using System;
using UnityEngine;

/// <summary>
/// يضع اللاعب عند نقطة الظهور المناسبة حسب السين الذي جاء منه.
///
/// بدونه يرجع اللاعب دائمًا لموضعه المحفوظ في سين الهب، فيظهر في وسط الهب
/// بعد كل مرحلة بدل أن يخرج من باب المرحلة التي أنهاها.
///
/// حُطّه على كائن فارغ واحد في الهب، واملأ الجدول:
///   From Scene = "Full_Steam"  →  Point = كائن فارغ أمام باب ستيم
///   From Scene = "Twilight"    →  Point = كائن فارغ أمام باب التوايلايت
/// و<see cref="defaultPoint"/> لأول دخول (قادم من القائمة الرئيسية).
///
/// يضبط أيضًا نقطة بعث اللاعب على نفس المكان، فأول موتة لا ترجّعه لبداية السين.
/// </summary>
[DefaultExecutionOrder(-100)] // قبل FlagCarry حتى يلتصق العلم باللاعب في مكانه النهائي
public class PlayerSpawnRouter : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("اسم السين الذي جاء منه اللاعب")]
        public string fromScene;
        [Tooltip("نقطة ظهوره عند القدوم من ذاك السين")]
        public Transform point;
    }

    [Header("اللاعب")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("نقاط الظهور")]
    [Tooltip("نقطة لكل سين قادم منه")]
    [SerializeField] private Entry[] entries;
    [Tooltip("نقطة أول دخول (قادم من القائمة أو من سين غير مذكور). " +
             "اتركها فارغة ليبقى اللاعب في موضعه داخل السين.")]
    [SerializeField] private Transform defaultPoint;

    [Header("نقطة البعث")]
    [Tooltip("يجعل نقطة الظهور هي نقطة البعث أيضًا (يحتاج PlayerKillable على اللاعب)")]
    [SerializeField] private bool alsoSetRespawnPoint = true;

    private void Start()
    {
        var playerGo = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGo == null)
        {
            Debug.LogWarning($"[PlayerSpawnRouter] ما وُجد كائن بوسم \"{playerTag}\".", this);
            return;
        }

        Transform point = PickPoint(GameProgress.Instance.LastScene);
        if (point == null) return;

        Teleport(playerGo.transform, point);

        if (alsoSetRespawnPoint)
        {
            var killable = playerGo.GetComponentInParent<PlayerKillable>();
            if (killable != null) killable.SetRespawnPoint(point);
        }
    }

    private Transform PickPoint(string lastScene)
    {
        if (entries != null && !string.IsNullOrEmpty(lastScene))
        {
            foreach (var e in entries)
            {
                if (e == null || e.point == null) continue;
                if (string.Equals(e.fromScene, lastScene, StringComparison.OrdinalIgnoreCase))
                    return e.point;
            }
        }

        return defaultPoint;
    }

    /// <summary>
    /// نقل آمن: الـ CharacterController يقاوم تغيير الموضع مباشرة فيرجّع اللاعب
    /// لمكانه — نفس ما يفعله PlayerKillable عند البعث.
    /// </summary>
    private static void Teleport(Transform player, Transform point)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.SetPositionAndRotation(point.position, point.rotation);

        if (cc != null) cc.enabled = true;
    }

    private void OnDrawGizmos()
    {
        // تُرسم دائمًا لا عند التحديد فقط — نقاط الظهور يجب أن تُرى وأنت تبني الهب
        if (entries != null)
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            foreach (var e in entries)
                if (e != null && e.point != null) DrawPoint(e.point);
        }

        if (defaultPoint != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            DrawPoint(defaultPoint);
        }
    }

    private static void DrawPoint(Transform t)
    {
        Gizmos.DrawWireSphere(t.position, 0.4f);
        Gizmos.DrawLine(t.position, t.position + t.forward * 1.2f); // اتجاه النظر
    }
}

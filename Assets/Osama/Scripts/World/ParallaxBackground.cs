using UnityEngine;

/// <summary>
/// خلفية طبقات (Parallax) لإحساس العمق في مشهد 2.5D:
/// كل طبقة تتحرك مع الكاميرا بنسبة مختلفة — القريبة تتحرك أقل والبعيدة تتحرك أكثر
/// فتظل مؤطّرة كخلفية. مثالي لخلفية ملاهي (عجلة دوّارة/خيام/أكشاك بعيدة).
/// حُط هذا السكربت على كائن فارغ يجمع طبقات الخلفية.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        [Tooltip("كائن الطبقة (يحوي مجسمات الخلفية)")]
        public Transform layer;

        [Tooltip("0 = قريبة (تثبت مع العالم)، 1 = بعيدة جدًا (تتحرك بالكامل مع الكاميرا)")]
        [Range(0f, 1f)] public float parallaxFactor = 0.5f;
    }

    [Tooltip("كاميرا اللعب — إن تُركت فارغة تُستخدم Camera.main")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("طبقات الخلفية مرتبة من القريبة إلى البعيدة")]
    [SerializeField] private Layer[] layers;

    [Tooltip("Parallax أفقي فقط (مناسب للسايد-سكرولر)")]
    [SerializeField] private bool horizontalOnly = true;

    private Vector3 lastCamPos;

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
            lastCamPos = cameraTransform.position;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        Vector3 delta = cameraTransform.position - lastCamPos;
        if (horizontalOnly) delta.y = 0f;

        foreach (var l in layers)
        {
            if (l.layer == null) continue;
            // البعيدة (factor عالٍ) تتحرك أكثر مع الكاميرا فتبدو ثابتة الإطار
            l.layer.position += new Vector3(delta.x * l.parallaxFactor,
                                            delta.y * l.parallaxFactor, 0f);
        }

        lastCamPos = cameraTransform.position;
    }
}

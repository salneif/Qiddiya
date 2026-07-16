using UnityEngine;

/// <summary>
/// كائن يتفاعل مع العالم (Interactor) — يوضع على أي كائن تريده أن يؤثر في
/// شيدر تغيير العالم (WorldChange Shader Graph).
///
/// يجمعه <see cref="InteractorManager"/> تلقائيًا ويرسل بياناته للشيدر.
/// </summary>
public class WorldInteractor : MonoBehaviour
{
    [Header("الشكل")]
    [Tooltip("نصف قطر التأثير — يكبّر/يصغّر البصمة كلها")]
    public float Radius = 1f;

    [Tooltip("حجم البصمة على كل محور (يتمدد الشكل)")]
    public Vector3 Scale = Vector3.one;

    [Tooltip("دوران البصمة بالدرجات (Euler)")]
    public Vector3 Rotation = Vector3.zero;

    [Header("الصورة")]
    [Tooltip("رقم الشكل داخل الـ Texture 2D Array (0 = أول صورة)")]
    [Range(0, 15)]
    public int TextureIndex = 0;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.15f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(Rotation),
                                      Scale * Mathf.Max(Radius, 0.0001f));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.05f, 1f));
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// اهتزاز كاميرا بسيط لإضافة وزن سينمائي للحظات القوية (تحوّل العالم، الموت...).
/// حُطّه على كائن الكاميرا (أو والدها)، وينادى Shake() من أي حدث (UnityEvent).
///
/// يهتز حول موضعه المحلي الأصلي ثم يرجع بنعومة، فلا يتعارض مع سكربتات
/// متابعة الكاميرا طالما تكتب على كائن أعلى في التسلسل.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("الافتراضي")]
    [Tooltip("قوة الاهتزاز الافتراضية (متر)")]
    [SerializeField] private float defaultMagnitude = 0.3f;
    [Tooltip("مدة الاهتزاز الافتراضية (ثواني)")]
    [SerializeField] private float defaultDuration = 0.5f;
    [Tooltip("سرعة تلاشي الاهتزاز (أكبر = يخبو أسرع)")]
    [SerializeField] private float damping = 1f;

    private Vector3 baseLocalPos;
    private Coroutine routine;

    private void Awake()
    {
        baseLocalPos = transform.localPosition;
    }

    /// <summary>اهتزاز بالقيم الافتراضية — مناسب للربط بـ UnityEvent.</summary>
    public void Shake() => Shake(defaultMagnitude, defaultDuration);

    /// <summary>اهتزاز بقوة ومدة محددتين.</summary>
    public void Shake(float magnitude, float duration)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(DoShake(magnitude, duration));
    }

    private IEnumerator DoShake(float magnitude, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float falloff = Mathf.Pow(1f - Mathf.Clamp01(t / duration), damping);
            Vector2 offset = Random.insideUnitCircle * magnitude * falloff;
            transform.localPosition = baseLocalPos + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }
        transform.localPosition = baseLocalPos;
        routine = null;
    }
}

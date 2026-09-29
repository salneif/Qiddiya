using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// انخفاضةٌ صغيرة في الكاميرا عند السقطة الكبيرة — وزنٌ يُرى.
///
/// <b>لا تلمس موضعًا يقرؤه أحد</b>: كل سين يحرّك كاميرته بسكربته (<c>CameraFollow</c>،
/// <c>TopDownCameraFollow</c>) في <c>LateUpdate</c>، ومنها ما يمشي من موضعه الحاليّ بـ<c>Lerp</c>.
/// و<see cref="CameraShake"/> يكتب الموضع المحلي ثم يعيده لما حفظه في <c>Awake</c> — وعلى كاميرا
/// تتبع اللاعب هذا قفزةٌ إلى مكانٍ قديم. هنا الإزاحة تُضاف لحظة يبدأ رسم الكاميرا
/// (<see cref="RenderPipelineManager.beginCameraRendering"/>) وتُطرح لحظة ينتهي: الصورة وحدها
/// تنخفض، وكل سكربت يجد الكاميرا حيث تركها.
///
/// والزمن حقيقيّ: إيقافٌ في منتصفها لا يُبقي الصورة منخفضة.
/// </summary>
[DisallowMultipleComponent]
public class ChromaJuiceCamera : MonoBehaviour
{
    /// <summary>المدّة كلها: نزولٌ في سُدسها الأول، ثم ارتدادٌ صغير يذوب.</summary>
    private const float Duration = 0.32f;
    private const float Frequency = 3f;

    private Camera target;
    private float depth;
    private float startedAt = -10f;

    private Camera shifted;
    private Vector3 offset;

    /// <summary>انخفاضة بعمق <paramref name="amount"/> م. الأعمق يغلب إن تداخلت اثنتان.</summary>
    public void Dip(float amount)
    {
        Camera cam = Camera.main;
        if (cam == null || amount <= 0f) return;
        if (cam == target && Remaining() >= amount) return;

        target = cam;
        depth = amount;
        startedAt = Time.unscaledTime;
    }

    /// <summary>بلا انخفاضة: السين تغيّر، والكاميرا ليست كاميرته.</summary>
    public void Stop()
    {
        Restore();
        target = null;
        depth = 0f;
    }

    /// <summary>ما بقي من الجارية، بمقياس عمقها.</summary>
    private float Remaining()
    {
        float k = 1f - (Time.unscaledTime - startedAt) / Duration;
        return k > 0f ? depth * k * k : 0f;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Begin;
        RenderPipelineManager.endCameraRendering -= End;
        Stop();
    }

    /// <summary>رسمٌ انقطع قبل نهايته يترك الإزاحة — تُطرح قبل أن يقرأها أحد.</summary>
    private void LateUpdate() => Restore();

    private void Begin(ScriptableRenderContext context, Camera cam)
    {
        if (target == null || cam != target) return;

        Restore();   // لا تتراكم إزاحتان على كاميرا واحدة

        float t = Time.unscaledTime - startedAt;
        if (t >= Duration)
        {
            target = null;
            return;
        }

        float fade = 1f - t / Duration;
        offset = new Vector3(0f, -Mathf.Sin(t * Frequency * 2f * Mathf.PI) * depth * fade * fade, 0f);
        cam.transform.position += offset;
        shifted = cam;
    }

    private void End(ScriptableRenderContext context, Camera cam)
    {
        if (cam == shifted) Restore();
    }

    private void Restore()
    {
        if (shifted == null) return;

        shifted.transform.position -= offset;
        shifted = null;
    }
}

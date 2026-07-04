using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// مؤثر احتراق/تفتّت الموت — يُركّب على كائن اللاعب (أو أي كائن له رندررات).
/// عند الموت يبدّل متيريالات الرندررات مؤقتًا إلى شيدر "Osama/BurnDissolve"
/// ويحرّك التفتّت من 0 (كامل) إلى 1 (متلاشي) مع توهّج ناري على الحافة،
/// وعند العودة يعكس العملية ثم يعيد المتيريالات الأصلية.
///
/// لا يعدّل سكربت اللاعب — يُقاد من PlayerKillable عبر الدوال:
/// <see cref="PlayDeath"/> ثم <see cref="PlayReform"/> (أو <see cref="ResetImmediate"/>).
/// </summary>
public class DeathDissolveEffect : MonoBehaviour
{
    [Header("الرندررات المتأثرة")]
    [Tooltip("رندررات جسم اللاعب. إذا تُركت فارغة تُجمع تلقائيًا من الأبناء.")]
    [SerializeField] private Renderer[] renderers;

    [Header("المتيريال")]
    [Tooltip("متيريال يستخدم شيدر Osama/BurnDissolve. إذا تُرك فارغًا يُنشأ تلقائيًا من الشيدر.")]
    [SerializeField] private Material dissolveMaterial;

    [Header("التوقيت")]
    [Tooltip("مدة الاحتراق/التلاشي عند الموت (ثواني)")]
    [SerializeField] private float dissolveDuration = 1.0f;
    [Tooltip("مدة إعادة التجسّد عند الريسبون (ثواني)")]
    [SerializeField] private float reformDuration = 0.4f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("مظهر النار (يُطبّق على المتيريال المُنشأ تلقائيًا)")]
    [SerializeField] private Color baseColor = new Color(0.02f, 0.02f, 0.02f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color edgeColor = new Color(1f, 0.35f, 0.05f, 1f);
    [SerializeField] private float edgeEmission = 6f;

    private const string ShaderName = "Osama/BurnDissolve";
    private static readonly int DissolveId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeEmissionId = Shader.PropertyToID("_EdgeEmission");

    private Material runtimeMat;
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    private bool swapped;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        runtimeMat = dissolveMaterial != null
            ? new Material(dissolveMaterial)
            : BuildRuntimeMaterial();
    }

    private Material BuildRuntimeMaterial()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[DeathDissolveEffect] لم يُعثر على الشيدر '{ShaderName}'. " +
                             "تأكد أنه موجود، وأضِفه إلى Always Included Shaders للبناء.");
            return null;
        }
        var mat = new Material(shader);
        mat.SetColor(BaseColorId, baseColor);
        mat.SetColor(EdgeColorId, edgeColor);
        mat.SetFloat(EdgeEmissionId, edgeEmission);
        mat.SetFloat(DissolveId, 0f);
        return mat;
    }

    /// <summary>يشغّل احتراق الموت (0 → 1) ويترك الجسم متلاشيًا.</summary>
    public IEnumerator PlayDeath()
    {
        if (runtimeMat == null) yield break;
        SwapToDissolve();
        yield return Animate(0f, 1f, dissolveDuration);
    }

    /// <summary>يعيد التجسّد (1 → 0) ثم يرجّع المتيريالات الأصلية.</summary>
    public IEnumerator PlayReform()
    {
        if (runtimeMat == null || !swapped) yield break;
        yield return Animate(1f, 0f, reformDuration);
        Restore();
    }

    /// <summary>إرجاع فوري للحالة الطبيعية بلا أنيميشن.</summary>
    public void ResetImmediate()
    {
        if (runtimeMat != null) runtimeMat.SetFloat(DissolveId, 0f);
        Restore();
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = curve.Evaluate(duration > 0f ? Mathf.Clamp01(t / duration) : 1f);
            runtimeMat.SetFloat(DissolveId, Mathf.Lerp(from, to, k));
            yield return null;
        }
        runtimeMat.SetFloat(DissolveId, to);
    }

    private void SwapToDissolve()
    {
        if (swapped) return;
        runtimeMat.SetFloat(DissolveId, 0f);
        originalMaterials.Clear();

        foreach (var r in renderers)
        {
            if (r == null) continue;
            originalMaterials[r] = r.sharedMaterials;

            // نستخدم نفس نسخة runtimeMat لكل الخانات كي يؤثر تحريك _DissolveAmount على الجميع
            var replacement = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < replacement.Length; i++)
                replacement[i] = runtimeMat;
            r.sharedMaterials = replacement;
        }
        swapped = true;
    }

    private void Restore()
    {
        if (!swapped) return;
        foreach (var kv in originalMaterials)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
        originalMaterials.Clear();
        swapped = false;
    }
}

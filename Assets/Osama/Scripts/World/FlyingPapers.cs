using UnityEngine;

/// <summary>
/// أوراق/تذاكر تطير في الهواء — تبني نظام جسيمات كامل تلقائيًا عند التشغيل.
/// الأوراق تتطاير بشكل عشوائي مع دوران وتذبذب (Noise) فتبدو محمولة بالريح.
///
/// حُطّه على كائن فارغ في المنطقة التي تريد الأوراق تطير فيها، واضبط حجم المنطقة.
/// يعمل بلا تكستشر (مربعات صغيرة)، ويمكن إعطاؤه ماتيريال أوراق لمظهر أفضل.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FlyingPapers : MonoBehaviour
{
    [Header("المنطقة")]
    [Tooltip("حجم منطقة تطاير الأوراق (متر)")]
    [SerializeField] private Vector3 areaSize = new Vector3(20f, 6f, 10f);

    [Header("الكمية والعمر")]
    [Tooltip("كم ورقة تظهر في الثانية")]
    [SerializeField] private float papersPerSecond = 4f;
    [Tooltip("كم تعيش الورقة (ثواني)")]
    [SerializeField] private Vector2 lifetime = new Vector2(6f, 12f);

    [Header("الحجم والحركة")]
    [Tooltip("حجم الورقة (متر)")]
    [SerializeField] private Vector2 size = new Vector2(0.12f, 0.28f);
    [Tooltip("سرعة الطيران")]
    [SerializeField] private Vector2 speed = new Vector2(0.6f, 2.2f);
    [Tooltip("اتجاه الريح العام")]
    [SerializeField] private Vector3 wind = new Vector3(1.2f, 0.15f, 0f);
    [Tooltip("قوة التذبذب العشوائي (يخلي الطيران غير منتظم)")]
    [SerializeField] private float turbulence = 1.4f;
    [Tooltip("سرعة دوران الورقة حول نفسها (درجة/ثانية)")]
    [SerializeField] private Vector2 spin = new Vector2(-180f, 180f);

    [Header("المظهر")]
    [Tooltip("لون الأوراق (فاتح مغبّر يناسب جو الملاهي)")]
    [SerializeField] private Color paperColor = new Color(0.85f, 0.8f, 0.68f, 1f);
    [Tooltip("ماتيريال الجسيمات — اتركه فارغًا لاستخدام ماتيريال افتراضي بسيط")]
    [SerializeField] private Material paperMaterial;

    private void Reset()
    {
        // يمنع النظام الافتراضي المزعج عند إضافة المكوّن
        var ps = GetComponent<ParticleSystem>();
        ps.Stop();
    }

    private void Start()
    {
        Build();
    }

    private void Build()
    {
        var ps = GetComponent<ParticleSystem>();
        ps.Stop();

        // ---- الإعداد الرئيسي ----
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = paperColor;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.02f;              // ثقل بسيط جدًا
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        // ---- معدل الانبعاث ----
        var emission = ps.emission;
        emission.rateOverTime = papersPerSecond;

        // ---- الشكل: صندوق يغطي المنطقة ----
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = areaSize;

        // ---- الريح ----
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(wind.x);
        vel.y = new ParticleSystem.MinMaxCurve(wind.y);
        vel.z = new ParticleSystem.MinMaxCurve(wind.z);

        // ---- التذبذب (يخلي الطيران عضويًا) ----
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = turbulence;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.4f;
        noise.damping = true;

        // ---- الدوران أثناء الطيران ----
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(spin.x * Mathf.Deg2Rad, spin.y * Mathf.Deg2Rad);
        rot.y = new ParticleSystem.MinMaxCurve(spin.x * Mathf.Deg2Rad, spin.y * Mathf.Deg2Rad);
        rot.z = new ParticleSystem.MinMaxCurve(spin.x * Mathf.Deg2Rad, spin.y * Mathf.Deg2Rad);

        // ---- تلاشٍ عند الظهور والاختفاء ----
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ---- الرندر ----
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.Local; // تميل مع دورانها
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (paperMaterial != null) renderer.material = paperMaterial;

        ps.Play();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, areaSize);
    }
}

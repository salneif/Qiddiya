using UnityEngine;

/// <summary>
/// دخان/بخار — يبني نظام جسيمات كامل تلقائيًا. مناسب لمواسير البخار،
/// فتحات التهوية، أو ضباب أرضي زاحف (غيّر الوضع من <see cref="mode"/>).
///
/// حُطّه على كائن فارغ عند مصدر الدخان (فوهة الماسورة مثلاً).
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class SmokeEmitter : MonoBehaviour
{
    public enum SmokeMode
    {
        [InspectorName("بخار صاعد (ماسورة/فتحة)")] Rising,
        [InspectorName("ضباب أرضي زاحف")] GroundFog
    }

    [Header("النوع")]
    [SerializeField] private SmokeMode mode = SmokeMode.Rising;

    [Header("الكمية والعمر")]
    [Tooltip("كم جسيم في الثانية")]
    [SerializeField] private float rate = 6f;
    [Tooltip("عمر الجسيم (ثواني) — أطول = دخان يبقى أكثر")]
    [SerializeField] private Vector2 lifetime = new Vector2(3f, 6f);

    [Header("الحجم والحركة")]
    [Tooltip("حجم البداية")]
    [SerializeField] private Vector2 startSize = new Vector2(0.6f, 1.4f);
    [Tooltip("كم يتضخم الدخان خلال عمره (مضاعف)")]
    [SerializeField] private float growth = 2.5f;
    [Tooltip("سرعة الخروج")]
    [SerializeField] private Vector2 speed = new Vector2(0.4f, 1.2f);
    [Tooltip("اتجاه انجراف الهواء")]
    [SerializeField] private Vector3 drift = new Vector3(0.3f, 0f, 0f);

    [Header("الشكل")]
    [Tooltip("زاوية انتشار المخروط (للبخار الصاعد)")]
    [SerializeField] private float coneAngle = 18f;
    [Tooltip("نصف قطر المصدر")]
    [SerializeField] private float radius = 0.25f;
    [Tooltip("مساحة الضباب الأرضي (تُستخدم في وضع الضباب)")]
    [SerializeField] private Vector3 fogArea = new Vector3(25f, 0.5f, 15f);

    [Header("المظهر")]
    [Tooltip("لون الدخان")]
    [SerializeField] private Color smokeColor = new Color(0.7f, 0.72f, 0.75f, 0.35f);
    [Tooltip("ماتيريال الجسيمات — اتركه فارغًا للافتراضي")]
    [SerializeField] private Material smokeMaterial;

    private void Start()
    {
        Build();
    }

    private void Build()
    {
        var ps = GetComponent<ParticleSystem>();
        ps.Stop();

        bool fog = mode == SmokeMode.GroundFog;

        // ---- الرئيسي ----
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            fog ? speed.x * 0.3f : speed.x, fog ? speed.y * 0.3f : speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(
            fog ? startSize.x * 3f : startSize.x, fog ? startSize.y * 3f : startSize.y);
        main.startColor = smokeColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = fog ? 0f : -0.02f; // البخار يرتفع قليلاً
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 250;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        // ---- الشكل ----
        var shape = ps.shape;
        shape.enabled = true;
        if (fog)
        {
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = fogArea;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = radius;
        }

        // ---- الانجراف ----
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(drift.x);
        vel.y = new ParticleSystem.MinMaxCurve(drift.y);
        vel.z = new ParticleSystem.MinMaxCurve(drift.z);

        // ---- التضخّم مع العمر ----
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, growth);
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ---- تذبذب خفيف ----
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = fog ? 0.25f : 0.5f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        // ---- تلاشٍ ناعم ----
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0.7f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ---- دوران بطيء يخلي الدخان يتقلب ----
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        // ---- الرندر ----
        var renderer = GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 10f; // يقلل تداخل الرسم مع المجسمات
        if (smokeMaterial != null) renderer.material = smokeMaterial;

        ps.Play();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.75f, 0.8f, 0.35f);
        if (mode == SmokeMode.GroundFog)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, fogArea);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

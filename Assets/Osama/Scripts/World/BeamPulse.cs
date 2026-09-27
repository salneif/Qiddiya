using UnityEngine;

/// <summary>
/// ينبض بشعاعٍ أو بأي مجسّم مضيء — يعلو ويخفت ويتنفّس بدل أن يقف ساكنًا.
///
/// شعاع البوابة يقف ثابتًا فيُقرأ زينةً في المشهد لا بابًا يُدخل منه. والنبض يقلب
/// المعنى: ما ينبض يدعوك، وما يسكن يُهمَل.
///
/// ولا يشترط شيدرًا بعينه: يكتب اللون عبر <c>MaterialPropertyBlock</c> على كل خاصّية
/// لون شائعة (<c>_BaseColor</c> لِـURP، و<c>_Color</c> لِـBuilt-in، و<c>_TintColor</c>
/// للجسيمات) فما يقرأه الشيدر يُقرأ وما لا يقرأه يُهمَل بلا خطأ — ولأنه Property Block
/// فالمادّة المشتركة لا تُمسّ، ولا ينتقل النبض إلى شيء آخر يستعملها.
///
/// <b>والبدء بشرط:</b> <c>Start When Planted</c> يجعله ساكنًا حتى يُزرع علمٌ بعينه —
/// فشعاع السيرك ينام حتى يُزرع علم التوايلايت، ثم ينبض بلا أن يُربط بحدث.
/// </summary>
[DisallowMultipleComponent]
public class BeamPulse : MonoBehaviour
{
    private static readonly int[] ColorIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_TintColor"),
        Shader.PropertyToID("_EmissionColor"),
    };

    [Header("ما ينبض")]
    [Tooltip("اتركه فارغًا ليأخذ كل الـ Renderers في هذا الكائن وأبنائه")]
    [SerializeField] private Renderer[] renderers;
    [Tooltip("ضوء ينبض معها — اختياري")]
    [SerializeField] private Light beamLight;

    [Header("النبض")]
    [Tooltip("نبضة كاملة في كم ثانية")]
    [SerializeField] private float period = 1.8f;
    [Tooltip("كم يخفت في أضعف لحظة. 1 = بلا خفوت، 0.5 = النصف")]
    [Range(0.05f, 1f)] [SerializeField] private float dim = 0.55f;
    [Tooltip("كم يسطع في أقوى لحظة، فوق لونه الأصلي")]
    [Range(1f, 3f)] [SerializeField] private float bright = 1.35f;
    [Tooltip("تمدّدٌ عرضيّ مع النبضة. صفر = بلا تمدّد")]
    [Range(0f, 0.5f)] [SerializeField] private float swell = 0.08f;
    [Tooltip("يمدّد الارتفاع أيضًا. أطفئه للشعاع — الشعاع يعرض ولا يطول")]
    [SerializeField] private bool swellHeight;

    [Header("متى يبدأ")]
    [Tooltip("يبقى ساكنًا حتى يُزرع هذا العلم في الهب. «بدون» = ينبض من البداية")]
    [SerializeField] private FlagId startWhenPlanted = FlagId.None;
    [Tooltip("يبدأ ساكنًا وينتظر Begin() من حدث")]
    [SerializeField] private bool startStopped;
    [Tooltip("مدّة استيقاظه حين يبدأ — فلا يقفز إلى النبض دفعةً")]
    [SerializeField] private float wakeSeconds = 1.2f;

    private MaterialPropertyBlock block;
    private Color[] baseColors;
    private float baseIntensity;
    private Vector3 baseScale;
    private float clock;
    private float wake;
    private bool running;

    private void Awake()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        block = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        if (beamLight != null) baseIntensity = beamLight.intensity;

        // اللون الأصلي مرّة واحدة: لو قرأناه كل إطار لقرأنا ما كتبناه نحن فيتضاعف
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            baseColors[i] = Tint(renderers[i]);

        running = !startStopped && startWhenPlanted == FlagId.None;
        wake = running ? 1f : 0f;
    }

    /// <summary>يبدأ النبض — اربطه بحدث (فتح حاجز، زرع علم) إن أردت.</summary>
    public void Begin() => running = true;

    /// <summary>يوقفه ويعيد الشعاع إلى صورته الأولى.</summary>
    public void Stop()
    {
        running = false;
        clock = 0f;
    }

    private void Update()
    {
        if (!running && startWhenPlanted != FlagId.None &&
            GameProgress.Instance != null && GameProgress.Instance.IsPlanted(startWhenPlanted))
            running = true;

        // الاستيقاظ والنوم بتدرّج: القفز إلى النبض دفعةً يُرى وميضًا لا حياة
        float step = wakeSeconds > 0f ? Time.deltaTime / wakeSeconds : 1f;
        wake = Mathf.MoveTowards(wake, running ? 1f : 0f, step);

        if (wake <= 0.001f)
        {
            Apply(1f, Vector3.one);
            return;
        }

        clock += Time.deltaTime;
        float wave = Mathf.Sin(clock / Mathf.Max(0.05f, period) * Mathf.PI * 2f) * 0.5f + 0.5f;

        float strength = Mathf.Lerp(1f, Mathf.Lerp(dim, bright, wave), wake);
        float grow = 1f + (strength - 1f) * swell / Mathf.Max(0.001f, bright - 1f);

        Apply(strength, new Vector3(grow, swellHeight ? grow : 1f, grow));
    }

    private void Apply(float strength, Vector3 scale)
    {
        transform.localScale = new Vector3(baseScale.x * scale.x,
                                           baseScale.y * scale.y,
                                           baseScale.z * scale.z);

        if (beamLight != null) beamLight.intensity = baseIntensity * strength;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Color color = baseColors[i] * strength;
            color.a = baseColors[i].a;      // الشفافية للمصمّم، والنبض للسطوع

            renderer.GetPropertyBlock(block);
            foreach (int id in ColorIds) block.SetColor(id, color);
            renderer.SetPropertyBlock(block);
        }
    }

    /// <summary>لون المادّة الأصلي، من أول خاصّية يعرفها شيدرها.</summary>
    private static Color Tint(Renderer renderer)
    {
        if (renderer == null) return Color.white;

        Material material = renderer.sharedMaterial;
        if (material == null) return Color.white;

        foreach (int id in ColorIds)
            if (material.HasProperty(id)) return material.GetColor(id);

        return Color.white;
    }

    private void OnDisable()
    {
        // لا نترك الشعاع على آخر لحظةٍ من النبضة
        if (block != null) Apply(1f, Vector3.one);
    }

    private void OnValidate()
    {
        period = Mathf.Max(0.05f, period);
        wakeSeconds = Mathf.Max(0f, wakeSeconds);
    }
}

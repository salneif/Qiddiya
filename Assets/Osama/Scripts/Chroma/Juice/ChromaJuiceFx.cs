using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// جسيمات العصارة: <b>سبعة أنظمة لا غير</b>، لكل مظهرٍ نظامٌ واحد يعيش طول اللعبة، وكل
/// احتفالٍ يُطلق فيه جسيماته بموضعها وسرعتها ولونها.
///
/// فالمخزون هو النظام نفسه: لا كائن يُنشأ ولا يُهدم وقت اللعب، ولا ضبط وحداتٍ مع كل احتفال
/// (وبعضها لا يُغيَّر والنظام يعمل). وسقف كل مظهرٍ <c>maxParticles</c> — احتفالاتٌ تتراكب في
/// لحظة لا تصنع إلا ما يتّسع له. والجسيمات في فضاء العالم، فتبقى حيث وُلدت وإن مشى اللاعب.
///
/// <b>المواد من <c>Osama/Resources/Chroma/Fx</c></b> فشيدرها يدخل البلد يقينًا. ونسختان منها
/// تُصنعان هنا بوجهين (<c>_Cull</c> = Off) — حالةُ رسمٍ لا كلمةٌ مفتاحية، فلا تطلب متغيّرًا من
/// الشيدر لم يُبنَ:
/// <list type="bullet">
/// <item>الحلقة الأرضية: بطاقةٌ أفقية، فلا نراهن على أيّ وجهيها يُرسم.</item>
/// <item>القصاصات والقطرات <b>معتمة</b>: الشاشة تُرمَّد بقراءة العمق، والشفّاف لا يكتب عمقًا
/// فيأخذ لون ما خلفه — قصاصةٌ أمام السماء رماديةٌ ولو كانت في قلب النبضة. والمعتم يكتب عمقه
/// فيُلوَّن بموضعه هو. والقصاصات تتقلّب فيظهر ظهرها.</item>
/// </list>
/// </summary>
public sealed class ChromaJuiceFx
{
    private const string Folder = "Chroma/Fx/";

    /// <summary>حلقة <c>Ring.png</c> على ٠٫٤١ من عرض البطاقة، فالبطاقة ضِعفا نصف القطر وزيادة.</summary>
    private const float RingCard = 2.44f;

    private readonly ParticleSystem dust, ring, flash, drift, spray, blobs, confetti;
    private readonly ParticleSystem[] all;
    private readonly Material ringMaterial, solidMaterial;
    private readonly Mesh blobMesh;

    public ChromaJuiceFx(Transform parent)
    {
        Material soft = Load("FxSoftAlpha");
        Material glow = Load("FxGlowAdd");
        Material sparkle = Load("FxSparkleAdd");
        ringMaterial = TwoSided(Load("FxRingAdd"));
        solidMaterial = TwoSided(Load("FxOrbOpaque"));
        blobMesh = BuildBlob();

        // غبارٌ وحبر: يندفع ثم يقف بالاحتكاك، ويكبر وهو يذوب كالدخان
        dust = Build(parent, "Dust", soft, 240, -0.02f, 3.5f,
                     Curve(0f, 0.45f, 0.35f, 0.9f, 1f, 1.15f), Fade(0f, 0f, 0.12f, 1f, 1f, 0f),
                     sorted: true);

        // تكبر بسرعة ثم تتمهّل — موجةٌ تنتشر على الأرض لا دائرةٌ تُنفخ
        ring = Build(parent, "Ring", ringMaterial, 16, 0f, 0f,
                     new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 2.6f), new Keyframe(1f, 1f, 0f, 0f)),
                     Fade(0f, 1f, 0.4f, 0.7f, 1f, 0f),
                     mode: ParticleSystemRenderMode.HorizontalBillboard);

        flash = Build(parent, "Flash", glow, 12, 0f, 0f,
                      Curve(0f, 0.55f, 0.2f, 1f, 1f, 1.15f), Fade(0f, 1f, 0.25f, 0.6f, 1f, 0f));

        // الشرر نوعان لأن الجاذبية للنظام كله: يطفو صاعدًا، أو يُقذف ويعود
        AnimationCurve twinkle = Curve(0f, 0f, 0.12f, 1f, 0.7f, 0.8f, 1f, 0f);
        Gradient shine = Fade(0f, 1f, 0.75f, 1f, 1f, 0f);
        drift = Build(parent, "Drift", sparkle, 320, -0.03f, 1.6f, twinkle, shine);
        spray = Build(parent, "Spray", sparkle, 320, 0.35f, 0.7f, twinkle, shine);

        // المعتم لا يذوب بالشفافية، فيظهر بالتكبير ويختفي بالتصغير
        blobs = Build(parent, "Blobs", solidMaterial, 180, 0.55f, 0.45f,
                      Curve(0f, 0f, 0.08f, 1f, 0.8f, 1f, 1f, 0f),
                      mode: ParticleSystemRenderMode.Mesh, mesh: blobMesh);
        confetti = Build(parent, "Confetti", solidMaterial, 420, 0.12f, 1.4f,
                         Curve(0f, 0.4f, 0.06f, 1f, 0.85f, 1f, 1f, 0f),
                         facing: ParticleSystemRenderSpace.World, tumble: true);

        all = new[] { dust, ring, flash, drift, spray, blobs, confetti };
    }

    // ---------- الإطلاق ----------

    /// <summary>نفخة غبارٍ أو حبر: تندفع بسرعتها ثم تقف، وتكبر وهي تذوب.</summary>
    public void Dust(Vector3 at, Vector3 velocity, float size, float life, Color color) =>
        Emit(dust, at, velocity, size, life, color, Random.Range(-45f, 45f));

    /// <summary>حلقةٌ مستوية على الأرض تتّسع حتى نصف القطر <paramref name="radius"/> (م) ثم تذوب.</summary>
    public void Ring(Vector3 at, float radius, float life, Color color) =>
        Emit(ring, at, Vector3.zero, radius * RingCard, life, color, 0f);

    /// <summary>ومضة ضوءٍ تكبر وتنطفئ — الضربة في أوّل الاحتفال.</summary>
    public void Flash(Vector3 at, float size, float life, Color color) =>
        Emit(flash, at, Vector3.zero, size, life, color, 0f);

    /// <summary>شرارةٌ تطفو: تندفع ثم تتباطأ وتصعد قليلًا — للأعمدة والانفجارات.</summary>
    public void Drift(Vector3 at, Vector3 velocity, float size, float life, Color color) =>
        Emit(drift, at, velocity, size, life, color, Spin());

    /// <summary>شرارةٌ تُقذف بقوسٍ وتعود — للنوافير.</summary>
    public void Spray(Vector3 at, Vector3 velocity, float size, float life, Color color) =>
        Emit(spray, at, velocity, size, life, color, Spin());

    /// <summary>قطرة لونٍ معتمة: تُلوَّن بموضعها داخل النبضة وإن كانت أمام السماء.</summary>
    public void Blob(Vector3 at, Vector3 velocity, float size, float life, Color color) =>
        Emit(blobs, at, velocity, size, life, color, 0f);

    /// <summary>قصاصة ورقٍ معتمة، <paramref name="size"/> عرضها وطولها، تتقلّب وترفرف وهي تسقط.</summary>
    public void Confetti(Vector3 at, Vector3 velocity, Vector2 size, float life, Color color)
    {
        if (confetti == null) return;

        var p = new ParticleSystem.EmitParams
        {
            position = at,
            velocity = velocity,
            startSize3D = new Vector3(size.x, size.y, 1f),
            startLifetime = life,
            startColor = color,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            angularVelocity3D = new Vector3(Random.Range(-420f, 420f), Random.Range(-420f, 420f),
                                            Random.Range(-240f, 240f)),
        };
        confetti.Emit(p, 1);
    }

    /// <summary>يمحو كل جسيمٍ حيّ — سينٌ جديد لا يرث دخان الذي قبله.</summary>
    public void Clear()
    {
        foreach (ParticleSystem ps in all)
            if (ps != null) ps.Clear();
    }

    /// <summary>النسختان والكرة صُنعت بالكود، فلا يهدمها غيرنا.</summary>
    public void Dispose()
    {
        if (ringMaterial != null) Object.Destroy(ringMaterial);
        if (solidMaterial != null) Object.Destroy(solidMaterial);
        if (blobMesh != null) Object.Destroy(blobMesh);
    }

    private static void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, float life,
                             Color color, float spin)
    {
        if (ps == null) return;

        var p = new ParticleSystem.EmitParams
        {
            position = at,
            velocity = velocity,
            startSize = size,
            startLifetime = life,
            startColor = color,
            rotation = Random.Range(0f, 360f),
            angularVelocity = spin,
        };
        ps.Emit(p, 1);
    }

    /// <summary>النجمة تدور فتلمع — لكلٍّ اتجاهه وسرعته.</summary>
    private static float Spin() => Random.Range(90f, 240f) * (Random.value < 0.5f ? -1f : 1f);

    // ---------- البناء ----------

    /// <summary>
    /// نظامٌ لا يُطلق وحده ولا شكل له: كل جسيمٍ يأتي بموضعه وسرعته من <c>Emit</c>. ويبقى
    /// يعمل بلا انبعاث، فلا يُسأل هل يُحاكي نظامٌ متوقّف ما أُطلق فيه.
    /// </summary>
    private static ParticleSystem Build(Transform parent, string name, Material material, int max,
                                        float gravity, float drag, AnimationCurve size, Gradient fade = null,
                                        ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard,
                                        ParticleSystemRenderSpace facing = ParticleSystemRenderSpace.View,
                                        bool sorted = false, bool tumble = false, Mesh mesh = null)
    {
        if (material == null) return null;

        // يُضبط مطفأً: نظامٌ جديد يستيقظ بإعداداته الافتراضية (مخروطٌ يرشّ بلا مادّة)
        var go = new GameObject("Juice" + name) { hideFlags = HideFlags.HideInHierarchy };
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.gravityModifier = gravity;
        main.startRotation3D = tumble;
        main.startSize3D = tumble;
        // جسيماته موزّعةٌ على العالم: نظامٌ «خارج الشاشة» يُجمَّد بالافتراضي، فتقف نفخةٌ في
        // الهواء حتى تعود الكاميرا
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false;

        if (drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule air = ps.limitVelocityOverLifetime;
            air.enabled = true;
            air.limit = 1000f;   // بلا سقفٍ للسرعة — الاحتكاك وحده
            air.dampen = 0f;
            air.drag = drag;
            air.multiplyDragByParticleSize = false;
            air.multiplyDragByParticleVelocity = false;
        }

        ParticleSystem.SizeOverLifetimeModule grow = ps.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, size);

        if (fade != null)
        {
            ParticleSystem.ColorOverLifetimeModule tint = ps.colorOverLifetime;
            tint.enabled = true;
            tint.color = new ParticleSystem.MinMaxGradient(fade);
        }

        if (tumble)
        {
            // ترفرف وهي تسقط، فلا تنزل خطوطًا مستقيمة
            ParticleSystem.NoiseModule flutter = ps.noise;
            flutter.enabled = true;
            flutter.strength = 0.6f;
            flutter.frequency = 0.5f;
            flutter.scrollSpeed = 0.4f;
            flutter.damping = true;
            flutter.quality = ParticleSystemNoiseQuality.Medium;
        }

        var look = go.GetComponent<ParticleSystemRenderer>();
        look.renderMode = mode;
        look.alignment = facing;
        look.sharedMaterial = material;
        look.sortMode = sorted ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
        // الافتراضي نصف الشاشة: حلقةٌ بعرض عشرين مترًا تتوقّف عن الكبر قرب الكاميرا
        look.maxParticleSize = 10f;
        look.shadowCastingMode = ShadowCastingMode.Off;
        look.receiveShadows = false;
        look.lightProbeUsage = LightProbeUsage.Off;
        look.reflectionProbeUsage = ReflectionProbeUsage.Off;
        if (mesh != null)
        {
            look.mesh = mesh;
            // متغيّرات الـ instancing تُحذف من البلد (Strip Unused، والمادّة بلا Enable GPU
            // Instancing) — والرسم العادي لا يحتاجها
            look.enableGPUInstancing = false;
        }

        go.SetActive(true);
        ps.Play();
        return ps;
    }

    private static Material Load(string name)
    {
        var material = Resources.Load<Material>(Folder + name);
        if (material == null)
            Debug.LogWarning($"[ChromaJuice] ما لقيت Osama/Resources/{Folder}{name} — مؤثّراتها لن تظهر.");
        return material;
    }

    /// <summary>نسخةٌ بوجهين من مادّةٍ مشتركة — الأصل لا يُمسّ، فغيرنا يستعمله بوجهٍ واحد.</summary>
    private static Material TwoSided(Material source)
    {
        if (source == null) return null;

        var copy = new Material(source) { name = source.name + " (Juice, two-sided)" };
        copy.SetFloat("_Cull", (float)CullMode.Off);
        return copy;
    }

    /// <summary>منحنى من أزواج (زمن، قيمة).</summary>
    private static AnimationCurve Curve(params float[] pairs)
    {
        var keys = new Keyframe[pairs.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1]);
        return new AnimationCurve(keys);
    }

    /// <summary>شفافيةٌ من أزواج (زمن، شفافية) على أبيض — اللون يأتي مع كل جسيم.</summary>
    private static Gradient Fade(params float[] pairs)
    {
        var alphas = new GradientAlphaKey[pairs.Length / 2];
        for (int i = 0; i < alphas.Length; i++) alphas[i] = new GradientAlphaKey(pairs[i * 2 + 1], pairs[i * 2]);

        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         alphas);
        return gradient;
    }

    /// <summary>
    /// كرةٌ صغيرة للقطرات. شيدرٌ بلا إضاءة يرسمها قرصًا مصمتًا من أي جهة — والقرص المعتم لا
    /// يُصنع من بطاقة إلا بقصّ الشفافية، وتلك كلمةٌ مفتاحية لم تُبنَ في البلد. والوجوه للخارج:
    /// اتجاهها يُكتب في الإطار المسبق الذي يقرأ منه الـ SSAO.
    /// </summary>
    private static Mesh BuildBlob()
    {
        const int Around = 10, Rings = 6;

        var vertices = new Vector3[(Around + 1) * (Rings + 1)];
        for (int y = 0; y <= Rings; y++)
        {
            float polar = Mathf.PI * y / Rings;
            for (int x = 0; x <= Around; x++)
            {
                float azimuth = 2f * Mathf.PI * x / Around;
                vertices[y * (Around + 1) + x] = 0.5f * new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth),
                                                                    Mathf.Cos(polar),
                                                                    Mathf.Sin(polar) * Mathf.Sin(azimuth));
            }
        }

        var triangles = new int[Around * Rings * 6];
        int t = 0;
        for (int y = 0; y < Rings; y++)
        {
            for (int x = 0; x < Around; x++)
            {
                int a = y * (Around + 1) + x, b = a + Around + 1;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        var mesh = new Mesh { name = "JuiceBlob", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

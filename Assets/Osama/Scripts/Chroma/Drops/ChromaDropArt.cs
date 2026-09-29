using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// شكل القطرات ومؤثّراتها في مكانٍ واحد: كرةٌ صغيرة، وبطاقةٌ تواجه الكاميرا للهالة
/// والحلقة، ونظاما جسيماتٍ يتشاركهما كل السين (بريقٌ وحلقات التقاط).
///
/// <b>الشيدر Unlit، والكرة المصمتة فيه تُرى قرصًا مسطّحًا.</b> فالضوء مخبوزٌ في ألوان
/// رؤوس الكرة — أعلاها أفتح من أسفلها — والشيدر يضرب لون الرأس في <c>_BaseColor</c>،
/// فيبقى التلوين بلونٍ واحد عبر <see cref="MaterialPropertyBlock"/> وتبقى الكرة كرة.
///
/// المواد من <c>Osama/Resources/Chroma/Fx</c> لا من <c>Shader.Find</c>: شيدرٌ لا تمسكه
/// مادّةٌ في البناء يُحذف منه، فيُرسم ورديًّا في البلد.
/// </summary>
public static class ChromaDropArt
{
    private const string Folder = "Chroma/Fx/";

    public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private static Material orb, glow, ring, sparkle, face;
    private static bool faceLooked;
    private static Mesh ball, card;
    private static MaterialPropertyBlock block;
    private static ParticleSystem.EmitParams emit;
    private static bool warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        orb = glow = ring = sparkle = face = null;
        faceLooked = false;
        ball = card = null;
        block = null;
        emit = new ParticleSystem.EmitParams();
        warned = false;
    }

    /// <summary>هل المواد كلها موجودة؟ بلا واحدةٍ منها لا قطرات — خيرٌ من كراتٍ وردية.</summary>
    public static bool Ready
    {
        get
        {
            if (orb == null) orb = Resources.Load<Material>(Folder + "FxOrbOpaque");
            if (glow == null) glow = Resources.Load<Material>(Folder + "FxGlowAdd");
            if (ring == null) ring = Resources.Load<Material>(Folder + "FxRingAdd");
            if (sparkle == null) sparkle = Resources.Load<Material>(Folder + "FxSparkleAdd");

            bool ok = orb != null && glow != null && ring != null && sparkle != null;
            if (!ok && !warned)
            {
                warned = true;
                Debug.LogWarning($"[ChromaDropArt] مادّةٌ ناقصة في Osama/Resources/{Folder} — لا قطرات.");
            }
            return ok;
        }
    }

    public static Material Orb => orb;

    /// <summary>
    /// <b>وجه الولد</b> — القطرة عملةٌ بوجهه تلفّ حول نفسها (طلب أسامة: "الفيس هو النقاط").
    /// إن غابت مادّته رجعت القطرة كرةً كما كانت.
    /// </summary>
    public static Material Face
    {
        get
        {
            if (!faceLooked) { faceLooked = true; face = Resources.Load<Material>("Chroma/Face/FxFaceCoin"); }
            return face;
        }
    }
    public static Material Glow => glow;
    public static Material Ring => ring;

    /// <summary>كرةٌ قطرها متر، ضوؤها في ألوان رؤوسها.</summary>
    public static Mesh Ball
    {
        get
        {
            if (ball == null) ball = BuildBall();
            return ball;
        }
    }

    /// <summary>بطاقةٌ مربّعة ضلعها متر، وجهها نحو −Z: تواجه الكاميرا إن أخذت دورانها.</summary>
    public static Mesh Card
    {
        get
        {
            if (card == null) card = BuildCard();
            return card;
        }
    }

    /// <summary>
    /// يلوّن رسّامًا بلا لمس مادّته المشتركة. كتلةٌ واحدة للجميع: يونيتي ينسخ قيمها
    /// لحظة <c>SetPropertyBlock</c>، فلا حاجة لكتلةٍ لكل قطرة ولا لتخصيصٍ كل إطار.
    /// </summary>
    public static void Tint(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, color);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>رسّامٌ صامت: بلا ظلال ولا مجسّات ضوء — القطرة تُضيء ولا تُضاء.</summary>
    public static MeshRenderer Renderer(GameObject go, Mesh mesh, Material material)
    {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return renderer;
    }

    // ---------- الجسيمات ----------

    /// <summary>بريقٌ يلمع ويدور ويخبو — نجمةٌ لكل قطرة بين حين وحين، ودفعةٌ عند الالتقاط.</summary>
    public static ParticleSystem Sparks(Transform parent) =>
        Particles("Sparks", parent, sparkle, 700, true);

    /// <summary>حلقةٌ تتّسع وتذوب لحظة الالتقاط.</summary>
    public static ParticleSystem Rings(Transform parent) =>
        Particles("Rings", parent, ring, 48, false);

    /// <summary>
    /// يُضبط والكائن مطفأ ثم يُشغَّل: تغيير مدّة النظام وهو يعمل يرمي خطأً، وبلا مادّةٍ
    /// يرسم ورديًّا. ولا انبعاث تلقائيًّا ولا شكل — كل جسيمٍ يُطلق بيدنا بموضعه ولونه.
    /// </summary>
    private static ParticleSystem Particles(string name, Transform parent, Material material, int max, bool twinkle)
    {
        if (material == null || parent == null) return null;

        var go = new GameObject(name);
        go.SetActive(false);
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;      // يعود للعمل وحده إن أُطفئ أبوه ثم شُغّل (وقت الهدوء)
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 0.25f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = max;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, twinkle
            ? new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f))
            : new AnimationCurve(new Keyframe(0f, 0.15f, 0f, 3f), new Keyframe(0.45f, 0.85f), new Keyframe(1f, 1f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            twinkle
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);

        if (twinkle)
        {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);     // راديان/ث، عشوائيّ بين الحدّين

            // سحبٌ بلا حدّ سرعة: الدفعة تنفجر ثم تتباطأ كأنها في هواء
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = new ParticleSystem.MinMaxCurve(1000f);
            drag.drag = new ParticleSystem.MinMaxCurve(3.2f);
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        go.SetActive(true);
        if (!ps.isPlaying) ps.Play();
        return ps;
    }

    /// <summary>جسيمٌ واحد بموضعه وسرعته ولونه — بلا تخصيص: البنية نفسها لكل الإطلاقات.</summary>
    public static void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float size, float life, Color color)
    {
        if (ps == null) return;

        emit.position = at;
        emit.velocity = velocity;
        emit.startSize = size;
        emit.startLifetime = life;
        emit.startColor = color;
        emit.rotation = Random.Range(0f, 360f);
        emit.applyShapeToPosition = false;
        ps.Emit(emit, 1);
    }

    // ---------- الأشكال ----------

    private static Mesh BuildBall()
    {
        const int rings = 10, segments = 16;
        int count = (rings + 1) * (segments + 1);
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        var colors = new Color32[count];
        var triangles = new int[rings * segments * 6];

        // الضوء من فوق وقليلًا من الجانب: الكاميرا تدور حول اللاعب، والعلوّ وحده ثابت
        Vector3 light = new Vector3(0.3f, 1f, -0.25f).normalized;

        int v = 0;
        for (int r = 0; r <= rings; r++)
        {
            float lat = Mathf.PI * r / rings;
            float y = Mathf.Cos(lat), around = Mathf.Sin(lat);
            for (int s = 0; s <= segments; s++)
            {
                float lon = 2f * Mathf.PI * s / segments;
                var n = new Vector3(around * Mathf.Cos(lon), y, around * Mathf.Sin(lon));
                vertices[v] = n * 0.5f;
                normals[v] = n;

                // نصف لامبرت: لا سواد في الجانب البعيد، فالقطرة مضيئةٌ من كل زاوية
                float lit = 0.5f + 0.5f * Vector3.Dot(n, light);
                byte shade = (byte)(255f * Mathf.Lerp(0.6f, 1f, lit * lit));
                colors[v] = new Color32(shade, shade, shade, 255);
                v++;
            }
        }

        // وجه المثلث الأمامي حيث يشير cross(b-a, c-a) — للخارج هنا في كل المثلثات
        int t = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s;
                int b = a + segments + 1;
                triangles[t++] = a;     triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
        }

        var mesh = new Mesh { name = "ChromaDropBall" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildCard()
    {
        var mesh = new Mesh { name = "ChromaDropCard" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        var white = new Color32(255, 255, 255, 255);
        mesh.colors32 = new[] { white, white, white, white };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// يبني مؤثّرات كل زيّ من جسيماتٍ بالكود، بمواد <c>Osama/Resources/Chroma/Fx</c> —
/// فتدخل البلد يقينًا، ولا يُطلب شيدرٌ بالاسم فيظهر ورديًّا حيث حُذف.
///
/// المقاسات <b>بطول شخصيةٍ واحد</b>: الجذر يُمدّ بطول الشخصية الحقيقي
/// (<see cref="ChromaSkinRig.Place"/>) والجسيمات تتبع المقياس. والأعداد قليلة عمدًا —
/// أكثر نظامٍ هنا سبعون جسيمًا، وزيٌّ واحد يعمل في كل لحظة.
///
/// العالم رماديٌّ خارج مناطق اللون، فالجسيمات <b>بيضاء مضيئة</b> تُقرأ في الرمادي، ولونها
/// الحقيقي يظهر داخل هالة اللاعب.
/// </summary>
public static class ChromaSkinFx
{
    private const string Folder = "Chroma/Fx/";

    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => materials.Clear();

    /// <summary>
    /// مؤثّرات الزيّ رقم <paramref name="skin"/>، أو null للأصليّ. الترتيب هو ترتيب
    /// <see cref="ChromaSkins"/>: جمر، نحاس، غسق، سيرك، موشور.
    /// </summary>
    public static ChromaSkinRig Build(int skin, Transform parent)
    {
        if (skin <= 0 || skin >= ChromaSkins.Count) return null;

        var look = (ChromaSkinRig.Look)(skin - 1);
        var root = new GameObject("Skin_" + look).transform;
        root.SetParent(parent, false);

        var rig = new ChromaSkinRig(look, root);
        switch (look)
        {
            case ChromaSkinRig.Look.Ember: Ember(rig, root); break;
            case ChromaSkinRig.Look.Brass: Brass(rig, root); break;
            case ChromaSkinRig.Look.Twilight: Twilight(rig, root); break;
            case ChromaSkinRig.Look.Circus: Circus(rig, root); break;
            case ChromaSkinRig.Look.Prism: Prism(rig, root); break;
        }
        return rig;
    }

    // ---------- الأزياء ----------

    /// <summary>جمراتٌ تصعد من الجسم وتنطفئ، وشرارٌ صغير، ودفءٌ خافت حوله.</summary>
    private static void Ember(ChromaSkinRig rig, Transform root)
    {
        ParticleSystem embers = NewSystem(root, "Embers", "FxGlowAdd", true, 36, new Vector3(0f, 0.42f, 0f));
        if (embers != null)
        {
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            Rate(embers, 8f);
            Shape(embers, ParticleSystemShapeType.Circle, 0.26f, new Vector3(90f, 0f, 0f));
            Rise(embers, 0.35f, 0.8f);
            Wander(embers, 0.35f, 0.9f, 0.6f);
            Colour(embers, Ramp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.5f, 0.15f),
                                new Color(0.75f, 0.12f, 0.05f), 0.1f, 0.55f, 1f));
            Size(embers, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
        }
        rig.embers = rig.Add(embers, true, false);

        ParticleSystem sparks = NewSystem(root, "Sparks", "FxSparkleAdd", true, 18, new Vector3(0f, 0.5f, 0f));
        if (sparks != null)
        {
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.15f;   // يطفو إلى الأعلى كشررٍ فوق نار
            Rate(sparks, 2.5f);
            Cone(sparks, 30f, 0.12f);
            Colour(sparks, Ramp(Color.white, new Color(1f, 0.8f, 0.4f), new Color(1f, 0.45f, 0.1f), 0.02f, 0.4f, 1f));
            Size(sparks, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        }
        rig.sparks = rig.Add(sparks, true, false);

        ParticleSystem halo = NewSystem(root, "Halo", "FxGlowAdd", false, 2, new Vector3(0f, 0.52f, 0f));
        if (halo != null)
        {
            var main = halo.main;
            main.startLifetime = 4f;
            main.startSize = 1.5f;
            main.startColor = new Color(1f, 0.55f, 0.25f, 0.16f);
            Rate(halo, 0.5f);   // اثنتان تتعاقبان فيتنفّس الدفء ولا يومض
            Colour(halo, Ramp(Color.white, Color.white, Color.white, 0.35f, 0.65f, 1f));
        }
        rig.halo = rig.Add(halo, true, true);
    }

    /// <summary>نفثات بخارٍ من الظهر على إيقاع المشي، ولمعاتٌ نحاسية على الجسم.</summary>
    private static void Brass(ChromaSkinRig rig, Transform root)
    {
        ParticleSystem steam = NewSystem(root, "Steam", "FxSoftAlpha", true, 28, Vector3.zero);
        if (steam != null)
        {
            var main = steam.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.26f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 0.97f, 0.92f, 0.6f);
            Rise(steam, 0.25f, 0.45f);
            Slow(steam, 0.35f, 0.2f);
            Spin(steam, 0.6f, false);
            Colour(steam, Ramp(Color.white, Color.white, Color.white, 0.08f, 0.35f, 1f));
            Size(steam, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));
        }
        rig.steam = rig.Add(steam, false, false);

        ParticleSystem glints = NewSystem(root, "Glints", "FxSparkleAdd", false, 8, new Vector3(0f, 0.5f, 0f));
        if (glints != null)
        {
            var main = glints.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.25f);
            main.startColor = new Color(1f, 0.8f, 0.5f, 1f);
            Rate(glints, 2.2f);
            var shape = glints.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.42f, 0.85f, 0.3f);
            Size(glints, Pop());
        }
        rig.glints = rig.Add(glints, true, true);
    }

    /// <summary>ستّ يراعاتٍ تدور حول اللاعب بكسل، بنفسجيّةٌ وفيروزيّة، تومض وتخبو.</summary>
    private static void Twilight(ChromaSkinRig rig, Transform root)
    {
        ParticleSystem flies = NewSystem(root, "Fireflies", "FxGlowAdd", false, 7, new Vector3(0f, 0.55f, 0f));
        if (flies != null)
        {
            var main = flies.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.52f, 1f), new Color(0.4f, 1f, 0.85f));
            Rate(flies, 1f);   // تولد واحدةً بعد واحدة وتموت كذلك — لا تنطفئ كلها معًا
            Shape(flies, ParticleSystemShapeType.Circle, 0.62f, new Vector3(90f, 0f, 0f));
            var shape = flies.shape;
            shape.radiusThickness = 0.35f;
            shape.randomPositionAmount = 0.22f;

            var velocity = flies.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            Wander(flies, 0.12f, 0.35f, 0.2f);
            Colour(flies, Ramp(Color.white, Color.white, Color.white, 0.12f, 0.85f, 1f));
            Size(flies, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.1f, 1f), new Keyframe(0.22f, 0.55f),
                new Keyframe(0.36f, 1f), new Keyframe(0.5f, 0.65f), new Keyframe(0.66f, 1f),
                new Keyframe(0.82f, 0.6f), new Keyframe(1f, 0.2f)));
        }
        rig.fireflies = rig.Add(flies, true, true);
    }

    /// <summary>قصاصاتٌ حمراء وبيضاء تتقلّب وتهبط ببطء: نافورةٌ مع كل قفزة وهبوط، ورشّةٌ مع الركض.</summary>
    private static void Circus(ChromaSkinRig rig, Transform root)
    {
        ParticleSystem confetti = NewSystem(root, "Confetti", "FxConfettiAlpha", true, 72, new Vector3(0f, 0.25f, 0f));
        if (confetti != null)
        {
            var main = confetti.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.04f, 0.06f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f, 1f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4f);
            main.gravityModifier = 0.5f;
            main.startColor = Candy();
            Cone(confetti, 38f, 0.12f);
            Spin(confetti, 6f, true);
            Slow(confetti, 1.4f, 0.1f);   // تندفع ثم تطفو كالورق
            Colour(confetti, Ramp(Color.white, Color.white, Color.white, 0.02f, 0.75f, 1f));
        }
        rig.confetti = rig.Add(confetti, false, false);
    }

    /// <summary>بريقٌ بكل الألوان حول الجسم وخلفه، وشريطٌ ضوئيّ بلون اللحظة.</summary>
    private static void Prism(ChromaSkinRig rig, Transform root)
    {
        ParticleSystem sparkles = NewSystem(root, "Sparkles", "FxSparkleAdd", true, 64, new Vector3(0f, 0.5f, 0f));
        if (sparkles != null)
        {
            var main = sparkles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startColor = Rainbow();
            Rate(sparkles, 3f);
            Shape(sparkles, ParticleSystemShapeType.Sphere, 0.32f, Vector3.zero);
            Size(sparkles, Pop());
        }
        rig.sparkles = rig.Add(sparkles, true, false);

        ParticleSystem ribbon = NewSystem(root, "Ribbon", "FxGlowAdd", true, 40, new Vector3(0f, 0.45f, 0f));
        if (ribbon != null)
        {
            var main = ribbon.main;
            main.startLifetime = 0.5f;
            main.startSize = 0.28f;
            main.startColor = Rainbow();
            Colour(ribbon, Ramp(Color.white, Color.white, Color.white, 0.02f, 0.1f, 0.45f));
            Size(ribbon, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }
        rig.ribbon = rig.Add(ribbon, false, false);
    }

    // ---------- البناء ----------

    /// <summary>نظامٌ ساكن بإعداداتٍ آمنة، أو null إن غابت مادّته — مؤثّرٌ فائت خيرٌ من مربّعٍ ورديّ.</summary>
    private static ParticleSystem NewSystem(Transform root, string name, string material, bool world, int max,
                                            Vector3 at)
    {
        Material shared = LoadMaterial(material);
        if (shared == null) return null;

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = at;

        var system = go.AddComponent<ParticleSystem>();
        // يبدأ يعمل لحظة إضافته، ومدّته لا تُضبط وهو يعمل
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = true;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // يتبع مقياس الجذر = طول الشخصية
        main.maxParticles = max;
        main.startSpeed = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        Rate(system, 0f);
        var shape = system.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = shared;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return system;
    }

    private static Material LoadMaterial(string name)
    {
        if (materials.TryGetValue(name, out Material found)) return found;

        found = Resources.Load<Material>(Folder + name);
        if (found == null)
            Debug.LogWarning($"[ChromaSkinFx] ما لقيت Osama/Resources/{Folder}{name} — مؤثّرٌ بلا جسيمات.");
        materials[name] = found;     // الغياب يُحفظ أيضًا فلا يتكرّر التحذير
        return found;
    }

    private static void Rate(ParticleSystem system, float perSecond)
    {
        var emission = system.emission;
        emission.rateOverTime = perSecond;
    }

    private static void Shape(ParticleSystem system, ParticleSystemShapeType type, float radius, Vector3 rotation)
    {
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = type;
        shape.radius = radius;
        shape.radiusThickness = 1f;
        shape.rotation = rotation;
    }

    /// <summary>مخروطٌ يشير إلى الأعلى (المخروط يشير إلى +Z، فيُدار ربع دورة).</summary>
    private static void Cone(ParticleSystem system, float angle, float radius)
    {
        Shape(system, ParticleSystemShapeType.Cone, radius, new Vector3(-90f, 0f, 0f));
        var shape = system.shape;
        shape.angle = angle;
    }

    /// <summary>
    /// صعودٌ في العالم. المحاور الثلاثة بنفس النوع عمدًا: يونيتي يرفض منحنيات سرعةٍ
    /// مختلطة الأنواع.
    /// </summary>
    private static void Rise(ParticleSystem system, float min, float max)
    {
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(min, max);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    /// <summary>ضجيجٌ رخيص يجعل المسار متردّدًا لا مستقيمًا.</summary>
    private static void Wander(ParticleSystem system, float strength, float frequency, float scroll)
    {
        var noise = system.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = scroll;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
    }

    /// <summary>كبحٌ لما يتجاوز <paramref name="limit"/>: دفعةٌ أولى ثم طفوّ.</summary>
    private static void Slow(ParticleSystem system, float limit, float dampen)
    {
        var slow = system.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = limit;
        slow.dampen = dampen;
    }

    private static void Spin(ParticleSystem system, float radiansPerSecond, bool allAxes)
    {
        var spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = allAxes;
        var range = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
        spin.x = range;
        spin.y = range;
        spin.z = range;
    }

    private static void Colour(ParticleSystem system, Gradient gradient)
    {
        var colour = system.colorOverLifetime;
        colour.enabled = true;
        colour.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void Size(ParticleSystem system, AnimationCurve curve)
    {
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    /// <summary>ثلاثة ألوان على العمر، وشفافيّةٌ تظهر ثم تثبت ثم تذوب حتى <paramref name="peak"/>.</summary>
    private static Gradient Ramp(Color start, Color middle, Color end, float fadeIn, float holdUntil, float peak)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.45f), new GradientColorKey(end, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, fadeIn),
                new GradientAlphaKey(peak, holdUntil), new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    /// <summary>يكبر بسرعة ويصغر ببطء — ومضة.</summary>
    private static AnimationCurve Pop() =>
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));

    /// <summary>قوس قزح كاملًا، يُختار منه لونٌ لكل جسيم.</summary>
    private static ParticleSystem.MinMaxGradient Rainbow()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.3f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.17f),
                new GradientColorKey(new Color(1f, 0.92f, 0.2f), 0.33f), new GradientColorKey(new Color(0.3f, 0.9f, 0.45f), 0.5f),
                new GradientColorKey(new Color(0.25f, 0.8f, 1f), 0.67f), new GradientColorKey(new Color(0.45f, 0.45f, 1f), 0.83f),
                new GradientColorKey(new Color(0.85f, 0.4f, 1f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
    }

    /// <summary>أحمر وأبيض في الأغلب، ورشّةٌ من الذهبيّ والورديّ والأزرق.</summary>
    private static ParticleSystem.MinMaxGradient Candy()
    {
        Color red = new Color(0.93f, 0.17f, 0.27f);
        var gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(red, 0.22f), new GradientColorKey(Color.white, 0.42f),
                new GradientColorKey(red, 0.56f), new GradientColorKey(Color.white, 0.7f),
                new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0.8f), new GradientColorKey(new Color(1f, 0.5f, 0.65f), 0.9f),
                new GradientColorKey(new Color(0.2f, 0.6f, 0.95f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
    }
}

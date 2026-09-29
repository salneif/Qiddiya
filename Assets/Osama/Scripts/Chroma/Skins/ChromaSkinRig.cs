using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// مؤثّرات زيٍّ واحد حول اللاعب: جسيماتٌ يبنيها <see cref="ChromaSkinFx"/> مرّة وتعيش
/// بين السينات، تعمل ما دام الزيّ ملبوسًا وتسكت حين يُخلع.
///
/// الجذر يتبع قدمي اللاعب موضعًا لا أبوّة (كائن اللاعب يموت مع سينه)، و<b>بلا
/// دوران</b>: اللاعب يستدير نصف دورة في إطار، واليراعات حوله كانت ستقفز معه. ومقياس
/// الجذر = طول الشخصية، والجسيمات تتبع المقياس — فالتصميم بطولٍ واحد يصلح لكل حجم.
///
/// <b>ما يتبع الحركة يُطلق من هنا لا بـ<c>rateOverDistance</c></b>: الأخير يعدّ كل
/// مسافةٍ قطعها الباعث، والعودة بعد الموت قفزةٌ بعشرات الأمتار في إطارٍ واحد — فكان
/// سيرسم خطًّا من القصاصات من مكان الموت إلى نقطة الحفظ. هنا يُسأل عن المسافة
/// الحقيقية ويُتجاهل النقل.
/// </summary>
public sealed class ChromaSkinRig
{
    public enum Look { Ember, Brass, Twilight, Circus, Prism }

    /// <summary>مسافةٌ بين نفثتي بخار (م) — كقاطرةٍ تنفث على إيقاع سرعتها.</summary>
    private const float ChuffEvery = 1.4f;
    private const float ConfettiPerMetre = 1.1f;
    private const float SparklesPerMetre = 5f;
    private const float RibbonPerMetre = 4f;

    private struct Part
    {
        public ParticleSystem system;
        public bool loop;           // يُطلق وحده بمعدّل، لا من هنا
        public bool clearOnStop;    // طويل العمر: يُمسح حين يُخلع الزيّ بدل أن يبقى ثواني
    }

    private readonly Look look;
    private readonly Transform root;
    private readonly List<Part> parts = new List<Part>();

    internal ParticleSystem embers, sparks, halo;       // جمر
    internal ParticleSystem steam, glints;              // نحاس
    internal ParticleSystem fireflies;                  // غسق
    internal ParticleSystem confetti;                   // سيرك
    internal ParticleSystem sparkles, ribbon;           // موشور

    private float scale = 1f, yaw, hue;
    private float chuffLeft = ChuffEvery, confettiDue, sparkleDue, ribbonDue;

    public ChromaSkinRig(Look look, Transform root)
    {
        this.look = look;
        this.root = root;
    }

    internal ParticleSystem Add(ParticleSystem system, bool loop, bool clearOnStop)
    {
        if (system != null) parts.Add(new Part { system = system, loop = loop, clearOnStop = clearOnStop });
        return system;
    }

    /// <summary>يلبس المؤثّر أو يخلعه. الخلع يترك القصير يذوب مكانه، ويمسح الطويل.</summary>
    public void SetActive(bool on)
    {
        foreach (Part part in parts)
        {
            if (part.system == null) continue;

            if (!on)
            {
                part.system.Stop(false, part.clearOnStop
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
                continue;
            }

            var emission = part.system.emission;
            emission.enabled = part.loop;
            if (!part.system.isPlaying) part.system.Play(false);
        }

        if (!on) return;

        chuffLeft = ChuffEvery * 0.5f;
        confettiDue = sparkleDue = ribbonDue = 0f;

        // لا يبدأ فارغًا: الهالة واليراعات تحتاج ثواني لتمتلئ بمعدّلها وحده
        if (halo != null && halo.particleCount == 0) halo.Emit(1);
        if (fireflies != null) Fill(fireflies, 5);
    }

    /// <summary>الخزانة توقف الزمن وتعرض الزيّ حيًّا: مؤثّراته تمشي بالوقت الحقيقي ما دامت مفتوحة.</summary>
    public void UseUnscaledTime(bool unscaled)
    {
        foreach (Part part in parts)
        {
            if (part.system == null) continue;
            var main = part.system.main;
            main.useUnscaledTime = unscaled;
        }
    }

    /// <summary>سينٌ جديد: لا يبقى شيءٌ في إحداثيّات السين الذي ذهب.</summary>
    public void Clear()
    {
        foreach (Part part in parts)
            if (part.system != null) part.system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// يضع الجذر عند القدمين. <paramref name="height"/> طول الشخصية،
    /// <paramref name="facing"/> زاوية وجهها (للبخار من الظهر)، <paramref name="hue01"/>
    /// لون الموشور الآن.
    /// </summary>
    public void Place(Vector3 feet, float facing, float height, float hue01)
    {
        root.position = feet;
        if (!Mathf.Approximately(scale, height))
        {
            scale = height;
            root.localScale = new Vector3(height, height, height);
        }
        yaw = facing;
        hue = hue01;
    }

    /// <summary>مشت القدمان من <paramref name="from"/> إلى الجذر مسافةً حقيقية (بلا نقل).</summary>
    public void Moved(Vector3 from, float distance)
    {
        if (distance <= 0f) return;

        switch (look)
        {
            case Look.Brass:
                chuffLeft -= distance;
                if (chuffLeft <= 0f)
                {
                    chuffLeft += ChuffEvery;
                    Chuff(2, 1f);
                }
                break;

            case Look.Circus:
                confettiDue += distance * ConfettiPerMetre;
                while (confettiDue >= 1f)
                {
                    confettiDue -= 1f;
                    Vector3 at = Vector3.Lerp(from, root.position, Random.value) + Vector3.up * (0.45f * scale);
                    Vector3 v = (Vector3.up * Random.Range(0.8f, 1.6f) + Flat() * 0.6f) * scale;
                    Emit(confetti, at, v, default);
                }
                break;

            case Look.Prism:
                sparkleDue += distance * SparklesPerMetre;
                ribbonDue += distance * RibbonPerMetre;
                while (sparkleDue >= 1f)
                {
                    sparkleDue -= 1f;
                    Vector3 at = Vector3.Lerp(from, root.position, Random.value) +
                                 (Vector3.up * Random.Range(0.25f, 0.8f) + Flat() * 0.25f) * scale;
                    Emit(sparkles, at, Random.insideUnitSphere * (0.25f * scale), default);
                }
                while (ribbonDue >= 1f)
                {
                    ribbonDue -= 1f;
                    Vector3 at = Vector3.Lerp(from, root.position, Random.value) + Vector3.up * (0.45f * scale);
                    Emit(ribbon, at, Vector3.zero, Color.HSVToRGB(Mathf.Repeat(hue + Random.Range(-0.04f, 0.04f), 1f), 0.75f, 1f));
                }
                break;
        }
    }

    public void Jumped()
    {
        switch (look)
        {
            case Look.Ember: Emit(sparks, 6); break;
            case Look.Brass: Chuff(3, 1.3f); break;
            case Look.Circus: Emit(confetti, 14); break;
            case Look.Prism: Ring(sparkles, 8, 0.2f, 1.2f); break;
        }
    }

    /// <summary>هبط. <paramref name="impact"/> ٠..١ — من خطوةٍ عن درجة إلى سقطةٍ طويلة.</summary>
    public void Landed(float impact)
    {
        switch (look)
        {
            case Look.Ember: Emit(sparks, 5 + Mathf.RoundToInt(6f * impact)); break;
            case Look.Brass: Ring(steam, 3 + Mathf.RoundToInt(2f * impact), 0.1f, 0.9f); break;
            case Look.Circus: Ring(confetti, 12 + Mathf.RoundToInt(14f * impact), 0.15f, 2.4f); break;
            case Look.Prism: Ring(sparkles, 10 + Mathf.RoundToInt(8f * impact), 0.15f, 1.6f); break;
        }
    }

    /// <summary>
    /// عرضٌ للخزانة: اللاعب واقفٌ والزمن موقوف، فما يُطلق بالحركة لن يُرى وحده.
    /// <paramref name="big"/> = لحظة اللبس.
    /// </summary>
    public void Showcase(bool big)
    {
        int more = big ? 2 : 1;
        switch (look)
        {
            case Look.Ember:
                Emit(sparks, 6 * more);
                Emit(embers, 8 * more);
                break;
            case Look.Brass:
                Chuff(2 + more, 1.2f);
                Emit(glints, 2 * more);
                break;
            case Look.Twilight:
                Fill(fireflies, 6);
                break;
            case Look.Circus:
                Emit(confetti, 14 * more);
                break;
            case Look.Prism:
                Ring(sparkles, 10 * more, 0.5f, 1.4f);
                Ring(ribbon, 6 * more, 0.45f, 0.8f);
                break;
        }
    }

    // ---------- الإطلاق ----------

    /// <summary>نفثة بخارٍ من أعلى الظهر، إلى الخلف ثم إلى الأعلى.</summary>
    private void Chuff(int count, float strength)
    {
        if (steam == null) return;

        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        Vector3 back = facing * Vector3.back;
        Vector3 at = root.position + facing * new Vector3(0f, 0.8f, -0.2f) * scale;

        for (int i = 0; i < count; i++)
        {
            Vector3 v = (back * 0.55f + Vector3.up * 0.45f + Random.insideUnitSphere * 0.15f) * (strength * scale);
            Emit(steam, at, v, default);
        }
    }

    /// <summary>حلقةٌ تنتشر أفقيًّا من ارتفاع <paramref name="up"/> (بطول الشخصية).</summary>
    private void Ring(ParticleSystem system, int count, float up, float speed)
    {
        if (system == null) return;

        Vector3 at = root.position + Vector3.up * (up * scale);
        float start = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = start + i * Mathf.PI * 2f / count;
            Vector3 dir = new Vector3(Mathf.Cos(a), Random.Range(0.35f, 0.9f), Mathf.Sin(a));
            Emit(system, at, dir * (speed * Random.Range(0.8f, 1.15f) * scale), default);
        }
    }

    /// <summary>يملأ النظام حتى <paramref name="count"/> جسيمًا حيًّا — لا يزيد عليه.</summary>
    private static void Fill(ParticleSystem system, int count)
    {
        if (system == null) return;
        int missing = count - system.particleCount;
        if (missing > 0) system.Emit(missing);
    }

    /// <summary>من شكل النظام نفسه، بسرعته — كما يُطلق وحده.</summary>
    private static void Emit(ParticleSystem system, int count)
    {
        if (system != null && count > 0) system.Emit(count);
    }

    /// <summary>
    /// جسيمٌ في موضعٍ وبسرعةٍ بعينهما (إحداثيّات العالم). اللون <c>default</c> = لون النظام.
    /// </summary>
    private static void Emit(ParticleSystem system, Vector3 at, Vector3 velocity, Color32 color)
    {
        if (system == null) return;

        var p = new ParticleSystem.EmitParams
        {
            position = at,
            velocity = velocity,
            applyShapeToPosition = false,
        };
        if (color.a > 0) p.startColor = color;
        system.Emit(p, 1);
    }

    /// <summary>اتّجاهٌ أفقيّ عشوائيّ بطولٍ حتى الواحد.</summary>
    private static Vector3 Flat()
    {
        Vector2 r = Random.insideUnitCircle;
        return new Vector3(r.x, 0f, r.y);
    }
}

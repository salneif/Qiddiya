using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// اللون حول أقرب القطرات: ستّ فقاعاتٍ صغيرة من <see cref="ColorZones"/> تنتقل بين
/// القطرات كلّما مشى اللاعب، فتتفتّح القطرة ملوّنةً حين يقترب منها وتبقى البعيدة
/// ضوءًا أبيض في العالم الرمادي.
///
/// <b>ستٌّ لا أكثر</b>: مخزون المناطق مشتركٌ بين كل مؤثّرات اللون (القطرات ستّ، هالة
/// الزيّ واحدة، والباقي لنبضات الاحتفال). والفقاعة لا تقفز من قطرةٍ لأخرى: تذبل حتى
/// تختفي ثم تنتقل وتتفتّح — فلا يُرى اللون يطير عبر المكان.
///
/// وتُفلَت كلها بعد ثانيتين بلا قطرةٍ قريبة، فلا نحجز من المخزون ما لا نرسم به.
/// </summary>
internal sealed class ChromaDropZones
{
    public const int Count = 6;
    private const float Range = 14f;
    private const float Radius = 0.8f;
    private const float Reach = 1f;
    private const float GrowRate = 3.2f, ShrinkRate = 6f;
    private const float IdleRelease = 2f;
    private const float Retry = 1f;

    /// <summary>الذهبية تُعدّ أقرب مما هي: لونها يتفتّح قبل غيرها.</summary>
    private const float GoldenPull = 0.6f;

    private sealed class Slot
    {
        public Transform anchor;
        public ColorZones.Handle handle;
        public ChromaDrop target, want;
        public float radius, idleSince = -1f, retryAt;
    }

    private readonly Slot[] slots = new Slot[Count];
    private readonly ChromaDrop[] nearest = new ChromaDrop[Count];
    private readonly float[] distances = new float[Count];

    /// <summary>مراسي الفقاعات تحت حاوية السين، فتموت معه كما تموت مناطقه.</summary>
    public void Build(Transform parent)
    {
        for (int i = 0; i < Count; i++)
        {
            var go = new GameObject("Zone" + i);
            go.transform.SetParent(parent, false);
            slots[i] = new Slot { anchor = go.transform };
        }
    }

    /// <summary>من يستحقّ اللون الآن: أقرب ستٍّ داخل المدى. يُنادى كل جزءٍ من الثانية لا كل إطار.</summary>
    public void Assign(List<ChromaDrop> drops, Vector3 player)
    {
        if (slots[0] == null) return;

        int found = 0;
        float range2 = Range * Range;
        foreach (ChromaDrop d in drops)
        {
            if (d.state == ChromaDrop.Phase.Waiting) continue;
            float dist = (d.position - player).sqrMagnitude;
            if (dist > range2) continue;
            if (d.golden) dist *= GoldenPull * GoldenPull;

            // إدراجٌ مرتّب في مصفوفةٍ ثابتة — بلا فرزٍ ولا تخصيص
            int at = found < Count ? found++ : Count;
            if (at == Count && dist >= distances[Count - 1]) continue;
            if (at == Count) at = Count - 1;
            while (at > 0 && distances[at - 1] > dist)
            {
                nearest[at] = nearest[at - 1];
                distances[at] = distances[at - 1];
                at--;
            }
            nearest[at] = d;
            distances[at] = dist;
        }

        // من بقي في القائمة يبقى في فقاعته؛ الباقون يُوزَّعون على الفارغة
        foreach (Slot s in slots)
            if (s.want != null && System.Array.IndexOf(nearest, s.want, 0, found) < 0) s.want = null;

        for (int i = 0; i < found; i++)
        {
            ChromaDrop d = nearest[i];
            if (Holds(d)) continue;
            foreach (Slot s in slots)
            {
                if (s.want != null) continue;
                s.want = d;
                break;
            }
        }

        System.Array.Clear(nearest, 0, Count);
    }

    /// <summary>كل إطار: تذبل الفقاعة قبل أن تنتقل، وتتفتّح عند هدفها، وتتبعه.</summary>
    public void Tick(float dt, float now)
    {
        foreach (Slot s in slots)
        {
            if (s == null || s.anchor == null) continue;

            if (s.target != s.want)
            {
                s.radius = Mathf.MoveTowards(s.radius, 0f, dt * ShrinkRate * Radius);
                if (s.radius <= 0.001f)
                {
                    if (s.target != null) s.target.bloom = 0f;
                    s.target = s.want;
                }
            }
            else if (s.target != null)
            {
                s.radius = Mathf.MoveTowards(s.radius, Radius, dt * GrowRate * Radius);
            }

            if (s.target != null)
            {
                s.anchor.position = s.target.position;
                s.target.bloom = s.radius / Radius;
                s.idleSince = -1f;

                if ((s.handle == null || !s.handle.Alive) && now >= s.retryAt)
                {
                    s.retryAt = now + Retry;
                    s.handle = ColorZones.Follow(s.anchor, Vector3.zero, 0.01f, Reach);
                }
            }
            else if (s.handle != null)
            {
                if (s.idleSince < 0f) s.idleSince = now;
                else if (now - s.idleSince > IdleRelease) Release(s);
            }

            if (s.handle != null && s.handle.Alive)
            {
                float k = s.radius / Radius;
                s.handle.Radius = Radius * k * (2f - k);   // تتفتّح سريعًا وتهدأ عند حجمها
            }
        }
    }

    /// <summary>قطرةٌ جُمعت أو ذابت: لا فقاعة تتبعها بعد الآن.</summary>
    public void Forget(ChromaDrop drop)
    {
        foreach (Slot s in slots)
        {
            if (s == null) continue;
            if (s.want == drop) s.want = null;
        }
    }

    /// <summary>يُفلت كل المناطق — عند الهدوء وعند تغيّر السين.</summary>
    public void ReleaseAll()
    {
        foreach (Slot s in slots)
        {
            if (s == null) continue;
            Release(s);
            if (s.target != null) s.target.bloom = 0f;
            s.target = s.want = null;
            s.radius = 0f;
        }
    }

    private bool Holds(ChromaDrop drop)
    {
        foreach (Slot s in slots)
            if (s.want == drop) return true;
        return false;
    }

    private static void Release(Slot s)
    {
        ColorZones.Release(s.handle);
        s.handle = null;
        s.idleSince = -1f;
    }
}

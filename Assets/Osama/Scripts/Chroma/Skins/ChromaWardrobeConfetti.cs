using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// قصاصاتٌ ملوّنة في الواجهة: تنفجر من نقطة، تتقلّب وتسقط، ثم تذوب.
///
/// <b>عددٌ ثابت يُبنى مرّة ويُعاد استعماله</b> — الانفجار يأخذ الأقدم إن لم يبقَ
/// فارغ، فلا يُنشأ كائنٌ ولا يُهدم وقت اللعب. والوقت يُمرَّر من المالك (غير متأثّر
/// بالإيقاف)، فتعمل والخزانة توقف الزمن.
/// </summary>
public sealed class ChromaWardrobeConfetti
{
    private const float Gravity = 1500f;
    private const float Drag = 1.6f;

    private readonly RectTransform[] pieces;
    private readonly Image[] images;
    private readonly Vector2[] position, velocity;
    private readonly float[] age, life, spin, angle, flutter;
    private readonly Color[] tint;
    private int next;

    /// <summary>هل في الهواء قصاصة؟ لمن يطفئ كانفسه حين يسكن كل شيء.</summary>
    public bool Busy { get; private set; }

    public ChromaWardrobeConfetti(Transform parent, int count)
    {
        pieces = new RectTransform[count];
        images = new Image[count];
        position = new Vector2[count];
        velocity = new Vector2[count];
        age = new float[count];
        life = new float[count];
        spin = new float[count];
        angle = new float[count];
        flutter = new float[count];
        tint = new Color[count];

        for (int i = 0; i < count; i++)
        {
            images[i] = ChromaWardrobeArt.NewImage(parent, "Confetti", ChromaWardrobeArt.Round, Color.white);
            pieces[i] = ChromaWardrobeArt.Place(images[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                Vector2.zero, new Vector2(16f, 8f));
            pieces[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// ينثر <paramref name="count"/> قصاصة من <paramref name="at"/> (إحداثيّات الأب،
    /// من مركزه) إلى الأعلى. <paramref name="power"/> = قوّة الدفعة.
    /// </summary>
    public void Burst(Vector2 at, int count, float power)
    {
        ChromaStyle style = ChromaStyle.Get();
        for (int n = 0; n < count; n++)
        {
            int i = next;
            next = (next + 1) % pieces.Length;

            float a = Random.Range(-75f, 75f) * Mathf.Deg2Rad;
            velocity[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (Random.Range(420f, 920f) * power);
            position[i] = at + Random.insideUnitCircle * 14f;
            age[i] = 0f;
            life[i] = Random.Range(0.8f, 1.35f);
            spin[i] = Random.Range(-720f, 720f);
            angle[i] = Random.Range(0f, 360f);
            flutter[i] = Random.Range(6f, 15f);
            tint[i] = style.Palette(Random.Range(0, 7));

            pieces[i].sizeDelta = new Vector2(Random.Range(12f, 21f), Random.Range(6f, 9f));
            pieces[i].gameObject.SetActive(true);
        }

        Busy = count > 0 || Busy;
    }

    public void Tick(float dt)
    {
        if (!Busy) return;

        bool any = false;
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!pieces[i].gameObject.activeSelf) continue;

            age[i] += dt;
            if (age[i] >= life[i])
            {
                pieces[i].gameObject.SetActive(false);
                continue;
            }

            any = true;
            velocity[i].y -= Gravity * dt;
            velocity[i] *= Mathf.Exp(-Drag * dt);
            position[i] += velocity[i] * dt;
            angle[i] += spin[i] * dt;

            pieces[i].anchoredPosition = position[i];
            pieces[i].localRotation = Quaternion.Euler(0f, 0f, angle[i]);
            // تقلّبٌ مزيّف: عرضها يضيق ويتّسع كورقةٍ تدور في الهواء
            pieces[i].localScale = new Vector3(Mathf.Cos(age[i] * flutter[i]), 1f, 1f);

            Color c = tint[i];
            c.a = 1f - Mathf.Clamp01((age[i] - life[i] * 0.7f) / (life[i] * 0.3f));
            images[i].color = c;
        }

        Busy = any;
    }

    public void Clear()
    {
        foreach (RectTransform piece in pieces)
            if (piece != null) piece.gameObject.SetActive(false);
        Busy = false;
    }
}

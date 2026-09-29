using UnityEngine;

/// <summary>
/// لحن الالتقاط: كل قطرةٍ نغمةٌ من <c>Drop_Note</c>، وكل قطرةٍ خلال ١٫٢ ث من التي قبلها
/// تصعد درجةً في السلّم الخماسي — صفّ القطرات يُسمع سلّمًا يُعزف لا نقرًا يتكرّر.
/// وحين تنقطع سلسلةٌ من خمسٍ فأكثر يُختم اللحن بـ<c>Combo_End</c>.
///
/// <b>السلّم يبدأ تحت نغمة المقطع برابعة</b> (صول تحت دو): <see cref="ChromaSfx"/> يسقف
/// الطبقة عند ×٣، أي ١٩ نصف درجة. والسلّم من دو يبلغ ٢٤ فتُسحق درجاته الأخيرة في
/// درجةٍ واحدة؛ ومن صول يتّسع كله (١١ درجة) ويبقى على نغمات دو الخماسيّة نفسها.
///
/// <b>ودفعةٌ من القطرات لا تُسمع ضجّة</b>: النغمات تُصفّ وتُعزف بفاصلٍ قصير، فخمس عشرة
/// قطرةً تُلتقط معًا تصير عَفقةً سريعة صاعدة. والزمن زمن اللعب: الإيقاف يجمّد السلسلة
/// فلا يُسمع ختامها فوق قائمة الإيقاف.
/// </summary>
internal sealed class ChromaDropSong
{
    private const float Window = 1.2f;
    private const int ChordAt = 5;
    private const float Gap = 0.055f;
    private const int Backlog = 8;
    private const float NoteVolume = 0.55f, GoldVolume = 0.85f, ChordVolume = 0.7f;

    /// <summary>دو الخماسيّ من صول تحتها: صول لا دو ري مي صول لا دو ري مي صول.</summary>
    private static readonly float[] Steps = { -5f, -3f, 0f, 2f, 4f, 7f, 9f, 12f, 14f, 16f, 19f };

    private readonly float[] queue = new float[Backlog];
    private int queued, head;
    private float nextNoteAt;
    private float lastPickup = -10f;

    /// <summary>طول السلسلة الجارية (٠ = لا سلسلة).</summary>
    public int Combo { get; private set; }

    /// <summary>التقاطٌ جديد: يمدّ السلسلة أو يبدأ غيرها، ويصفّ نغمته.</summary>
    public void Pickup(bool golden, float now)
    {
        if (now - lastPickup > Window) Finish();
        Combo++;
        lastPickup = now;

        if (golden)
        {
            ChromaSfx.Play("Drop_Gold", GoldVolume);
            return;
        }

        if (queued == Backlog) return;   // دفعةٌ كبيرة: تكفي ثمانٍ تُسمع، والعدّ لا ينقص
        float step = Steps[Mathf.Min(Combo - 1, Steps.Length - 1)];
        queue[(head + queued) % Backlog] = step;
        queued++;
    }

    /// <summary>كل إطار: نغمةٌ من الصفّ إن حان وقتها، وختام السلسلة إن انقطعت.</summary>
    public void Tick(float now)
    {
        if (queued > 0 && now >= nextNoteAt)
        {
            ChromaSfx.Play("Drop_Note", NoteVolume, ChromaSfx.Semitones(queue[head]));
            head = (head + 1) % Backlog;
            queued--;
            nextNoteAt = now + Gap;
        }

        if (Combo > 0 && queued == 0 && now - lastPickup > Window) Finish();
    }

    /// <summary>سينٌ جديد: لا نغمة معلّقة ولا ختام يُسمع فوق شاشة التحميل.</summary>
    public void Reset()
    {
        queued = head = 0;
        Combo = 0;
        lastPickup = -10f;
        nextNoteAt = 0f;
    }

    private void Finish()
    {
        if (Combo >= ChordAt) ChromaSfx.Play("Combo_End", ChordVolume);
        Combo = 0;
    }
}

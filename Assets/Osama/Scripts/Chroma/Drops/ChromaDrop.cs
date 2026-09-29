using UnityEngine;

/// <summary>
/// قطرةٌ واحدة في العالم: أين تستقرّ، ولونها، وفي أيّ مرحلةٍ من حياتها.
///
/// بياناتٌ لا <c>MonoBehaviour</c>: مئة قطرة بمئة <c>Update</c> تكلّف أكثر من حلقةٍ
/// واحدة تمرّ عليها كلها في <see cref="ChromaDropField"/>.
/// </summary>
internal sealed class ChromaDrop
{
    public enum Phase
    {
        Waiting,    // وُلدت ولم يحن ظهورها (موجة الظهور تمشي من اللاعب للخارج)
        Popping,    // تكبر من لا شيء بقفزةٍ مرنة
        Resting,    // تطفو في مكانها
        Arcing,     // دفعةُ حدث: تطير في قوس ثم تستقرّ
        Flying,     // جذبها اللاعب: تطير إلى صدره
    }

    public string id;             // null لقطرات الأحداث — لا تُحفظ
    public int value = 1;
    public Color color;
    public bool golden, big;
    public bool runaway;          // الوجه الهارب: ينطّ بعيدًا حين يُقترب منه
    public int hopsLeft;

    public Vector3 rest;          // موضع الطفو
    public Collider floor;        // أرضها — تتبعها إن تحرّكت، وتذوب إن اختفت
    public Vector3 floorLocal;
    public float phase;

    public Phase state;
    public float since;           // متى بدأت مرحلتها (Time.time)
    public float delay;           // انتظار الظهور
    public Vector3 from;          // بداية القوس أو الطيران
    public float duration;        // مدّة القوس أو الطيران
    public float height;          // ارتفاع القوس
    public float collectibleAt;
    public float autoAt;          // قطرات الأحداث: بعده تنجذب من بعيد
    public float nextLook;        // آخر فحص نظرٍ للجذب
    public float nextSpark;

    public Vector3 position;      // موضعها المرئيّ الآن
    public float scale = 1f;
    public float bloom;           // كم هي داخل منطقة لونها (٠..١)
    public float painted = -1f;   // آخر bloom رُسم به — لا نعيد التلوين بلا تغيّر
    public ChromaDropView view;

    public float Size => golden ? 0.4f : big ? 0.34f : 0.26f;
    public bool Ringed => golden || big;

    /// <summary>يمكن جذبها الآن؟</summary>
    public bool Collectible(float now) =>
        (state == Phase.Resting || state == Phase.Arcing) && now >= collectibleAt;

    /// <summary>يربطها بأرضها بإحداثيّات الأرض نفسها، فتركب ما تحرّك.</summary>
    public void Anchor(Vector3 ground, Collider collider) =>
        Anchor(ground, collider, collider != null ? collider.transform.InverseTransformPoint(ground) : ground);

    /// <summary>
    /// كالتي قبلها بإحداثيّاتٍ حُسبت لحظة فحص الأرض (<paramref name="local"/>)، لا بعد
    /// التخطيط كله: القارب يكون قد مشى، فتولد القطرة بجانبه فوق الماء. وأرضٌ ذهبت في
    /// الأثناء تُبقيها حيث فُحصت، ثم تذوب في أوّل <see cref="Follow"/>.
    /// </summary>
    public void Anchor(Vector3 ground, Collider collider, Vector3 local)
    {
        floor = collider;
        bool held = collider != null;
        floorLocal = held ? local : ground;
        rest = (held ? collider.transform.TransformPoint(local) : ground) + Vector3.up * ChromaDropProbe.Hover;
    }

    /// <summary>
    /// موضع الطفو من أرضها. false إن ذهبت الأرض — دُمّرت أو أُطفئت — فالقطرة لم
    /// يعد تحتها ما يحملها.
    /// </summary>
    public bool Follow()
    {
        if (ReferenceEquals(floor, null)) return true;
        if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy) return false;
        rest = floor.transform.TransformPoint(floorLocal) + Vector3.up * ChromaDropProbe.Hover;
        return true;
    }
}

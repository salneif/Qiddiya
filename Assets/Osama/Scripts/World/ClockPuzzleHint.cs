using UnityEngine;

/// <summary>
/// يجعل لغز الساعة قابلًا للحلّ: عقربان شاحبان يقفان على الوقت المطلوب فيحاذي
/// اللاعب عليهما، وتلوّن متدرّج على العقربين الحقيقيين يقول له إن كان يقترب.
///
/// سبب صعوبة اللغز لم يكن نقص التأكيد بل أن <b>الهدف نفسه مخفي</b>: اللاعب يقلّب
/// الرافعات ويرى العقارب تتحرك بلا أن يعرف إلى أين. العقربان الشاحبان يحوّلان
/// السؤال من «خمّن الرقم» إلى «حاذِ العلامة» — وهذا وحده هو الإصلاح. والتلوّن
/// تأكيدٌ يمشي معه في الطريق.
///
/// مستقلّ تمامًا عن سكربت اللغز: لا يناديه ولا يقرأ منه. يستنتج الوقت الحالي من
/// <b>دوران عقرب الساعات نفسه</b>، فيصلح مع أي ساعة من أي أحد ولا ينكسر إن غُيّر
/// سكربتها.
///
/// حُطّه على أي كائن، وأعطه العقربين والوقت المطلوب بالدقائق (٣:٠٠ = 180).
/// </summary>
[DisallowMultipleComponent]
public class ClockPuzzleHint : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("الساعة")]
    [Tooltip("عقرب الساعات — يُقرأ منه الوقت الحالي")]
    [SerializeField] private Transform hourHand;
    [Tooltip("عقرب الدقائق")]
    [SerializeField] private Transform minuteHand;

    [Header("الهدف")]
    [Tooltip("الوقت المطلوب بالدقائق: 3:00 = 180، 6:30 = 390")]
    [SerializeField] private float targetMinutes = 180f;
    [Tooltip("الهامش المقبول — طابِقه مع Tolerance في لغز سلطان")]
    [SerializeField] private float tolerance = 2f;

    [Header("عقربا الهدف")]
    [Tooltip("ينسخ العقربين ويثبّتهما على الوقت المطلوب، شاحبين، فيحاذي اللاعب عليهما")]
    [SerializeField] private bool showGhostHands = true;
    [Tooltip("لون العقربين الشاحبين")]
    [SerializeField] private Color ghostColor = new Color(1f, 0.85f, 0.35f, 1f);
    [Tooltip("تصغير طفيف للعقرب الشاحب حتى لا يحجب الحقيقي تمامًا")]
    [Range(0.5f, 1f)] [SerializeField] private float ghostScale = 0.92f;
    [Tooltip("نبض خفيف على العقربين الشاحبين ليُلاحظا")]
    [SerializeField] private float ghostPulse = 0.8f;

    [Header("التلوّن بالقرب")]
    [Tooltip("يلوّن العقربين الحقيقيين كلما اقترب المجموع من الهدف")]
    [SerializeField] private bool tintByDistance = true;
    [Tooltip("من هذي المسافة بالدقائق يبدأ اللون يتغيّر")]
    [SerializeField] private float warmRange = 240f;
    [Tooltip("لون الاقتراب")]
    [SerializeField] private Color nearColor = new Color(1f, 0.7f, 0.2f, 1f);
    [Tooltip("لون الوصول — أبيض ساطع")]
    [SerializeField] private Color solvedColor = Color.white;

    [Header("عند الحلّ")]
    [Tooltip("يخفي العقربين الشاحبين بعد الحلّ، فلا يبقى في المشهد ما لا معنى له")]
    [SerializeField] private bool hideGhostsWhenSolved = true;

    private Transform ghostHour;
    private Transform ghostMinute;
    private Renderer[] hourRenderers;
    private Renderer[] minuteRenderers;
    private Renderer[] ghostRenderers;
    private MaterialPropertyBlock block;
    private bool solved;

    private void Awake()
    {
        if (hourHand == null || minuteHand == null)
        {
            Debug.LogWarning("[ClockPuzzleHint] ناقص عقرب — حُطّ Hour Hand و Minute Hand.", this);
            enabled = false;
            return;
        }

        block = new MaterialPropertyBlock();
        hourRenderers = hourHand.GetComponentsInChildren<Renderer>(true);
        minuteRenderers = minuteHand.GetComponentsInChildren<Renderer>(true);

        if (showGhostHands) BuildGhosts();
    }

    /// <summary>
    /// ينسخ العقربين ويجرّدهما ويثبّتهما على زاويتي الوقت المطلوب. النسخ لا الرسم:
    /// فالعلامة تطابق شكل عقرب هذي الساعة بالضبط، ولا تحتاج أي أصل جديد.
    /// </summary>
    private void BuildGhosts()
    {
        ghostHour = Ghost(hourHand, "ClockHint_GhostHour", Angle(targetMinutes, 720f));
        ghostMinute = Ghost(minuteHand, "ClockHint_GhostMinute", Angle(targetMinutes, 60f));

        var found = new System.Collections.Generic.List<Renderer>();
        if (ghostHour != null) found.AddRange(ghostHour.GetComponentsInChildren<Renderer>(true));
        if (ghostMinute != null) found.AddRange(ghostMinute.GetComponentsInChildren<Renderer>(true));
        ghostRenderers = found.ToArray();

        if (ghostRenderers.Length == 0)
            Debug.LogWarning("[ClockPuzzleHint] العقارب بلا Renderer — ما فيه ما يُنسخ.", this);
    }

    private Transform Ghost(Transform source, string name, float angle)
    {
        GameObject clone = Instantiate(source.gameObject, source.parent);
        clone.name = name;

        // زينة لا جسم: بلا كولايدرات ولا سكربتات تحرّكها مع الأصل
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled = false;

        clone.SetActive(true);
        Transform t = clone.transform;
        t.localPosition = source.localPosition;
        t.localRotation = Quaternion.Euler(0f, 0f, -angle);
        t.localScale = source.localScale * ghostScale;
        return t;
    }

    private static float Angle(float minutes, float fullTurn) => minutes / fullTurn * 360f;

    private void LateUpdate()
    {
        float now = CurrentMinutes();
        float off = Mathf.Abs(Mathf.DeltaAngle(Angle(now, 720f), Angle(targetMinutes, 720f)))
                    / 360f * 720f;   // الفرق بالدقائق، بأقصر الطريقين حول القرص

        bool hit = off <= tolerance;
        if (hit && !solved)
        {
            solved = true;
            // ساعةٌ محلولةٌ أصلًا لحظة دخول السين ليست حلًّا يُحتفل به
            if (Time.timeSinceLevelLoad > 1.5f)
            {
                PadRumble.Open(0.9f);   // انحلّت الساعة — تُحسّ في اليد
                ChromaEvents.RaisePuzzleSolved(transform.position);
            }
        }

        if (tintByDistance) Tint(off, hit);
        if (ghostRenderers != null) Ghosts(hit);
    }

    /// <summary>
    /// الوقت الحالي من دوران عقرب الساعات لا من سكربت اللغز: العقرب يلفّ دورة كاملة
    /// في اثنتي عشرة ساعة، فزاويته تحدّد الدقائق بلا التباس — بخلاف عقرب الدقائق
    /// الذي يعيد نفسه كل ساعة.
    /// </summary>
    private float CurrentMinutes()
    {
        float angle = Mathf.Repeat(-hourHand.localEulerAngles.z, 360f);
        return angle / 360f * 720f;
    }

    private void Tint(float off, bool hit)
    {
        Color color;
        if (hit)
        {
            color = solvedColor;
        }
        else
        {
            float near = 1f - Mathf.Clamp01(off / Mathf.Max(1f, warmRange));
            // التربيع يجعل التلوّن متأخّرًا: لا يلمع إلا وقد قارب فعلًا
            color = Color.Lerp(Color.white * 0.35f, nearColor, near * near);
        }

        Paint(hourRenderers, color);
        Paint(minuteRenderers, color);
    }

    private void Ghosts(bool hit)
    {
        if (hideGhostsWhenSolved && solved)
        {
            if (ghostHour != null) ghostHour.gameObject.SetActive(false);
            if (ghostMinute != null) ghostMinute.gameObject.SetActive(false);
            return;
        }

        float wave = ghostPulse > 0f
            ? Mathf.Lerp(0.55f, 1f, Mathf.Sin(Time.time * ghostPulse * Mathf.PI * 2f) * 0.5f + 0.5f)
            : 1f;

        Paint(ghostRenderers, ghostColor * wave);
    }

    /// <summary>
    /// اللون عبر <c>_BaseColor</c> بـ MaterialPropertyBlock: يعمل على URP Lit بلا أي
    /// كلمة مفتاحية — بخلاف <c>_EmissionColor</c> الذي لا يرسمه يونيتي ما لم يكن
    /// <c>_EMISSION</c> مفعّلًا في الماتيريال. ولأنه Property Block فمادّة العقرب
    /// المشتركة لا تُمسّ.
    /// </summary>
    private void Paint(Renderer[] renderers, Color color)
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    [ContextMenu("تجربة: اعرض الوقت الحالي")]
    private void DebugNow()
    {
        if (hourHand == null) return;

        float now = CurrentMinutes();
        Debug.Log($"[ClockPuzzleHint] الآن {(int)(now / 60f):00}:{(int)(now % 60f):00} " +
                  $"({now:0.0} دقيقة) • المطلوب {(int)(targetMinutes / 60f):00}:" +
                  $"{(int)(targetMinutes % 60f):00}", this);
    }

    private void OnValidate()
    {
        targetMinutes = Mathf.Repeat(targetMinutes, 720f);
        tolerance = Mathf.Max(0.1f, tolerance);
        warmRange = Mathf.Max(1f, warmRange);
    }
}

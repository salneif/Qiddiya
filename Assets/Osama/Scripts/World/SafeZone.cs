using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// منطقة أمان (ملجأ ضوء): الأعداء لا يدخلونها ولا يقدرون يمسكون اللاعب داخلها.
///
/// الفكرة في اللعب: العلم يمشي معك بدائرة ملوّنة تحميك، لكن لحظة ما تتركه
/// عشان تشغّل شيئًا آخر تصير مكشوفًا — فتحتاج ملاجئ ثابتة تشغّلها بنفسك
/// (رافعة/زر) وتقدر تلجأ لها وأنت بلا علم.
///
/// نصف قطر الأمان يتزامن مع نصف قطر الدائرة الملوّنة، فيصير القانون واضحًا
/// للاعب بلا شرح: <b>اللون = أمان</b>.
///
/// التركيب:
///  - كائن فارغ في مركز الملجأ + هذا السكربت.
///  - (مفضّل) <see cref="WorldInteractor"/> على نفس الكائن، واربطه في خانة
///    "الدائرة الملوّنة" — عندها اللون يظهر ويختفي مع تشغيل/إطفاء الملجأ.
///  - اربط <see cref="Activate"/> أو <see cref="Toggle"/> بحدث
///    WorldLever.onActivated أو PuzzleButton.onPressed.
///
/// ⚠️ لا تضع SafeZone و <see cref="FlagZoneSize"/> على نفس الكائن —
/// كلاهما يتحكّم في WorldInteractor.Radius فيتنازعان عليه.
/// </summary>
public class SafeZone : MonoBehaviour
{
    /// <summary>من تطرده المنطقة. قابل للجمع، فمنطقة واحدة تقدر تطرد نوعًا أو الاثنين.</summary>
    [System.Flags]
    public enum Targets
    {
        [InspectorName("الفئران")] Rats = 1 << 0,
        [InspectorName("الوحوش")] Monsters = 1 << 1
    }

    /// <summary>كل المناطق الموجودة في المشهد (المفعّلة وغير المفعّلة).</summary>
    private static readonly List<SafeZone> zones = new List<SafeZone>();

    [Header("المنطقة")]
    [Tooltip("من تطرده هذه المنطقة؟ مثال: ضوء العلم يطرد الفئران فقط، فيبقى المهرج " +
             "قادرًا على مطاردتك رغم حملك العلم. وملاجئ الغرف تطرد الاثنين.")]
    [SerializeField] private Targets repels = Targets.Rats | Targets.Monsters;
    [Tooltip("نصف قطر الأمان (متر) — الأعداء لا يدخلونه")]
    [SerializeField] private float radius = 4f;
    [Tooltip("تجاهل فرق الارتفاع (أسطوانة بدل كرة) — يطابق Flat On Ground في الشيدر")]
    [SerializeField] private bool ignoreHeight = true;
    [Tooltip("تبدأ المنطقة شغّالة؟ (عادة مطفأة حتى يشغّلها اللاعب)")]
    [SerializeField] private bool startActive = false;

    [Header("الدائرة الملوّنة (اختياري)")]
    [Tooltip("WorldInteractor على نفس الكائن — نصف قطره يتزامن مع نصف قطر الأمان، " +
             "فتطابق الدائرة الملوّنة منطقة الأمان بالضبط. " +
             "خلّ Scale = (1,1,1) عليه حتى تنطبق البصمة على الأرض مع حدود اللون.")]
    [SerializeField] private WorldInteractor coloredZone;
    [Tooltip("مدة اتساع/انكماش الدائرة عند التشغيل (ثواني)")]
    [SerializeField] private float turnOnDuration = 0.6f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("لمسات")]
    [Tooltip("ضوء الملجأ — يُضاء مع التشغيل")]
    [SerializeField] private Light zoneLight;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت اشتعال الملجأ")]
    [SerializeField] private AudioClip turnOnSound;
    [Tooltip("صوت انطفائه")]
    [SerializeField] private AudioClip turnOffSound;

    [Header("أحداث")]
    [Tooltip("عند تشغيل الملجأ")]
    public UnityEvent onActivated;
    [Tooltip("عند إطفائه")]
    public UnityEvent onDeactivated;

    /// <summary>هل الملجأ شغّال الآن؟ (المطفأ لا يحمي)</summary>
    public bool IsActive { get; private set; }

    /// <summary>نصف قطر الأمان.</summary>
    public float Radius => radius;

    private Coroutine routine;

    private void Awake()
    {
        IsActive = startActive;
        if (zoneLight != null) zoneLight.enabled = IsActive;
        if (coloredZone != null) coloredZone.Radius = IsActive ? radius : 0f;
    }

    private void OnEnable() => zones.Add(this);

    private void OnDisable() => zones.Remove(this);

    /// <summary>هل هذه النقطة داخل هذا الملجأ تحديدًا؟ (بغض النظر عن تشغيله)</summary>
    public bool Contains(Vector3 point)
    {
        Vector3 d = point - transform.position;
        if (ignoreHeight) d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    /// <summary>هل تطرد هذه المنطقة النوع المطلوب؟</summary>
    public bool Repels(Targets target) => (repels & target) != 0;

    /// <summary>
    /// الملجأ الشغّال الذي يغطي هذه النقطة <b>ويطرد النوع المطلوب</b>، أو null.
    /// </summary>
    public static SafeZone ZoneAt(Vector3 point, Targets target)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            if (z != null && z.IsActive && z.Repels(target) && z.Contains(point)) return z;
        }
        return null;
    }

    /// <summary>هل هذه النقطة محميّة من النوع المطلوب؟ — يستخدمها الأعداء والفئران.</summary>
    public static bool IsSafe(Vector3 point, Targets target) => ZoneAt(point, target) != null;

    /// <summary>يشغّل الملجأ — اربطه بـ WorldLever.onActivated أو PuzzleButton.onPressed.</summary>
    public void Activate() => SetOn(true);

    /// <summary>يطفئ الملجأ.</summary>
    public void Deactivate() => SetOn(false);

    /// <summary>يبدّل حالة الملجأ.</summary>
    public void Toggle() => SetOn(!IsActive);

    /// <summary>يضبط حالة الملجأ صراحةً.</summary>
    public void SetOn(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;

        if (zoneLight != null) zoneLight.enabled = on;

        AudioClip clip = on ? turnOnSound : turnOffSound;
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);

        if (coloredZone != null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(GrowTo(on ? radius : 0f));
        }

        if (on) onActivated?.Invoke();
        else onDeactivated?.Invoke();
    }

    private IEnumerator GrowTo(float target)
    {
        float start = coloredZone.Radius;
        float t = 0f;
        while (t < turnOnDuration)
        {
            t += Time.deltaTime;
            float k = curve.Evaluate(turnOnDuration > 0f ? Mathf.Clamp01(t / turnOnDuration) : 1f);
            coloredZone.Radius = Mathf.Lerp(start, target, k);
            yield return null;
        }
        coloredZone.Radius = target;
        routine = null;
    }

    private void OnDrawGizmosSelected()
    {
        bool on = !Application.isPlaying || IsActive;
        Gizmos.color = on ? new Color(0.3f, 1f, 0.5f, 0.9f)
                          : new Color(0.3f, 1f, 0.5f, 0.3f);

        if (ignoreHeight)
        {
            // قرص مسطّح يمثّل الأسطوانة
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity,
                                          new Vector3(1f, 0.02f, 1f));
            Gizmos.DrawWireSphere(Vector3.zero, radius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

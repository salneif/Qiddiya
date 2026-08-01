using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// جرس/قرص يُضرب: كلما مرّ به الفاس المتأرجح يرنّ ويقفز لأعلى ثم يهبط ويستقر.
/// ويتكرر تلقائيًا مع كل مرور — ذهابًا وإيابًا — لأن الفاس يخرج من التريغر ويعود.
///
/// يعطي الممر إيقاعًا مسموعًا: اللاعب يسمع الرنّة فيعرف أن الفاس عبر ويحسب
/// وقته دون أن ينظر إليه.
///
/// التركيب:
///  - على القرص: Collider مفعّل عليه <b>Is Trigger</b> + هذا السكربت.
///  - على الفاس: Collider + Rigidbody (Is Kinematic) — بدون Rigidbody لا يلتقي
///    تريغران أبدًا.
///  - اربط <see cref="bellVisual"/> بمجسم القرص ليقفز.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ImpactBell : MonoBehaviour
{
    [Header("الضربة")]
    [Tooltip("وسم الضارب — اتركه فارغًا ليستجيب لأي شيء يدخله")]
    [SerializeField] private string strikerTag = "";
    [Tooltip("أقل زمن بين رنّتين (ثواني) — يمنع الرنّ المزدوج في المرور الواحد")]
    [SerializeField] private float cooldown = 0.25f;

    [Header("الارتداد")]
    [Tooltip("مجسم القرص الذي يقفز — اتركه فارغًا بلا حركة")]
    [SerializeField] private Transform bellVisual;
    [Tooltip("مقدار القفزة (محلي)")]
    [SerializeField] private Vector3 knockOffset = new Vector3(0f, 0.45f, 0f);
    [Tooltip("زمن الصعود — سريع جدًا، فالضربة مفاجئة")]
    [SerializeField] private float riseTime = 0.07f;
    [Tooltip("زمن الهبوط — أبطأ، فيبدو كأنه يستقر بثقله")]
    [SerializeField] private float fallTime = 0.4f;
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت الرنّة")]
    [SerializeField] private AudioClip hitSound;
    [Tooltip("تفاوت عشوائي في طبقة الصوت — يمنع الرنّات المتكررة من أن تبدو آلية")]
    [Range(0f, 0.4f)]
    [SerializeField] private float pitchVariation = 0.12f;

    [Header("أحداث")]
    [Tooltip("عند كل رنّة — اربطه بأي شيء (عدّاد، ضوء، لغز...)")]
    public UnityEvent onStruck;

    /// <summary>كم مرة ضُرب الجرس؟</summary>
    public int StrikeCount { get; private set; }

    private Vector3 restPosition;
    private float lastStrikeTime = -999f;
    private Coroutine routine;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (bellVisual != null) restPosition = bellVisual.localPosition;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(strikerTag) && !other.CompareTag(strikerTag)) return;
        if (Time.time - lastStrikeTime < cooldown) return;

        Strike();
    }

    /// <summary>يرنّ الجرس — يمكن استدعاؤه يدويًا أيضًا.</summary>
    public void Strike()
    {
        lastStrikeTime = Time.time;
        StrikeCount++;

        if (hitSound != null && audioSource != null)
        {
            // طبقة مختلفة كل رنّة فلا تتحول لضجيج متكرر ممل
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(hitSound);
        }

        if (bellVisual != null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(KnockRoutine());
        }

        onStruck?.Invoke();
    }

    private IEnumerator KnockRoutine()
    {
        Vector3 up = restPosition + knockOffset;

        // الصعود: من موضعه الحالي (لو ضُرب قبل أن يستقر يُستأنف من مكانه)
        yield return Move(bellVisual.localPosition, up, riseTime, null);

        // الهبوط: أبطأ ليبدو ثقيلًا
        yield return Move(up, restPosition, fallTime, fallCurve);

        bellVisual.localPosition = restPosition;
        routine = null;
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float duration, AnimationCurve curve)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            if (curve != null) k = curve.Evaluate(k);
            bellVisual.localPosition = Vector3.Lerp(from, to, k);
            yield return null;
        }
        bellVisual.localPosition = to;
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);

        if (bellVisual == null) return;
        Gizmos.DrawLine(bellVisual.position,
                        bellVisual.position + bellVisual.TransformVector(knockOffset));
    }
}

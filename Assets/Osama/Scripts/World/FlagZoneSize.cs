using System.Collections;
using UnityEngine;

/// <summary>
/// يتحكم في حجم الدائرة الملوّنة حول العلم بحجمين تحددهما أنت:
///  - العلم في الأرض  → نصف قطر <see cref="groundRadius"/>.
///  - العلم محمول     → يكبر بنعومة إلى <see cref="heldRadius"/>.
///
/// حُطّه على العلم نفسه (مع FlagItem و WorldInteractor).
/// العالم يظل أبيض/أسود طوال الوقت — الدائرة فقط تتغير حجمًا.
/// </summary>
[RequireComponent(typeof(WorldInteractor))]
public class FlagZoneSize : MonoBehaviour
{
    [Tooltip("العلم — يُلتقط تلقائيًا من نفس الكائن إذا تُرك فارغًا")]
    [SerializeField] private FlagItem flag;

    [Header("الحجمان (أنت تحددهما)")]
    [Tooltip("نصف قطر الدائرة والعلم في الأرض")]
    [SerializeField] private float groundRadius = 3f;
    [Tooltip("نصف قطر الدائرة والعلم محمول (أكبر شوي)")]
    [SerializeField] private float heldRadius = 6f;

    [Header("الحركة")]
    [Tooltip("مدة تكبّر/تصغّر الدائرة (ثواني)")]
    [SerializeField] private float growDuration = 0.8f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private WorldInteractor zone;
    private Coroutine routine;

    private void Awake()
    {
        zone = GetComponent<WorldInteractor>();
        if (flag == null) flag = GetComponent<FlagItem>();
    }

    private void OnEnable()
    {
        if (flag != null)
        {
            flag.PickedUp += OnPickedUp;
            flag.Placed += OnPlaced;
        }
    }

    private void OnDisable()
    {
        if (flag != null)
        {
            flag.PickedUp -= OnPickedUp;
            flag.Placed -= OnPlaced;
        }
    }

    private void Start()
    {
        // الحجم الابتدائي فورًا حسب حالة العلم
        zone.Radius = (flag != null && flag.IsHeld) ? heldRadius : groundRadius;
    }

    private void OnPickedUp() => AnimateTo(heldRadius);
    private void OnPlaced() => AnimateTo(groundRadius);

    private void AnimateTo(float target)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Grow(target));
    }

    private IEnumerator Grow(float target)
    {
        float start = zone.Radius;
        float t = 0f;
        while (t < growDuration)
        {
            t += Time.deltaTime;
            float k = curve.Evaluate(growDuration > 0f ? Mathf.Clamp01(t / growDuration) : 1f);
            zone.Radius = Mathf.Lerp(start, target, k);
            yield return null;
        }
        zone.Radius = target;
        routine = null;
    }
}

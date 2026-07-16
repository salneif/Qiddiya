using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// يحوّل العالم "بشويش": يكبّر/يصغّر نصف قطر WorldInteractor تدريجيًا مع الوقت،
/// فتتمدد بصمة التحويل كموجة تزحف على العالم (الشيدر يقرأ الـ Radius كل إطار
/// عبر InteractorManager — لا حاجة لأي تعديل على الشيدر).
///
/// حُطّه على نفس الكائن الذي عليه WorldInteractor، ثم نادِ Expand() / Shrink()
/// من أي حدث (تريغر، لمبة، التقاط آيتم...) أو استخدم زر الاختبار.
/// </summary>
[RequireComponent(typeof(WorldInteractor))]
public class InteractorRadiusAnimator : MonoBehaviour
{
    [Header("نصف القطر")]
    [Tooltip("نصف القطر عند التمدد الكامل — اجعله كبيرًا بما يكفي ليغطي المنطقة/المستوى")]
    [SerializeField] private float expandedRadius = 40f;
    [Tooltip("نصف القطر عند الانكماش (0 = يختفي التحويل تمامًا)")]
    [SerializeField] private float collapsedRadius = 0f;

    [Header("الحركة")]
    [Tooltip("مدة التحول الكامل (ثواني) — كبّرها ليكون التحول أبطأ")]
    [SerializeField] private float duration = 3f;
    [Tooltip("شكل التسارع: بداية هادئة ثم اندفاع ثم استقرار")]
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("البداية")]
    [Tooltip("يبدأ منكمشًا ثم يتمدد تلقائيًا عند التشغيل")]
    [SerializeField] private bool expandOnStart = false;

    [Header("اختبار (في المحرر)")]
    [Tooltip("زر لوحة مفاتيح لتبديل التمدد/الانكماش أثناء التجربة (None لتعطيله)")]
    [SerializeField] private Key testToggleKey = Key.G;

    [Header("أحداث")]
    [Tooltip("يُستدعى عند اكتمال التمدد")]
    public UnityEvent onExpandComplete;
    [Tooltip("يُستدعى عند اكتمال الانكماش")]
    public UnityEvent onShrinkComplete;

    private WorldInteractor interactor;
    private Coroutine routine;
    private bool expanded;

    /// <summary>هل هو متمدد حاليًا (أو في طريقه للتمدد)؟</summary>
    public bool IsExpanded => expanded;

    private void Awake()
    {
        interactor = GetComponent<WorldInteractor>();
    }

    private void Start()
    {
        if (expandOnStart)
        {
            interactor.Radius = collapsedRadius;
            Expand();
        }
    }

    private void Update()
    {
        if (testToggleKey != Key.None && Keyboard.current != null &&
            Keyboard.current[testToggleKey].wasPressedThisFrame)
            Toggle();
    }

    /// <summary>يمدّد المنطقة تدريجيًا — العالم يتحول بشويش.</summary>
    public void Expand() => AnimateTo(expandedRadius, expand: true);

    /// <summary>يرجّع العالم لوضعه تدريجيًا.</summary>
    public void Shrink() => AnimateTo(collapsedRadius, expand: false);

    /// <summary>يبدّل بين التمدد والانكماش.</summary>
    public void Toggle()
    {
        if (expanded) Shrink();
        else Expand();
    }

    private void AnimateTo(float target, bool expand)
    {
        expanded = expand;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Animate(target, expand));
    }

    private IEnumerator Animate(float target, bool expand)
    {
        float start = interactor.Radius;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = curve.Evaluate(duration > 0f ? Mathf.Clamp01(t / duration) : 1f);
            interactor.Radius = Mathf.Lerp(start, target, k);
            yield return null;
        }
        interactor.Radius = target;
        routine = null;

        if (expand) onExpandComplete?.Invoke();
        else onShrinkComplete?.Invoke();
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// تأثير تفاعلي احترافي للأزرار: تكبير ناعم + صوت عند المرور بالماوس أو
/// التحديد بلوحة المفاتيح/الجويستيك. يُركّب على كل زر في القائمة.
/// </summary>
public class UIButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    [Header("التكبير")]
    [Tooltip("نسبة التكبير عند التحويم/التحديد")]
    [SerializeField] private float hoverScale = 1.12f;
    [Tooltip("سرعة نعومة التكبير")]
    [SerializeField] private float scaleSpeed = 12f;

    [Header("العنصر المتأثر")]
    [Tooltip("العنصر الذي يتكبّر — إن تُرك فارغًا يُستخدم هذا الكائن")]
    [SerializeField] private RectTransform target;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hoverSound;

    private Vector3 baseScale;
    private bool highlighted;

    private void Awake()
    {
        if (target == null) target = transform as RectTransform;
        baseScale = target != null ? target.localScale : Vector3.one;
    }

    private void OnDisable()
    {
        highlighted = false;
        if (target != null) target.localScale = baseScale;
    }

    private void Update()
    {
        if (target == null) return;
        Vector3 goal = highlighted ? baseScale * hoverScale : baseScale;
        target.localScale = Vector3.Lerp(target.localScale, goal, Time.unscaledDeltaTime * scaleSpeed);
    }

    private void SetHighlight(bool on)
    {
        if (on && !highlighted && hoverSound != null && audioSource != null)
            audioSource.PlayOneShot(hoverSound);
        highlighted = on;
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHighlight(true);
    public void OnPointerExit(PointerEventData eventData) => SetHighlight(false);
    public void OnSelect(BaseEventData eventData) => SetHighlight(true);
    public void OnDeselect(BaseEventData eventData) => SetHighlight(false);
}

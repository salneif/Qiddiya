using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// صوت وحركة الزر عند المرور فوقه أو تحديده. بديل HoverScript القديم الذي كان
/// يعمل بالماوس فقط — هذا يتفاعل مع تحديد الكنترولر أيضًا (OnSelect).
/// ضعه على كل زر. لا يحتاج AudioSource — يشغّل عبر AudioManager.
/// </summary>
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("الأصوات (فارغ = صوت AudioManager الافتراضي)")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0.1f, 3f)] private float hoverPitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float clickPitch = 1f;

    [Header("التكبير")]
    [SerializeField] private bool scaleOnHighlight = true;
    [SerializeField] private float highlightScale = 1.08f;
    [SerializeField] private float scaleSpeed = 12f;

    private Vector3 baseScale;
    private bool highlighted;

    private void Awake() => baseScale = transform.localScale;

    private void OnDisable()
    {
        highlighted = false;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (!scaleOnHighlight) return;

        Vector3 target = highlighted ? baseScale * highlightScale : baseScale;
        // unscaledDeltaTime حتى تعمل الحركة أثناء الإيقاف (Time.timeScale = 0)
        transform.localScale = Vector3.Lerp(transform.localScale, target, scaleSpeed * Time.unscaledDeltaTime);
    }

    // --- ماوس ---
    public void OnPointerEnter(PointerEventData eventData) => Highlight();
    public void OnPointerExit(PointerEventData eventData) => Unhighlight();
    public void OnPointerClick(PointerEventData eventData) => Click();

    // --- كنترولر / كيبورد ---
    public void OnSelect(BaseEventData eventData) => Highlight();
    public void OnDeselect(BaseEventData eventData) => Unhighlight();
    public void OnSubmit(BaseEventData eventData) => Click();

    private void Highlight()
    {
        if (highlighted) return; // لا نكرّر الصوت إذا كان الماوس والتحديد على نفس الزر

        highlighted = true;
        Play(hoverClip, AudioManager.Instance != null ? AudioManager.Instance.DefaultHoverClip : null, hoverPitch);
    }

    private void Unhighlight() => highlighted = false;

    private void Click()
    {
        Play(clickClip, AudioManager.Instance != null ? AudioManager.Instance.DefaultClickClip : null, clickPitch);
    }

    private static void Play(AudioClip specific, AudioClip fallback, float pitch)
    {
        AudioClip clip = specific != null ? specific : fallback;
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(clip, pitch);
    }
}

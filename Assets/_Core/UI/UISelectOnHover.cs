using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// يجعل الماوس والكنترولر يتشاركان نفس الإبراز.
///
/// بدونه: الكنترولر يترك الزر في حالة Selected، وعند تمرير الماوس على زر آخر
/// يدخل هذا في حالة Highlighted — فيظهر زرّان بارزان في نفس الوقت.
/// معه: المرور بالماوس ينقل التحديد نفسه، فيبقى زر واحد بارز دائمًا.
///
/// يُركَّب تلقائيًا على كل زر داخل UIPanel (خانة Auto Sync Mouse And Controller).
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UISelectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    [Tooltip("يشغّل صوت التحويم المربوط في EventTrigger عند التنقّل بالكنترولر أيضًا")]
    [SerializeField] private bool playHoverEventOnControllerSelect = true;

    /// <summary>العنصر الذي يقف المؤشر فوقه الآن — يستخدمه MenuManager ليعرف
    /// أي تحديد مفروض يجب إسقاطه حين ينتقل اللاعب للماوس.</summary>
    public static GameObject Hovered { get; private set; }

    private Selectable selectable;
    private EventTrigger eventTrigger;
    private bool pointerInside;
    private bool selectOnHover = true;

    /// <summary>
    /// يُطفئ نقل التحديد بالتحويم مع إبقاء تتبّع <see cref="Hovered"/> شغّالًا.
    /// يستخدمه UIPanel للعناصر التي يُنفّذ تحديدها أمرًا (L1_Button / R1_Button).
    /// </summary>
    public void SetSelectOnHover(bool value) => selectOnHover = value;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        eventTrigger = GetComponent<EventTrigger>();
    }

    private void OnDisable()
    {
        pointerInside = false;
        if (Hovered == gameObject) Hovered = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        Hovered = gameObject;

        if (!selectOnHover) return;
        if (EventSystem.current == null) return;
        if (selectable == null || !selectable.IsInteractable()) return;
        if (EventSystem.current.currentSelectedGameObject == gameObject) return;

        EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (Hovered == gameObject) Hovered = null;

        // الماوس غادر الزر ولا يزال هو المحدَّد → يبقى بارزًا/منتفخًا بلا سبب.
        // نلغي التحديد فقط إن كان الماوس هو الجهاز القائد، حتى لا نكسر الكنترولر.
        if (EventSystem.current == null) return;
        if (eventData != null && eventData.dragging) return;   // سحب سلايدر: المؤشر يخرج والعنصر ما زال قيد الاستخدام
        if (!MenuManager.PointerIsDriving) return;
        if (EventSystem.current.currentSelectedGameObject != gameObject) return;

        EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// أصوات التحويم في المشهد مربوطة على EventTrigger > PointerEnter، أي بالماوس فقط.
    /// عند التحديد بالكنترولر ننادي الـ EventTrigger وحده — لا نُطلق الحدث على الكائن كله،
    /// لأن ذلك يجعل الـ Button يظن أن المؤشر فوقه فيعلق في حالة Highlighted بلا خروج.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        if (!playHoverEventOnControllerSelect) return;
        if (pointerInside) return;                    // الماوس أطلقه أصلًا — لا نكرّر الصوت
        if (MenuManager.SelectionIsSilent) return;    // تحديد فرضه فتح/إغلاق لوحة، لا تنقّل اللاعب
        if (eventTrigger == null || EventSystem.current == null) return;

        eventTrigger.OnPointerEnter(new PointerEventData(EventSystem.current));
    }
}

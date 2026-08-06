using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// لوحة واجهة واحدة (Main-Menu / Settings_UI / Credits-UI / Pause_Holder).
/// تُركّب على الكائن الجذر للّوحة. لا تفتحها بنفسك — استخدم MenuManager.Open/Back
/// حتى يبقى ترتيب الرجوع وتحديد أزرار الكنترولر صحيحًا.
/// </summary>
public class UIPanel : MonoBehaviour
{
    [Header("الكنترولر")]
    [Tooltip("الزر الذي يُحدَّد تلقائيًا عند فتح اللوحة — ضروري ليعمل الكنترولر")]
    [SerializeField] private GameObject firstSelected;

    [Header("السلوك")]
    [Tooltip("يخفي اللوحة السابقة عند فتح هذه (أطفئه للّوحات التي تُعرض فوق غيرها)")]
    [SerializeField] private bool hidePrevious = true;

    [Tooltip("يوقف الزمن أثناء فتح اللوحة (للـ Pause فقط)")]
    [SerializeField] private bool pauseGameWhileOpen = false;

    [Tooltip("يمنع الرجوع بـ ESC / زر B من هذه اللوحة (للوحة الجذر مثل القائمة الرئيسية)")]
    [SerializeField] private bool blockBack = false;

    [Header("الأزرار")]
    [Tooltip("يركّب UISelectOnHover تلقائيًا على كل زر، فيتشارك الماوس والكنترولر إبرازًا واحدًا")]
    [SerializeField] private bool autoSyncMouseAndController = true;

    [Tooltip("إن كان كليب Selected فارغًا وكليب Highlighted مليئًا، يجعل حالة Selected تشغّل Highlighted")]
    [SerializeField] private bool treatSelectedAsHighlighted = true;

    [Header("أحداث")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    /// <summary>آخر زر كان محددًا قبل مغادرة هذه اللوحة — يُستعاد عند الرجوع إليها.</summary>
    public GameObject LastSelected { get; set; }

    public GameObject FirstSelected => firstSelected;
    public bool HidePrevious => hidePrevious;
    public bool PauseGameWhileOpen => pauseGameWhileOpen;
    public bool BlockBack => blockBack;

    /// <summary>
    /// صورة الزر قبل أن يلمسه أي أنميشن — الحجم والموضع والزخارف المخفية.
    /// تُلتقط مرة واحدة فقط، ولهذا يجب أن تكون قبل أول تحويم على الإطلاق.
    /// </summary>
    private struct ButtonBaseline
    {
        public Animator animator;
        public RectTransform rect;
        public Vector3 localScale;
        public Vector2 anchoredPosition;
        public GameObject[] decorations;   // أبناء الزر (Big-Marker وغيره)
        public bool[] decorationActive;
    }

    private ButtonBaseline[] baselines;
    private bool baselineCaptured;

    private void Awake() => CaptureBaseline();

    /// <summary>
    /// يلتقط الحالة الطبيعية لكل زر. تُنادى من MenuManager قبل إخفاء اللوحات عند بدء
    /// المشهد، لأن Awake لا يعمل على لوحة مطفأة أصلًا في السين.
    /// </summary>
    public void CaptureBaseline()
    {
        if (baselineCaptured) return;
        baselineCaptured = true;

        Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
        baselines = new ButtonBaseline[selectables.Length];

        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];

            if (autoSyncMouseAndController)
            {
                UISelectOnHover hover = selectable.GetComponent<UISelectOnHover>();
                if (hover == null) hover = selectable.gameObject.AddComponent<UISelectOnHover>();

                // يُركَّب على الجميع ليتتبّع ما تحت المؤشر، لكن نقل التحديد يُطفأ
                // للعناصر التي يُنفّذ تحديدها أمرًا
                hover.SetSelectOnHover(!SelectionHasSideEffects(selectable));
            }

            if (treatSelectedAsHighlighted) RedirectEmptySelectedClip(selectable);

            var rect = selectable.transform as RectTransform;

            // كل أبناء الزر — كليب Highlighted يشغّل بعضها (Big-Marker) وكليب Normal
            // الفارغ لا يطفئها، فنحتاج قيمتها الأصلية بأنفسنا.
            Transform[] children = selectable.GetComponentsInChildren<Transform>(true);
            int count = children.Length - 1; // بدون الزر نفسه
            var decorations = new GameObject[count];
            var decorationActive = new bool[count];
            int d = 0;
            foreach (Transform child in children)
            {
                if (child == selectable.transform) continue;
                decorations[d] = child.gameObject;
                decorationActive[d] = child.gameObject.activeSelf;
                d++;
            }

            baselines[i] = new ButtonBaseline
            {
                animator = selectable.GetComponent<Animator>(),
                rect = rect,
                localScale = selectable.transform.localScale,
                anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero,
                decorations = decorations,
                decorationActive = decorationActive,
            };
        }
    }

    /// <summary>
    /// هل التحديد وحده يُنفّذ أمرًا على هذا العنصر؟
    ///
    /// L1_Button و R1_Button في الإعدادات عليهما `EventTrigger > Select → SetActive`،
    /// أي أن **مجرد تحديدهما يبدّل التبويب بلا أي كليك**. لو ركّبنا UISelectOnHover
    /// عليهما لصار مرور المؤشر فوقهما يبدّل التبويبات وحده — وهو ليس ما يريده اللاعب.
    /// فنتركهما بلا تحديد بالتحويم؛ يعملان بالكليك وبالكنترولر فقط.
    /// </summary>
    private static bool SelectionHasSideEffects(Selectable selectable)
    {
        EventTrigger trigger = selectable.GetComponent<EventTrigger>();
        if (trigger == null || trigger.triggers == null) return false;

        foreach (EventTrigger.Entry entry in trigger.triggers)
        {
            if (entry == null || entry.eventID != EventTriggerType.Select) continue;
            if (entry.callback != null && entry.callback.GetPersistentEventCount() > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// حالة Selected تسبق Highlighted في Selectable:
    ///   Pressed ← Selected ← Highlighted ← Normal
    /// وكليب Selected في كنترولرات الأزرار **فارغ** بينما كل التكبير في Highlighted.
    /// فبمجرد أن يصير الزر محدَّدًا (بالكنترولر، أو بالماوس عبر UISelectOnHover)
    /// يدخل حالة بلا أي أنميشن — فيبدو الزر "ناشفًا" لا يكبر إطلاقًا.
    ///
    /// الحل: نجعل زناد Selected يشير لحالة Highlighted نفسها. ولا نفعلها إلا إذا كان
    /// Selected فارغًا فعلًا وHighlighted مليئًا — لأن L1_Button و R1_Button معكوسان
    /// تمامًا (أنميشنهما في Selected) وتعميم التحويل يكسرهما.
    /// </summary>
    private static void RedirectEmptySelectedClip(Selectable selectable)
    {
        if (selectable.transition != Selectable.Transition.Animation) return;

        Animator animator = selectable.GetComponent<Animator>();
        RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
        if (controller == null) return;

        AnimationTriggers triggers = selectable.animationTriggers;
        if (string.IsNullOrEmpty(triggers.highlightedTrigger)) return;
        if (triggers.selectedTrigger == triggers.highlightedTrigger) return;

        AnimationClip selectedClip = null;
        AnimationClip highlightedClip = null;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null) continue;
            if (clip.name == triggers.selectedTrigger) selectedClip = clip;
            else if (clip.name == triggers.highlightedTrigger) highlightedClip = clip;
        }

        if (highlightedClip == null || highlightedClip.empty) return;  // لا شيء نعرضه أصلًا
        if (selectedClip != null && !selectedClip.empty) return;       // للزر حالة Selected حقيقية — لا نلمسها

        triggers.selectedTrigger = triggers.highlightedTrigger;
    }

    /// <summary>تُستدعى من MenuManager فقط.</summary>
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf == visible) return;

        // نُعيد الأزرار لحالتها قبل الإطفاء حتى لا يُحفظ الحجم المنتفخ على الترانسفورم
        if (!visible) RestoreBaseline();

        managedToggle = true;
        gameObject.SetActive(visible);
        managedToggle = false;
    }

    /// <summary>حقيقي حين يكون MenuManager هو من يُظهر/يُخفي اللوحة الآن.</summary>
    private bool managedToggle;

    /// <summary>
    /// شبكة أمان: عدة أزرار في المشهد تُظهر/تُخفي اللوحات بـ`GameObject.SetActive`
    /// مباشرةً بدل `MenuManager.Open/Back` (مثل `Back_Button1` في الإعدادات).
    /// عندها لا تعمل ResetVisualStates فيبقى الزر متجمّدًا على حالة Highlighted —
    /// أي "يولّع" بلا سبب. نُصلح ذلك هنا مهما كان من غيّر الحالة.
    /// </summary>
    private void OnEnable()
    {
        ResetVisualStates();
        if (!managedToggle) StartCoroutine(ResetAfterAnimatorBinds());
    }

    private IEnumerator ResetAfterAnimatorBinds()
    {
        yield return null;      // الأنميتور يكمل ربط نفسه بعد إطار التفعيل
        ResetVisualStates();
    }

    private void OnDisable()
    {
        RestoreBaseline();

        // أُخفيت من خارج MenuManager: نُخرجها من ترتيب اللوحات وإلا ظنّها مفتوحة
        // ورفض إعادة فتحها (Open يعود فورًا حين Current == panel).
        if (!managedToggle && MenuManager.Instance != null)
            MenuManager.Instance.NotifyPanelClosedExternally(this);
    }

    public void RaiseOpened() => onOpened?.Invoke();
    public void RaiseClosed() => onClosed?.Invoke();

    /// <summary>
    /// يعيد كل أزرار اللوحة لحالتها الطبيعية.
    ///
    /// لماذا لا يكفي animator.Rebind() وحده:
    /// كليب Highlighted في كنترولرات الأزرار يكتب m_LocalScale، بينما كليب Normal
    /// **فارغ تمامًا** — فيونيتي يرجّع الحجم من "القيمة الافتراضية" التي التقطها
    /// الأنميتور وقت الربط، لا من الكليب. وإذا نادينا Rebind والزر لا يزال متجمّدًا
    /// على حجم Highlighted، يلتقط Rebind الحجم المنتفخ كقيمة افتراضية جديدة فيعلق
    /// الزر منتفخًا للأبد.
    ///
    /// الترتيب الصحيح: نُرجع الترانسفورم للقيمة الأصلية **أولًا**، ثم Rebind ليلتقط
    /// القيمة الصحيحة، ثم Update(0) ليطبّق حالة Normal بلا وميض.
    /// </summary>
    public void ResetVisualStates()
    {
        CaptureBaseline();
        RestoreBaseline();

        if (baselines == null) return;

        foreach (ButtonBaseline baseline in baselines)
        {
            Animator animator = baseline.animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                continue;

            animator.Rebind();   // الآن يلتقط الحجم الصحيح
            animator.Update(0f); // يطبّق الحالة الافتراضية فورًا بدون وميض
        }
    }

    /// <summary>يعيد الحجم والموضع والزخارف لقيمها الأصلية بلا لمس الأنميتور.</summary>
    private void RestoreBaseline()
    {
        if (baselines == null) return;

        foreach (ButtonBaseline baseline in baselines)
        {
            if (baseline.rect == null) continue;

            baseline.rect.localScale = baseline.localScale;
            baseline.rect.anchoredPosition = baseline.anchoredPosition;

            if (baseline.decorations == null) continue;
            for (int i = 0; i < baseline.decorations.Length; i++)
            {
                GameObject decoration = baseline.decorations[i];
                if (decoration != null && decoration.activeSelf != baseline.decorationActive[i])
                    decoration.SetActive(baseline.decorationActive[i]);
            }
        }
    }

    /// <summary>اربطه بزر Back في الـ Inspector، أو استخدم MenuManager.Instance.Back().</summary>
    public void CloseSelf()
    {
        if (MenuManager.Instance != null)
            MenuManager.Instance.Back();
        else
            SetVisible(false);
    }
}

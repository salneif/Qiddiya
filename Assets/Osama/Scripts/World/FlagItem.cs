using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// العلم القابل للحمل (بأسلوب CTF): يلمسه اللاعب فيلتصق فيه ويُحمل فوقه،
/// ولا يمكن تركه إلا في مقبس مخصص (<see cref="FlagSocket"/>) — لا رمي حر.
///
/// عند الالتقاط/الوضع يطلق أحداثًا يستخدمها <see cref="WorldFlagDirector"/>
/// لقلب العالم أبيض/أسود وتغيير الموسيقى وفتح البوابات... إلخ.
///
/// حُط على العلم: Collider (Is Trigger) يغطيه + هذا السكربت.
/// وإذا أردت دائرة ملوّنة تمشي مع العلم: أضف عليه WorldInteractor أيضًا.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlagItem : MonoBehaviour
{
    [Header("الالتقاط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("إزاحة العلم عن مركز اللاعب أثناء الحمل فوق الرأس — تُستخدم فقط إذا أطفأت Carry On Back")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 1.8f, -0.15f);

    [Header("الحمل على الظهر")]
    [Tooltip("يُحمل العلم على ظهر اللاعب بدل فوق رأسه — ما يغطي الشخصية ولا يزعج. " +
             "أطفئه ليرجع فوق الرأس (Hold Offset).")]
    [SerializeField] private bool carryOnBack = true;
    [Tooltip("موضع العلم على الظهر بالنسبة للاعب: Y = الارتفاع، Z سالب = وراه. " +
             "لو طلع على جنبه بدل ظهره، انقل الرقم من Z إلى X.")]
    [SerializeField] private Vector3 backOffset = new Vector3(0f, 1.1f, -0.45f);
    [Tooltip("ميلان العلم على الظهر (درجات)")]
    [SerializeField] private Vector3 backRotation = Vector3.zero;
    [Tooltip("اسم كائن داخل اللاعب يُعلَّق عليه العلم — الشنطة أو عظمة الظهر مثلًا. " +
             "عندها يمشي العلم مع الأنميشن ويثبت مكانه مهما كان دوران اللاعب، " +
             "و Back Offset يصير بالنسبة له. اتركه فارغًا ليُعلَّق على اللاعب نفسه.")]
    [SerializeField] private string attachToChildNamed = "";
    [Tooltip("حجم العلم وهو محمول نسبةً لحجمه الأصلي. يرجع لحجمه كاملًا لحظة غرسه.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float heldScale = 0.5f;

    [Header("العودة عند إعادة الضبط")]
    [Tooltip("نقطة عودة العلم عند الموت — عادة مقبسه. " +
             "اتركها فارغة ليعود لموضعه الذي بدأت عليه اللعبة.")]
    [SerializeField] private Transform returnPoint;
    [Tooltip("يعود تلقائيًا لحظة موت حامله، بلا حاجة لربطه بـ On Respawn. " +
             "بدونه يبقى العلم في يد اللاعب بعد البعث فيظل العالم في حالة 'العلم محمول' " +
             "— الفئران مختفية والأبواب مفتوحة.")]
    [SerializeField] private bool returnOnHolderDeath = true;

    [Header("الصوت")]
    [Tooltip("مصدر الصوت — يُلتقط تلقائيًا من نفس كائن العلم إذا تُرك فارغًا")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت التقاط العلم")]
    [SerializeField] private AudioClip pickupSound;
    [Tooltip("صوت غرس العلم في مقبسه")]
    [SerializeField] private AudioClip placeSound;

    [Header("أحداث")]
    [Tooltip("عند التقاط العلم (افتح بوابة/شغّل مؤثر...)")]
    public UnityEvent onPickedUp;
    [Tooltip("عند وضع العلم في مقبسه")]
    public UnityEvent onPlaced;

    /// <summary>أحداث برمجية يستخدمها WorldFlagDirector.</summary>
    public event Action PickedUp;
    public event Action Placed;

    /// <summary>هل العلم محمول الآن؟</summary>
    public bool IsHeld { get; private set; }

    private Collider pickupCollider;
    private Vector3 startScale;              // حجمه الأصلي في العالم — يرجع له عند الغرس
    private bool hasStartScale;
    private FlagItem primary;                // أول FlagItem على الكائن — مصدر إعدادات الحمل
    private Vector3 startPosition;
    private Quaternion startRotation;
    private PlayerKillable holderKillable;   // حامل العلم الحالي، لمراقبة موته
    private bool locked;                     // مزروع نهائيًا في قاعدته — لا يُلتقط ولا يرجع
    private bool plantAtStartPose;           // يُغرس حيث وُضع في المحرر لا في نقطة المقبس

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        startPosition = transform.position;
        startScale = transform.lossyScale;
        hasStartScale = true;
        primary = GetComponents<FlagItem>()[0];
        startRotation = transform.rotation;
    }

    /// <summary>
    /// يعيد العلم لنقطة عودته ويفلته من يد اللاعب — اربطه بـ PlayerKillable.onRespawn.
    /// لا يطلق أحداث الوضع إلا إن كان محمولًا فعلًا، حتى لا يُعاد ضبط العالم
    /// في كل موتة والعلم أصلًا في مكانه.
    /// </summary>
    public void ReturnHome()
    {
        // العلم المزروع نهائيًا لا يرجع ولا يصير قابلًا للالتقاط من جديد
        if (locked) return;

        bool wasHeld = IsHeld;
        IsHeld = false;

        Detach();
        transform.SetPositionAndRotation(
            returnPoint != null ? returnPoint.position : startPosition,
            returnPoint != null ? returnPoint.rotation : startRotation);

        pickupCollider.enabled = true;

        if (!wasHeld) return;
        Play(placeSound);
        onPlaced?.Invoke();
        Placed?.Invoke();
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (locked || IsHeld || !other.CompareTag(playerTag)) return;
        PickUp(other.transform);
    }

    /// <summary>
    /// يعيد العلم لحظة موت حامله. بلا هذا يبقى العلم في يده بعد البعث،
    /// فيظل العالم كله في حالة "العلم محمول" رغم أن اللاعب عاد لنقطة البداية.
    /// </summary>
    private void Update()
    {
        if (locked || !IsHeld || !returnOnHolderDeath) return;
        if (holderKillable != null && holderKillable.IsDead) ReturnHome();
    }

    private void PickUp(Transform player)
    {
        IsHeld = true;
        holderKillable = player.GetComponentInParent<PlayerKillable>();
        pickupCollider.enabled = false; // لا يُلتقط مرتين ولا يعيق الحركة

        transform.SetParent(FindAnchor(player));
        ApplyCarryPose();

        Play(pickupSound);
        onPickedUp?.Invoke();
        PickedUp?.Invoke();
    }

    /// <summary>
    /// العلم يبقى في يد اللاعب حتى لو مات — اربطها بـ<c>Checkpoint.On Activated</c>
    /// لتخفيف العقوبة بعد نقطة معيّنة في المرحلة: قبلها الموت يكلّفك العلم،
    /// وبعدها يكلّفك المسافة فقط.
    /// </summary>
    public void KeepOnDeath() => returnOnHolderDeath = false;

    /// <summary>يرجع العلم لموضعه عند موت حامله — السلوك الافتراضي.</summary>
    public void ReturnHomeOnDeath() => returnOnHolderDeath = true;

    /// <summary>
    /// يلصق العلم بلاعب فورًا بلا لمس التريغر — يستخدمها <see cref="FlagCarry"/>
    /// لاستعادة الحمل عند بداية سين جديد، فيدخل اللاعب وهو حامله.
    /// تطلق أحداث الالتقاط كالمعتاد، فتكبر الدائرة وتتبدّل الموسيقى وحدها.
    /// </summary>
    public void AttachTo(Transform player)
    {
        if (locked || IsHeld || player == null) return;
        PickUp(player);
    }

    /// <summary>
    /// يقفل العلم في موضعه الحالي فلا يُلتقط مرة أخرى — للعلم المزروع في قاعدته
    /// نهائيًا. (تعطيل الكولايدر وحده لا يكفي لأن ReturnHome يعيد تفعيله.)
    ///
    /// يقفل <b>كل</b> نسخ FlagItem على نفس الكائن: لو وُجدت نسخة ثانية بالغلط، كانت
    /// تبقى "حاملة" بعد الزرع، فترجّع العلم وتعيد تفعيل التقاطه أول ما يموت اللاعب.
    /// </summary>
    public void LockInPlace()
    {
        foreach (var f in GetComponents<FlagItem>()) f.LockSelf();
    }

    private void LockSelf()
    {
        locked = true;
        IsHeld = false;
        holderKillable = null;
        Detach();
        if (pickupCollider == null) pickupCollider = GetComponent<Collider>();
        if (pickupCollider != null) pickupCollider.enabled = false;
    }

    /// <summary>
    /// يُغرس العلم حيث وُضع في المحرر بدل نقطة المقبس — ينادى من <see cref="FlagSocket"/>
    /// إذا كان العلم واقفًا عند قاعدته في السين: ما تشوفه في المحرر هو ما يصير في اللعب.
    /// </summary>
    public void UseStartPoseWhenPlanted() => plantAtStartPose = true;

    /// <summary>هل يُغرس هذا العلم في مكانه الأصلي في المحرر؟</summary>
    public bool PlantsAtStartPose => plantAtStartPose;

    /// <summary>
    /// يضع العلم في موضع زرعه فورًا ولو لم يكن محمولًا — للتجربة من الـ Inspector
    /// (<see cref="FlagBase"/>). إن كان محمولًا يمر بالوضع العادي فتنطلق أحداثه.
    /// </summary>
    public void SnapToPlanted(Transform point)
    {
        if (locked) return;
        if (IsHeld) { PlaceAt(point); return; }

        Detach();
        MoveToPlantPose(point);
    }

    /// <summary>
    /// وضع العلم على اللاعب: على ظهره وأصغر، أو فوق رأسه إن أطفأت الحمل على الظهر.
    /// الإعدادات من <b>أول</b> FlagItem على الكائن دائمًا — بريفابات الأعلام فيها نسخة
    /// ثانية، وبدون هذا كانت قيمها الافتراضية تكتب فوق ما تضبطه في الأولى.
    /// </summary>
    private void ApplyCarryPose()
    {
        var s = primary != null ? primary : this;

        if (s.carryOnBack)
        {
            transform.localPosition = s.backOffset;
            transform.localRotation = Quaternion.Euler(s.backRotation);
        }
        else
        {
            transform.localPosition = s.holdOffset;
            transform.localRotation = Quaternion.identity;
        }

        // من الحجم الأصلي لا الحالي، فتطبيقه مرتين (نسختا FlagItem) يعطي نفس النتيجة
        if (hasStartScale) SetWorldScale(startScale * s.heldScale);
    }

    /// <summary>
    /// الكائن الذي يُعلَّق عليه العلم داخل اللاعب: المطابق بالاسم إن حُدِّد، وإلا اللاعب نفسه.
    /// المطابقة تتجاهل حالة الأحرف وتقبل جزءًا من الاسم، فـ"شنطة"/"Backpack" تكفي.
    /// </summary>
    private Transform FindAnchor(Transform player)
    {
        var s = primary != null ? primary : this;
        if (string.IsNullOrWhiteSpace(s.attachToChildNamed)) return player;

        string wanted = s.attachToChildNamed.Trim().ToLowerInvariant();
        Transform root = player.root != null ? player.root : player;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n == wanted) return t;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.ToLowerInvariant().Contains(wanted)) return t;
        }

        Debug.LogWarning($"[FlagItem] ما لقيت كائنًا اسمه \"{s.attachToChildNamed}\" داخل اللاعب — " +
                         "عُلِّق العلم على اللاعب نفسه.", this);
        return player;
    }

    /// <summary>يضبط حجم العلم الفعلي في العالم مهما كان حجم أبيه.</summary>
    private void SetWorldScale(Vector3 world)
    {
        Vector3 p = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(Div(world.x, p.x), Div(world.y, p.y), Div(world.z, p.z));
    }

    private static float Div(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    /// <summary>يفك العلم من اللاعب ويرجّع حجمه الأصلي.</summary>
    private void Detach()
    {
        transform.SetParent(null);
        if (hasStartScale) transform.localScale = startScale;
    }

    /// <summary>موضع الزرع: مكانه في المحرر لنسخ الهب، وإلا نقطة المقبس.</summary>
    private void MoveToPlantPose(Transform point)
    {
        if (plantAtStartPose || point == null)
            transform.SetPositionAndRotation(startPosition, startRotation);
        else
            transform.SetPositionAndRotation(point.position, point.rotation);
    }

    /// <summary>
    /// يضع العلم في نقطة المقبس — ينادى حصرًا من FlagSocket
    /// (لا توجد طريقة أخرى لترك العلم).
    /// </summary>
    public void PlaceAt(Transform point)
    {
        if (locked || !IsHeld) return;

        IsHeld = false;
        Detach();
        MoveToPlantPose(point);
        pickupCollider.enabled = true; // يمكن التقاطه من جديد

        Play(placeSound);
        onPlaced?.Invoke();
        Placed?.Invoke();
    }
}

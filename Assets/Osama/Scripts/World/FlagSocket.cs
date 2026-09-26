using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// مقبس العلم — المنطقة الوحيدة التي يمكن ترك العلم فيها (مثل نقاط CTF).
/// عندما يدخل اللاعب المنطقة وهو حامل العلم: يضعه تلقائيًا أو بزر تفاعل.
///
/// حُط على كائن فارغ فيه Collider (Is Trigger) يغطي منطقة الوضع.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlagSocket : MonoBehaviour
{
    [Header("الهدف")]
    [Tooltip("العلم الذي يقبله هذا المقبس")]
    [SerializeField] private FlagItem flag;
    [Tooltip("نقطة تثبيت العلم — إن تُركت فارغة يُستخدم هذا الكائن نفسه")]
    [SerializeField] private Transform placePoint;

    [Header("طريقة الوضع")]
    [Tooltip("وضع تلقائي بمجرد دخول المنطقة (بدون زر)")]
    [SerializeField] private bool autoPlace = false;
    [Tooltip("زر الوضع عندما يكون اللاعب داخل المنطقة (إذا لم يكن تلقائيًا)")]
    [SerializeField] private Key placeKey = Key.E;

    [Header("مكان الزرع")]
    [Tooltip("لو كان العلم واقفًا في المحرر عند هذي القاعدة، يُزرع في مكانه ذاك بالضبط " +
             "(نفس الموضع والدوران) — ما تشوفه في السين هو ما يصير في اللعب. " +
             "وإلا يُزرع في نقطة التثبيت. لتعديل مكان الزرع: حرّك العلم نفسه فوق القاعدة.")]
    [SerializeField] private bool plantWhereFlagStands = true;
    [Tooltip("أقصى بُعد للعلم عن القاعدة (متر) ليُعتبر واقفًا عندها")]
    [SerializeField] private float standRange = 3f;

    [Header("الشروط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("أحداث")]
    [Tooltip("عند وضع العلم في هذا المقبس تحديدًا")]
    public UnityEvent onFlagPlacedHere;

    /// <summary>العلم الذي يقبله هذا المقبس — يستخدمه <see cref="FlagBase"/> لقفله بعد الزرع.</summary>
    public FlagItem Flag => flag;

    /// <summary>نقطة تثبيت العلم الفعلية (المحددة أو هذا الكائن).</summary>
    public Transform PlacePoint => placePoint != null ? placePoint : transform;

    /// <summary>زر الوضع — يعرضه <see cref="FlagBaseBeacon"/> للاعب.</summary>
    public Key PlaceKey => placeKey;

    /// <summary>هل يوضع العلم تلقائيًا بلا زر؟</summary>
    public bool AutoPlace => autoPlace;

    private bool playerInside;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>
    /// في Start لا Awake: بعد أن يحفظ كل علم موضعه الأصلي، وقبل أن يحرّكه
    /// <see cref="FlagCarry"/> (ترتيب تنفيذه 100). الحكم على مكان العلم لا على إعداداته،
    /// فيصح سواء كان العلم مخفيًا حتى يُملك أو ظاهرًا للتجربة.
    /// </summary>
    private void Start()
    {
        if (!plantWhereFlagStands || flag == null || !FlagStandsHere(flag.transform.position)) return;

        // كل نسخ FlagItem على العلم — المقبس مربوط بواحدة، والتقاطه يمر بها كلها
        foreach (var f in flag.GetComponents<FlagItem>()) f.UseStartPoseWhenPlanted();
    }

    /// <summary>هل هذا الموضع عند القاعدة؟ قريب من نقطة التثبيت أو داخل منطقتها.</summary>
    private bool FlagStandsHere(Vector3 position)
    {
        if (Vector3.Distance(position, PlacePoint.position) <= standRange) return true;

        var zone = GetComponent<Collider>();
        if (zone == null) return false;

        Bounds b = zone.bounds;
        b.Expand(standRange);
        return b.Contains(position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) playerInside = false;
    }

    private void Update()
    {
        if (!playerInside || flag == null || !flag.IsHeld) return;

        bool keyPressed = placeKey != Key.None && Keyboard.current != null &&
                          Keyboard.current[placeKey].wasPressedThisFrame;

        if (autoPlace || keyPressed)
        {
            flag.PlaceAt(PlacePoint);
            onFlagPlacedHere?.Invoke();
        }
    }
}

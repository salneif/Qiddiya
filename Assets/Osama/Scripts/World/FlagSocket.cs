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

    [Header("الشروط")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";

    [Header("أحداث")]
    [Tooltip("عند وضع العلم في هذا المقبس تحديدًا")]
    public UnityEvent onFlagPlacedHere;

    /// <summary>العلم الذي يقبله هذا المقبس — يستخدمه <see cref="FlagBase"/> لقفله بعد الزرع.</summary>
    public FlagItem Flag => flag;

    private bool playerInside;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
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
            flag.PlaceAt(placePoint != null ? placePoint : transform);
            onFlagPlacedHere?.Invoke();
        }
    }
}

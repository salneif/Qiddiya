using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// زر ضغط قابل للتكرار — يقترب اللاعب ويضغط زر التفاعل فيُطلق حدثًا
/// (مثل تدوير عمود اللغز). يمكن ضغطه مرات غير محدودة، مع حركة غطسة بصرية.
/// </summary>
public class PuzzleButton : MonoBehaviour
{
    [Header("التفاعل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("أقصى مسافة يشتغل منها الزر (متر)")]
    [SerializeField] private float interactRange = 2f;
    [Tooltip("زر الضغط")]
    [SerializeField] private Key interactKey = Key.E;
    [Tooltip("مهلة بين الضغطات (ثواني) — تمنع الضغط أثناء دوران العمود")]
    [SerializeField] private float cooldown = 0.6f;

    [Header("حركة الزر (بصري، اختياري)")]
    [Tooltip("مجسم الزر الذي يغطس عند الضغط — اتركه فارغًا بلا حركة")]
    [SerializeField] private Transform buttonVisual;
    [Tooltip("مسافة الغطسة (بالمحور المحلي Y)")]
    [SerializeField] private float pressDepth = 0.05f;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressSound;

    [Header("أحداث")]
    [Tooltip("عند الضغط — اربطه بـ RotaryPillar.Rotate")]
    public UnityEvent onPressed;

    /// <summary>تعطيل الزر (يستخدمه اللغز بعد الحل).</summary>
    public bool Locked { get; set; }

    private Transform player;
    private float lastPress = -999f;
    private Vector3 visualStart;

    private void Awake()
    {
        if (buttonVisual != null)
            visualStart = buttonVisual.localPosition;
    }

    private void Update()
    {
        if (Locked) return;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
            if (player == null) return;
        }

        bool pressed = interactKey != Key.None && Keyboard.current != null &&
                       Keyboard.current[interactKey].wasPressedThisFrame;

        if (pressed &&
            Time.time - lastPress >= cooldown &&
            Vector3.Distance(player.position, transform.position) <= interactRange)
        {
            lastPress = Time.time;
            if (pressSound != null && audioSource != null)
                audioSource.PlayOneShot(pressSound);
            if (buttonVisual != null)
                StartCoroutine(PressAnim());
            onPressed?.Invoke();
        }
    }

    private IEnumerator PressAnim()
    {
        Vector3 down = visualStart + Vector3.down * pressDepth;
        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            buttonVisual.localPosition = Vector3.Lerp(visualStart, down, t / 0.1f);
            yield return null;
        }
        t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            buttonVisual.localPosition = Vector3.Lerp(down, visualStart, t / 0.15f);
            yield return null;
        }
        buttonVisual.localPosition = visualStart;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// دوّاسة أرضية: ينطّ عليها اللاعب فتنضغط للأسفل وتطلق حدثها — بديل
/// <see cref="PuzzleButton"/> الذي يحتاج الوقوف بجانبه وضغط زر.
///
/// تنضغط ما دام اللاعب فوقها وترتفع حين ينزل، فتصير كل دوسة = تفعيل واحد.
/// اربط <see cref="onPressed"/> بـ RotaryPillar.Rotate ليدور العمود الذي أمامها.
///
/// (الاسم PuzzlePlate وليس PressurePlate لتفادي التعارض مع
/// Sultan/Scripts/SteamRoom/Plates and Boxes/PressurePlate.cs — نفس سبب تسمية WorldLever.)
///
/// التركيب:
///  - مجسم الدوّاسة مع كولايدر <b>صلب</b> يقف عليه اللاعب.
///  - كائن ابن فوقه كولايدر <b>Is Trigger</b> رفيع يغطي سطح الدوّاسة + هذا السكربت.
///  - اربط <see cref="plateVisual"/> بمجسم الدوّاسة لتنزل بصريًا.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuzzlePlate : MonoBehaviour
{
    [Header("التفعيل")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("أقل زمن بين دوستين (ثواني) — يمنع التفعيل المزدوج عند النطّ")]
    [SerializeField] private float cooldown = 0.4f;

    [Header("الحركة")]
    [Tooltip("مجسم الدوّاسة الذي ينزل — اتركه فارغًا بلا حركة")]
    [SerializeField] private Transform plateVisual;
    [Tooltip("مسافة الغطس (متر)")]
    [SerializeField] private float pressDepth = 0.08f;
    [Tooltip("سرعة النزول والصعود")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("الصوت")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("صوت الانضغاط — نقرة حجرية")]
    [SerializeField] private AudioClip pressSound;
    [Tooltip("صوت الارتفاع عند النزول عنها")]
    [SerializeField] private AudioClip releaseSound;

    [Header("أحداث")]
    [Tooltip("عند الدوس — اربطه بـ RotaryPillar.Rotate")]
    public UnityEvent onPressed;
    [Tooltip("عند النزول عنها")]
    public UnityEvent onReleased;

    /// <summary>قفل الدوّاسة (يستخدمه اللغز بعد الحل).</summary>
    public bool Locked { get; set; }

    /// <summary>هل هي مضغوطة الآن؟</summary>
    public bool IsPressed { get; private set; }

    private Vector3 upPosition;
    private int occupants;
    private float lastPressTime = -999f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (plateVisual != null) upPosition = plateVisual.localPosition;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // نعدّ الكولايدرات لأن اللاعب قد يحمل أكثر من واحد
        occupants++;
        if (occupants > 1 || IsPressed) return;
        if (Locked || Time.time - lastPressTime < cooldown) return;

        IsPressed = true;
        lastPressTime = Time.time;

        Play(pressSound);
        onPressed?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        occupants = Mathf.Max(0, occupants - 1);
        if (occupants > 0 || !IsPressed) return;

        IsPressed = false;
        Play(releaseSound);
        onReleased?.Invoke();
    }

    private void Update()
    {
        if (plateVisual == null) return;

        // تبقى منخفضة ما دام واقفًا عليها، وترتفع بنعومة حين ينزل
        Vector3 target = IsPressed ? upPosition + Vector3.down * pressDepth : upPosition;
        plateVisual.localPosition = Vector3.MoveTowards(plateVisual.localPosition, target,
                                                        moveSpeed * pressDepth * Time.deltaTime);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = IsPressed ? new Color(0.3f, 1f, 0.4f, 0.9f)
                                 : new Color(0.2f, 0.7f, 1f, 0.7f);
        Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}

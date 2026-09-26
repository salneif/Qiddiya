using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// أيقونة زر تطفو فوق أي شيء يمكن استخدامه، تظهر عند اقتراب اللاعب وتخفّ مع ابتعاده.
///
/// مستقلّة تمامًا: لا تعرف سكربت التفاعل ولا تتدخّل فيه، فتصلح للرافعات والأبواب
/// وأي شيء من أي أحد. ضعها على الكائن وكفى — الأيقونة تُحمَّل من
/// <c>Osama/Resources</c> حسب <see cref="key"/>.
///
/// بعد أن يضغط اللاعب الزر وهو قريب، تختفي نهائيًا (<see cref="hideAfterUse"/>):
/// التلميح للتعليم لا للتزيين.
/// </summary>
[DisallowMultipleComponent]
public class KeyPromptHint : MonoBehaviour
{
    [Header("الزر")]
    [Tooltip("زر التفاعل — يحدّد الأيقونة (KeyE / KeyA / KeyD) ويكشف استخدام اللاعب لها")]
    [SerializeField] private Key key = Key.E;
    [Tooltip("أيقونة مخصّصة — اتركها فارغة لتُحمَّل حسب الزر من Osama/Resources")]
    [SerializeField] private Sprite icon;

    [Header("المكان")]
    [Tooltip("نقطة الظهور — يُستخدم هذا الكائن إن تُركت فارغة")]
    [SerializeField] private Transform anchor;
    [Tooltip("الارتفاع فوق النقطة (متر)")]
    [SerializeField] private float height = 1.05f;
    [Tooltip("حجم الأيقونة (متر)")]
    [SerializeField] private float size = 0.35f;

    [Header("الظهور")]
    [Tooltip("المسافة التي تظهر عندها (متر)")]
    [SerializeField] private float showDistance = 3.5f;
    [Tooltip("آخر متر يخفّ فيه بالتدريج بدل أن ينطفئ فجأة")]
    [SerializeField] private float fadeBand = 1.2f;
    [Tooltip("سرعة الظهور والاختفاء")]
    [SerializeField] private float fadeSpeed = 6f;
    [Tooltip("تختفي نهائيًا بعد أن يضغط اللاعب الزر قريبًا منها")]
    [SerializeField] private bool hideAfterUse = true;
    [SerializeField] private string playerTag = "Player";

    private Transform player;
    private Camera cam;
    private SpriteRenderer icon3d;
    private float alpha;
    private bool used;

    private Transform Anchor => anchor != null ? anchor : transform;

    private void Awake()
    {
        if (icon == null) icon = Resources.Load<Sprite>("Key" + key);
        if (icon == null) icon = Resources.Load<Sprite>("KeyE");

        if (icon == null)
        {
            Debug.LogWarning($"[KeyPromptHint] ما لقيت أيقونة للزر {key} في Osama/Resources.", this);
            enabled = false;
            return;
        }

        var go = new GameObject($"KeyPromptHint_{key}");
        go.layer = 2; // Ignore Raycast
        go.transform.SetParent(transform, false);

        icon3d = go.AddComponent<SpriteRenderer>();
        icon3d.sprite = icon;
        icon3d.enabled = false;
    }

    private void LateUpdate()
    {
        alpha = Mathf.MoveTowards(alpha, Target(), fadeSpeed * Time.deltaTime);

        if (alpha <= 0.001f)
        {
            icon3d.enabled = false;
            return;
        }

        if (cam == null || !cam.isActiveAndEnabled) cam = ResolveCamera();

        Vector3 pos = Anchor.position + Vector3.up * height;
        icon3d.transform.position = pos;

        if (cam != null)
            icon3d.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);

        // الحجم بفضاء العالم مهما كان تكبير الكائن الأب
        Vector3 s = transform.lossyScale;
        icon3d.transform.localScale = new Vector3(Div(size, s.x), Div(size, s.y), Div(size, s.z));
        icon3d.color = new Color(1f, 1f, 1f, alpha);
        icon3d.enabled = true;
    }

    /// <summary>شدّة الظهور المطلوبة الآن.</summary>
    private float Target()
    {
        if (used && hideAfterUse) return 0f;

        if (player == null)
        {
            var go = PlayerLocator.Find(playerTag);
            if (go == null) return 0f;
            player = go.transform;
        }

        float distance = Vector3.Distance(player.position, Anchor.position);
        if (distance > showDistance) return 0f;

        // ضغط الزر وهو قريب = تعلّمها، فلا داعي لتكرار التلميح
        if (Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame)
            used = true;

        float band = Mathf.Max(0.01f, fadeBand);
        float fadeStart = Mathf.Max(0f, showDistance - band);
        return Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((distance - fadeStart) / band));
    }

    /// <summary>يظهر التلميح من جديد — اربطه بحدث إن أردت إعادة تعليم اللاعب.</summary>
    public void ShowAgain() => used = false;

    private static float Div(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    private static Camera ResolveCamera()
    {
        if (Camera.main != null) return Camera.main;

        foreach (var c in Camera.allCameras)
            if (c != null && c.isActiveAndEnabled) return c;

        return null;
    }

    private void OnValidate()
    {
        size = Mathf.Max(0.01f, size);
        showDistance = Mathf.Max(0.5f, showDistance);
        fadeSpeed = Mathf.Max(0.1f, fadeSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(Anchor.position, showDistance);
        Gizmos.DrawLine(Anchor.position, Anchor.position + Vector3.up * height);
    }
}

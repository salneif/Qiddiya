using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// تلميح توتوريال مرسوم في العالم (بأسلوب Little Nightmares — لا واجهة على الشاشة).
///
/// - يظهر بالتلاشي عندما يقترب اللاعب من المنطقة.
/// - يختفي نهائيًا بعد أن ينفّذ اللاعب الحركة المطلوبة فعلًا (ضغط الزر)، لا بمجرد المرور.
/// - يمكن أيضًا إخفاؤه من حدث خارجي عبر <see cref="CompleteHint"/> (مثلاً عند اجتياز الحاجز).
///
/// حُطّه على كائن اللافتة/النص (Quad برسمة الطباشير أو TextMeshPro 3D).
/// يعمل مع أي Renderer أو TextMeshPro عبر تغيير شفافية الماتيريال.
/// </summary>
public class WorldTutorialHint : MonoBehaviour
{
    [Header("العرض")]
    [Tooltip("الرندررات التي تتلاشى — تُجمع تلقائيًا من الأبناء إن تُركت فارغة")]
    [SerializeField] private Renderer[] visuals;
    [Tooltip("مدة الظهور/الاختفاء (ثواني)")]
    [SerializeField] private float fadeTime = 0.6f;
    [Tooltip("أقصى شفافية عند الظهور الكامل")]
    [Range(0f, 1f)] [SerializeField] private float maxAlpha = 1f;

    [Header("منطقة الظهور")]
    [Tooltip("وسم اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("المسافة التي يظهر عندها التلميح (متر)")]
    [SerializeField] private float showDistance = 8f;

    [Header("إتمام التوتوريال")]
    [Tooltip("الزر الذي إذا ضغطه اللاعب اعتُبر التلميح منفَّذًا (None لتعطيله)")]
    [SerializeField] private Key completeKey = Key.Space;
    [Tooltip("يجب أن يكون اللاعب داخل المنطقة عند الضغط ليُحتسب")]
    [SerializeField] private bool requireInsideToComplete = true;
    [Tooltip("يختفي نهائيًا بعد الإتمام ولا يعود")]
    [SerializeField] private bool hideForever = true;

    [Header("مواجهة الكاميرا")]
    [Tooltip("يدور التلميح ليواجه الكاميرا دائمًا (أطفئه للوحة ثابتة على جدار)")]
    [SerializeField] private bool faceCamera = false;

    [Header("أحداث")]
    public UnityEvent onShown;
    public UnityEvent onCompleted;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform player;
    private Camera cam;
    private MaterialPropertyBlock block;
    private float alpha;
    private float targetAlpha;
    private bool completed;
    private bool wasShown;

    private void Awake()
    {
        if (visuals == null || visuals.Length == 0)
            visuals = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        alpha = targetAlpha = 0f;
        ApplyAlpha();
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go != null) player = go.transform;
        }

        bool inside = player != null &&
                      Vector3.Distance(player.position, transform.position) <= showDistance;

        // الظهور/الاختفاء حسب القرب (ما لم يكن قد أُتمّ)
        if (!completed)
        {
            targetAlpha = inside ? maxAlpha : 0f;

            if (inside && !wasShown)
            {
                wasShown = true;
                onShown?.Invoke();
            }
            else if (!inside)
            {
                wasShown = false;
            }

            // الإتمام بالضغط
            if (completeKey != Key.None && Keyboard.current != null &&
                Keyboard.current[completeKey].wasPressedThisFrame &&
                (!requireInsideToComplete || inside))
            {
                CompleteHint();
            }
        }

        // تنعيم الشفافية
        if (!Mathf.Approximately(alpha, targetAlpha))
        {
            alpha = Mathf.MoveTowards(alpha, targetAlpha,
                Time.deltaTime / Mathf.Max(fadeTime, 0.0001f));
            ApplyAlpha();
        }

        if (faceCamera)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = transform.position - cam.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }
    }

    /// <summary>ينهي التلميح ويخفيه (نادِه من حدث خارجي: اجتياز حاجز، فتح باب...).</summary>
    public void CompleteHint()
    {
        if (completed) return;
        if (hideForever) completed = true;
        targetAlpha = 0f;
        onCompleted?.Invoke();
    }

    /// <summary>يعيد تفعيل التلميح من جديد.</summary>
    public void ResetHint()
    {
        completed = false;
        wasShown = false;
    }

    private void ApplyAlpha()
    {
        if (visuals == null) return;
        foreach (var r in visuals)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);

            // يدعم شيدرات URP (_BaseColor) والقديمة/TMP (_Color)
            Color c = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)
                ? r.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
            c.a = alpha;
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);

            r.SetPropertyBlock(block);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}

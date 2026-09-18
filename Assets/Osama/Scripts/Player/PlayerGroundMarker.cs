using UnityEngine;

/// <summary>
/// نقطة ظل تحت اللاعب على الأرض تتبعه أينما مشى — توريه وين بينزل بالضبط وهو
/// يقفز أو يمشي على حافة. تصغر وتخف كلما ارتفع عن الأرض، فيقدّر القفزة بعينه.
///
/// التركيب: كائن فارغ في السين + هذا السكربت. ما يحتاج ربط أي شيء — يلقى اللاعب
/// بالوسم، ويرسم النقطة بماتيريال جاهز في <c>Assets/Osama/Resources</c>.
///
/// <see cref="keepAcrossScenes"/> ✅ = حطّه مرة واحدة في الهب فيبقى معك في كل المراحل
/// بعدها. ولو حطيته في أكثر من سين، النسخة الزائدة تشيل نفسها — فحطّه بأمان في كل
/// سين تجرّبه مباشرة.
/// </summary>
[DisallowMultipleComponent]
public class PlayerGroundMarker : MonoBehaviour
{
    private const string DefaultMaterialPath = "OsamaGroundMarker";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

    /// <summary>النسخة الشغّالة — تمنع نقطتين تحت اللاعب.</summary>
    private static PlayerGroundMarker active;

    [Header("اللاعب")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("يبقى شغّالًا في كل المشاهد بعد هذا. يشتغل فقط إذا كان السكربت على كائن " +
             "فارغ خاص فيه — لا تحطّه على اللاعب أو على كائن فيه سكربتات ثانية.")]
    [SerializeField] private bool keepAcrossScenes = true;

    [Header("الشكل")]
    [Tooltip("نصف قطر النقطة (متر) وهو واقف على الأرض")]
    [SerializeField] private float radius = 0.45f;
    [Tooltip("لون النقطة، والشفافية (A) هي قوتها")]
    [SerializeField] private Color color = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("نعومة الحافة — 0 حادة، 1 ضبابية بالكامل")]
    [Range(0.01f, 1f)]
    [SerializeField] private float softness = 0.4f;
    [Tooltip("ماتيريال مخصص — اتركه فارغًا ليُستخدم الجاهز")]
    [SerializeField] private Material material;

    [Header("السلوك")]
    [Tooltip("تصغر وتخف كلما ارتفع اللاعب عن الأرض — تساعده يقدّر القفزة")]
    [SerializeField] private bool shrinkWithHeight = true;
    [Tooltip("الارتفاع الذي تصل عنده النقطة لأصغر حجم (متر)")]
    [SerializeField] private float fullShrinkHeight = 4f;
    [Tooltip("أصغر حجم وأخف قوة نسبةً للأصل")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minSize = 0.5f;
    [Tooltip("تظهر فقط واللاعب يتحرك، وتختفي بنعومة لما يوقف")]
    [SerializeField] private bool onlyWhileMoving = false;
    [Tooltip("أقل سرعة (متر/ثانية) تُحسب حركة")]
    [SerializeField] private float movingSpeedThreshold = 0.2f;
    [Tooltip("سرعة ظهور واختفاء النقطة")]
    [SerializeField] private float fadeSpeed = 6f;

    [Header("الأرض")]
    [Tooltip("الطبقات التي تُعتبر أرضًا")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [Tooltip("أقصى مسافة للبحث عن أرض تحته (متر) — أبعد من كذا يعني فراغ فتختفي النقطة")]
    [SerializeField] private float maxGroundDistance = 30f;
    [Tooltip("ارتفاع النقطة فوق الأرض — يمنع وميضها معها")]
    [SerializeField] private float surfaceOffset = 0.02f;

    private Transform player;
    private Transform ignoreRoot;          // اللاعب وكل ما تحته (والعلم المحمول) لا يُحسب أرضًا
    private PlayerKillable killable;
    private CharacterController controller;

    private Transform dot;
    private MeshRenderer dotRenderer;
    private Mesh dotMesh;
    private MaterialPropertyBlock block;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private Vector3 lastPlayerPos;
    private bool hasLastPos;
    private float visibility;

    /// <summary>يصفّر النسخة الشغّالة مع كل تشغيل — ضروري مع "Enter Play Mode without Domain Reload".</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;

    private void Awake()
    {
        if (active != null && active != this)
        {
            // نسخة ثانية (سين ثاني فيه واحدة، والشغّالة جت معنا) — نشيل السكربت وحده لا الكائن
            Destroy(this);
            return;
        }
        active = this;

        if (keepAcrossScenes)
        {
            if (IsDedicatedObject())
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning("[PlayerGroundMarker] ما يقدر يبقى عبر المشاهد وهو على كائن فيه " +
                                 "أشياء ثانية — حطّه على كائن فارغ خاص فيه. يشتغل في هذا السين فقط.", this);
            }
        }

        if (material == null) material = Resources.Load<Material>(DefaultMaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"[PlayerGroundMarker] ما لقيت الماتيريال Resources/{DefaultMaterialPath}.", this);
            enabled = false;
            return;
        }

        CreateDot();
    }

    /// <summary>كائن فارغ خاص بالسكربت؟ (Transform + هذا فقط، وبلا أبناء)</summary>
    private bool IsDedicatedObject()
    {
        return GetComponents<Component>().Length == 2 && transform.childCount == 0;
    }

    private void CreateDot()
    {
        dotMesh = BuildQuad();

        var go = new GameObject("PlayerGroundMarker_Dot");
        go.layer = 2; // Ignore Raycast — لا يعترض أي شعاع (ولا كولايدر له أصلًا)
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = dotMesh;

        dotRenderer = go.AddComponent<MeshRenderer>();
        dotRenderer.sharedMaterial = material;
        dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dotRenderer.receiveShadows = false;
        dotRenderer.enabled = false;

        dot = go.transform;
        block = new MaterialPropertyBlock();
    }

    /// <summary>مربّع مسطّح على الأرض (وجهه لفوق) بطول 1 — يُكبَّر بحسب نصف القطر.</summary>
    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "PlayerGroundMarker_Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f,  0.5f), new Vector3(0.5f, 0f,  0.5f)
        };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void LateUpdate()
    {
        if (dot == null) return;

        if (!ResolvePlayer())
        {
            dotRenderer.enabled = false;
            hasLastPos = false;
            return;
        }

        if (!FindGround(out RaycastHit ground))
        {
            // فوق فراغ — لا نقطة
            dotRenderer.enabled = false;
            return;
        }

        float wanted = 1f;
        if (killable != null && killable.IsDead) wanted = 0f;
        if (onlyWhileMoving && !IsMoving()) wanted = 0f;
        visibility = Mathf.MoveTowards(visibility, wanted, fadeSpeed * Time.deltaTime);

        lastPlayerPos = player.position;
        hasLastPos = true;

        if (visibility <= 0.001f)
        {
            dotRenderer.enabled = false;
            return;
        }

        // الارتفاع عن الأرض: من القدمين إن وُجد CharacterController، وإلا من مركز اللاعب
        float feetY = controller != null ? controller.bounds.min.y : player.position.y;
        float height = Mathf.Max(0f, feetY - ground.point.y);
        float k = shrinkWithHeight && fullShrinkHeight > 0.01f
            ? Mathf.Lerp(1f, minSize, Mathf.Clamp01(height / fullShrinkHeight))
            : 1f;

        dot.SetPositionAndRotation(ground.point + ground.normal * surfaceOffset,
                                   Quaternion.FromToRotation(Vector3.up, ground.normal));
        float size = radius * 2f * k;
        dot.localScale = Vector3.one * size;
        // الأب قد يكون مكبّرًا — نضبط الحجم الفعلي في العالم
        if (dot.parent != null)
        {
            Vector3 ps = dot.parent.lossyScale;
            dot.localScale = new Vector3(SafeDiv(size, ps.x), SafeDiv(size, ps.y), SafeDiv(size, ps.z));
        }

        block.SetColor(ColorId, new Color(color.r, color.g, color.b, color.a * visibility * k));
        block.SetFloat(SoftnessId, softness);
        dotRenderer.SetPropertyBlock(block);
        dotRenderer.enabled = true;
    }

    private static float SafeDiv(float a, float b) => Mathf.Abs(b) > 0.0001f ? a / b : a;

    /// <summary>يلقى اللاعب ويعيد البحث بعد كل تغيير سين (اللاعب القديم انحذف).</summary>
    private bool ResolvePlayer()
    {
        if (player != null) return true;

        var go = PlayerLocator.Find(playerTag);
        if (go == null) return false;

        player = go.transform;
        killable = player.GetComponentInParent<PlayerKillable>();
        controller = player.GetComponentInParent<CharacterController>();
        ignoreRoot = killable != null ? killable.transform : player;
        hasLastPos = false;
        visibility = 0f;
        return true;
    }

    /// <summary>أقرب أرض تحت اللاعب، متجاهلًا كولايدرات اللاعب نفسه والتريغرات.</summary>
    private bool FindGround(out RaycastHit ground)
    {
        ground = default;
        Vector3 origin = player.position + Vector3.up * 0.5f;
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxGroundDistance + 0.5f,
                                        groundLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider.transform.IsChildOf(ignoreRoot)) continue;
            if (hits[i].distance >= best) continue;
            best = hits[i].distance;
            ground = hits[i];
            found = true;
        }
        return found;
    }

    private bool IsMoving()
    {
        if (!hasLastPos || Time.deltaTime <= 0f) return true;
        Vector3 delta = player.position - lastPlayerPos;
        delta.y = 0f;
        return delta.magnitude / Time.deltaTime >= movingSpeedThreshold;
    }

    private void OnDisable()
    {
        if (dotRenderer != null) dotRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
        if (dotMesh != null) Destroy(dotMesh);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        fullShrinkHeight = Mathf.Max(0f, fullShrinkHeight);
        maxGroundDistance = Mathf.Max(0.5f, maxGroundDistance);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
    }
}

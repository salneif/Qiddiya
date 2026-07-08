using UnityEngine;

/// <summary>
/// Standalone reaction: when the scale becomes balanced (solved), tint a target
/// GameObject. Lives separately from BalanceScale so the puzzle logic stays clean
/// and you can drop as many reactions as you like on one scale.
///
/// It subscribes to the scale's BalanceChanged event in OnEnable and unsubscribes
/// in OnDisable. Works on a MeshRenderer (via MaterialPropertyBlock, so no material
/// instances are leaked) or a SpriteRenderer for 2D.
/// </summary>
[DisallowMultipleComponent]
public class ScaleColorReaction : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("The scale to watch. Fires when it gets solved.")]
    [SerializeField] private BalanceScale scale;

    [Header("Target")]
    [Tooltip("The GameObject whose color changes when the scale is solved. " +
             "Needs a Renderer (3D) or SpriteRenderer (2D).")]
    [SerializeField] private GameObject target;

    [Header("Colors")]
    [SerializeField] private Color solvedColor = new Color(0.3f, 1f, 0.4f, 1f);

    [Tooltip("If on, the target returns to its original color whenever the scale " +
             "goes back to unbalanced. If off, it stays solved-colored once solved.")]
    [SerializeField] private bool revertWhenUnbalanced = true;

    // Resolved target state.
    private Renderer targetRenderer;
    private SpriteRenderer targetSprite;
    private MaterialPropertyBlock mpb;
    private int colorPropertyId;
    private Color originalColor = Color.white;
    private bool resolved;

    private void Awake() => ResolveTarget();

    private void OnEnable()
    {
        if (scale != null) scale.BalanceChanged += HandleBalanceChanged;

        // Sync to the current state immediately so a re-enabled (or late-added)
        // reaction shows the right color without waiting for the next transition.
        if (scale != null) Apply(scale.IsBalanced);
    }

    private void OnDisable()
    {
        if (scale != null) scale.BalanceChanged -= HandleBalanceChanged;
    }

    private void HandleBalanceChanged(bool balanced) => Apply(balanced);

    private void Apply(bool balanced)
    {
        if (!resolved) return;
        if (balanced) SetColor(solvedColor);
        else if (revertWhenUnbalanced) SetColor(originalColor);
    }

    private void ResolveTarget()
    {
        resolved = false;
        if (target == null) return;

        // 2D path.
        targetSprite = target.GetComponent<SpriteRenderer>();
        if (targetSprite != null)
        {
            originalColor = targetSprite.color;
            resolved = true;
            return;
        }

        // 3D path — use a MaterialPropertyBlock to avoid instancing the material.
        targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            // URP/HDRP shaders use _BaseColor; the built-in pipeline uses _Color.
            Material mat = targetRenderer.sharedMaterial;
            bool baseColor = mat != null && mat.HasProperty("_BaseColor");
            colorPropertyId = Shader.PropertyToID(baseColor ? "_BaseColor" : "_Color");

            if (mat != null && mat.HasProperty(colorPropertyId))
                originalColor = mat.GetColor(colorPropertyId);

            mpb = new MaterialPropertyBlock();
            resolved = true;
            return;
        }

        Debug.LogWarning($"{nameof(ScaleColorReaction)}: target '{target.name}' has no Renderer or SpriteRenderer to color.", this);
    }

    private void SetColor(Color c)
    {
        if (targetSprite != null)
        {
            targetSprite.color = c;
            return;
        }

        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(colorPropertyId, c);
            targetRenderer.SetPropertyBlock(mpb);
        }
    }
}
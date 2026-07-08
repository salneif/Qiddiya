using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The puzzle brain. Compares the two pans' total weight and decides whether
/// the scale is balanced. This is the "trick": balance is a logic check
/// (leftTotal == rightTotal within tolerance), not a physics outcome. So only
/// the correct combination of objects/weights can ever fire OnBalanced.
///
/// The beam tilt is purely cosmetic — it just lerps a rotation toward an angle
/// derived from the weight difference. It does NOT drive the balance result.
/// </summary>
public class BalanceScale : MonoBehaviour
{
    [Header("Pans")]
    [SerializeField] private ScalePan leftPan;
    [SerializeField] private ScalePan rightPan;

    [Header("Balance Rule")]
    [Tooltip("How close the two totals must be to count as balanced.")]
    [SerializeField] private float balanceTolerance = 0.01f;

    [Tooltip("Optional: also require these exact object IDs to be present. " +
             "Leave empty to balance purely by weight.")]
    [SerializeField] private string[] requiredObjectIds = new string[0];

    [Header("Cosmetic Tilt (no physics)")]
    [SerializeField] private Transform beam;          // the crossbar that visually tilts
    [SerializeField] private float maxTiltAngle = 20f;
    [SerializeField] private float weightForMaxTilt = 5f;  // weight diff that reaches max tilt
    [SerializeField] private float tiltLerpSpeed = 5f;
    [Tooltip("Local axis the beam rotates around. (0,0,1) = Z is typical for a 2D-facing beam.")]
    [SerializeField] private Vector3 tiltAxis = new Vector3(0f, 0f, 1f);

    [Header("Puzzle Events")]
    public UnityEvent OnBalanced;     // fire your door/reward here
    public UnityEvent OnUnbalanced;
    public UnityEvent OnTipLeft;      // left side heavier
    public UnityEvent OnTipRight;     // right side heavier

    /// <summary>
    /// Fires when the balanced/unbalanced state actually changes (true = balanced).
    /// A separate script (e.g. the color changer) subscribes to this in its OnEnable.
    /// Not fired for the silent startup baseline.
    /// </summary>
    public event System.Action<bool> BalanceChanged;

    public bool IsBalanced { get; private set; }

    private float targetTilt;
    private bool initialized;

    private void OnEnable()
    {
        if (leftPan != null) leftPan.ContentsChanged += Evaluate;
        if (rightPan != null) rightPan.ContentsChanged += Evaluate;

        // Sync state on enable. The first call only records a baseline (see Evaluate)
        // so we never fire a spurious OnBalanced before pans have scanned.
        Evaluate();
    }

    private void OnDisable()
    {
        if (leftPan != null) leftPan.ContentsChanged -= Evaluate;
        if (rightPan != null) rightPan.ContentsChanged -= Evaluate;
    }

    /// <summary>
    /// Re-checks balance and fires events on state change.
    /// PUBLIC so your separate MAGIC script can force a re-check after it
    /// changes a weight in a way that didn't auto-notify, e.g.:
    ///     scale.Evaluate();
    /// (Normally not needed if magic sets WeightedObject.Weight via the property.)
    /// </summary>
    public void Evaluate()
    {
        if (leftPan == null || rightPan == null) return;

        float diff = leftPan.TotalWeight - rightPan.TotalWeight; // + = left heavier
        bool weightsMatch = Mathf.Abs(diff) <= balanceTolerance;
        bool balancedNow = weightsMatch && RequiredObjectsPresent();

        // Cosmetic target: heavier side dips down. Just a clamped number map.
        float t = Mathf.Clamp(diff / Mathf.Max(0.0001f, weightForMaxTilt), -1f, 1f);
        targetTilt = t * maxTiltAngle;

        // First evaluation records the starting state silently. Without this, an empty
        // scale at scene load reads 0 == 0 and would wrongly fire OnBalanced (opening
        // your door before the player did anything). Read IsBalanced for initial state.
        if (!initialized)
        {
            initialized = true;
            IsBalanced = balancedNow;
            return;
        }

        if (balancedNow == IsBalanced) return;

        IsBalanced = balancedNow;
        BalanceChanged?.Invoke(balancedNow);
        if (balancedNow)
        {
            OnBalanced?.Invoke();
        }
        else
        {
            OnUnbalanced?.Invoke();
            if (diff > balanceTolerance) OnTipLeft?.Invoke();
            else if (diff < -balanceTolerance) OnTipRight?.Invoke();
        }
    }

    private bool RequiredObjectsPresent()
    {
        if (requiredObjectIds == null || requiredObjectIds.Length == 0) return true;
        for (int i = 0; i < requiredObjectIds.Length; i++)
        {
            string id = requiredObjectIds[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (!leftPan.Contains(id) && !rightPan.Contains(id)) return false;
        }
        return true;
    }

    private void Update()
    {
        if (beam == null) return;

        // Read current angle around the chosen axis, lerp toward target.
        // This is the only "movement" — a visual nicety, not a simulation.
        Vector3 e = beam.localEulerAngles;
        float currentZ = e.z > 180f ? e.z - 360f : e.z;
        float nextZ = Mathf.Lerp(currentZ, targetTilt, Time.deltaTime * tiltLerpSpeed);

        // Apply around the configured axis (default Z).
        Quaternion baseRot = Quaternion.Euler(e.x * (1 - tiltAxis.x), e.y * (1 - tiltAxis.y), 0f);
        beam.localRotation = baseRot * Quaternion.AngleAxis(nextZ, tiltAxis.normalized);
    }
}
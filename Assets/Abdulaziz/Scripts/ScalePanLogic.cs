using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One side of the scale. Detects WeightedObjects by polling an overlap volume
/// instead of relying on OnTrigger callbacks. This means NO Rigidbody is required
/// anywhere — it works with CharacterController-driven objects, kinematic objects,
/// static colliders, or props teleported in by the magic script.
///
/// Setup: add a BoxCollider to this GameObject (set it as a trigger so it doesn't
/// block movement) and size it to cover the pan surface. It's used only to DEFINE
/// the detection zone — the actual detection is a region query, not a physics event.
/// If you prefer, skip the BoxCollider and set zoneSize/zoneCenter manually.
/// </summary>
public class ScalePan : MonoBehaviour
{
    [Header("Detection Zone")]
    [Tooltip("Optional. If set, its bounds define the detection volume. " +
             "Mark it as a trigger so it never blocks the player/objects.")]
    [SerializeField] private BoxCollider zoneCollider;

    [Tooltip("Used only if no zoneCollider is assigned. Local-space box around this transform.")]
    [SerializeField] private Vector3 zoneCenter = Vector3.zero;
    [SerializeField] private Vector3 zoneSize = new Vector3(1f, 0.5f, 1f);

    [Tooltip("Which layers hold weighable objects. Keep this tight for performance.")]
    [SerializeField] private LayerMask detectionLayers = ~0;

    [Tooltip("How often (seconds) to re-scan the zone. 0 = every frame.")]
    [SerializeField] private float scanInterval = 0.1f;

    private readonly List<WeightedObject> contents = new List<WeightedObject>();
    private readonly HashSet<WeightedObject> found = new HashSet<WeightedObject>();
    private readonly Collider[] buffer = new Collider[32];
    private float scanTimer;

    /// <summary>Fires when an object is added/removed OR an object's weight changes.</summary>
    public event System.Action ContentsChanged;

    /// <summary>Sum of every WeightedObject currently resting on this pan.</summary>
    public float TotalWeight
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < contents.Count; i++)
                sum += contents[i].Weight;
            return sum;
        }
    }

    public IReadOnlyList<WeightedObject> Contents => contents;

    private void OnEnable()
    {
        // Re-hook anything still tracked (e.g. after this pan was disabled then
        // re-enabled — OnDisable removed those subscriptions), then scan now so we
        // don't sit blind until the first timed scan.
        for (int i = 0; i < contents.Count; i++)
            Subscribe(contents[i]);

        Scan();
        scanTimer = scanInterval;
    }

    private void OnDisable()
    {
        for (int i = 0; i < contents.Count; i++)
            Unsubscribe(contents[i]);
    }

    private void Update()
    {
        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f) return;
        scanTimer = scanInterval;
        Scan();
    }

    // ---- Overlap-based detection (no Rigidbody needed) --------------------

    private void Scan()
    {
        GetZone(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);

        int count = Physics.OverlapBoxNonAlloc(
            center, halfExtents, buffer, orientation,
            detectionLayers, QueryTriggerInteraction.Ignore);

        found.Clear();
        for (int i = 0; i < count; i++)
        {
            var w = buffer[i].GetComponentInParent<WeightedObject>();
            if (w != null) found.Add(w);
        }

        bool changed = false;

        // Remove objects that left the zone.
        for (int i = contents.Count - 1; i >= 0; i--)
        {
            if (!found.Contains(contents[i]))
            {
                Unsubscribe(contents[i]);
                contents.RemoveAt(i);
                changed = true;
            }
        }

        // Add objects that entered the zone.
        foreach (var w in found)
        {
            if (!contents.Contains(w))
            {
                contents.Add(w);
                Subscribe(w);
                changed = true;
            }
        }

        if (changed) ContentsChanged?.Invoke();
    }

    private void GetZone(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation)
    {
        if (zoneCollider != null)
        {
            Transform t = zoneCollider.transform;
            center = t.TransformPoint(zoneCollider.center);
            halfExtents = Vector3.Scale(zoneCollider.size * 0.5f, t.lossyScale);
            orientation = t.rotation;
        }
        else
        {
            center = transform.TransformPoint(zoneCenter);
            halfExtents = Vector3.Scale(zoneSize * 0.5f, transform.lossyScale);
            orientation = transform.rotation;
        }
    }

    // ---- Subscription helpers (keep enable/disable symmetric) -------------

    private void Subscribe(WeightedObject w)
    {
        w.WeightChanged += HandleWeightChanged;
        w.Disabled += HandleObjectDisabled;
    }

    private void Unsubscribe(WeightedObject w)
    {
        w.WeightChanged -= HandleWeightChanged;
        w.Disabled -= HandleObjectDisabled;
    }

    private void HandleWeightChanged(WeightedObject w) => ContentsChanged?.Invoke();

    // An object turned off (destroyed-by-disable, pooled, etc.) drops out instantly
    // rather than lingering until the next scan.
    private void HandleObjectDisabled(WeightedObject w) => Remove(w);

    // ---- Manual placement (magic teleports an object in/out by code) ------

    public void Add(WeightedObject w)
    {
        if (w == null || contents.Contains(w)) return;
        contents.Add(w);
        Subscribe(w);
        ContentsChanged?.Invoke();
    }

    public void Remove(WeightedObject w)
    {
        if (w == null || !contents.Remove(w)) return;
        Unsubscribe(w);
        ContentsChanged?.Invoke();
    }

    /// <summary>True if an object with this ID is currently on the pan.</summary>
    public bool Contains(string objectId)
    {
        for (int i = 0; i < contents.Count; i++)
            if (contents[i].ObjectId == objectId) return true;
        return false;
    }

    // Draws the detection zone in the Scene view so you can size it visually.
    private void OnDrawGizmosSelected()
    {
        GetZone(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, orientation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = old;
    }
}
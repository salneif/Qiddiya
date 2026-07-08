using UnityEngine;

public class WeightedObject : MonoBehaviour
{
    [Tooltip("The logical weight this object contributes to a pan. Not kilograms, just a number.")]
    [SerializeField] private float weight = 1f;

    [Tooltip("Optional ID so a scale can require *specific* objects, not just matching totals.")]
    [SerializeField] private string objectId = "";

    /// Fires whenever the weight changes so pans can recalculate.
    public event System.Action<WeightedObject> WeightChanged;

    /// <summary>
    /// Fired in OnDisable so a pan can drop this object immediately instead of
    /// waiting for its next timed scan. Lets removal be event-driven, not polled.
    /// </summary>
    public event System.Action<WeightedObject> Disabled;

    public string ObjectId => objectId;

    /// <summary>
    /// Preferred way for the magic script to change weight:
    ///   myObject.GetComponent<WeightedObject>().Weight = 0f;
    /// Setting this auto-notifies the scale.
    /// </summary>
    public float Weight
    {
        get => weight;
        set
        {
            if (Mathf.Approximately(weight, value)) return;
            weight = value;
            WeightChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Call this only if the magic script changed the weight some other way
    /// (e.g. wrote the serialized field directly) and you need to force a refresh.
    /// </summary>
    public void NotifyWeightChanged() => WeightChanged?.Invoke(this);

    // No OnEnable: this component has nothing to subscribe to on enable, and empty
    // Unity message methods still get invoked by the engine, so adding one would be
    // dead overhead. The lifecycle hook lives only where it does real work.
    private void OnDisable() => Disabled?.Invoke(this);
}
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Checkpoint trigger. When the player touches it, it becomes the player's
/// latest respawn point (via <see cref="PlayerKillable.SetRespawnPoint"/>).
///
/// Put this on a GameObject with a trigger Collider covering the checkpoint area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag of the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Spawn location")]
    [Tooltip("Optional exact spawn transform (e.g. a child marker). " +
             "If empty, this checkpoint's own transform is used.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Behaviour")]
    [Tooltip("If on, this checkpoint only registers once.")]
    [SerializeField] private bool triggerOnce = false;

    [Header("Events")]
    [Tooltip("Invoked when this checkpoint is activated (flag/sound/VFX...).")]
    [SerializeField] private UnityEvent onActivated;

    /// <summary>
    /// يُطلق عند تفعيل أي نقطة حفظ في السين. ساكنٌ عمدًا: من يريد أن يُعلن عنها —
    /// كـ<c>CheckpointFace</c> — يستمع مرّة واحدة بدل أن يُربط في كل نقطة بيدك،
    /// وإضافة نقطة جديدة لا تحتاج ربطًا أصلًا.
    /// </summary>
    public static event System.Action<Checkpoint> Activated;

    /// <summary>يونيتي يُبقي المشتركين بين تشغيلتين في المحرر.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Activated = null;

    private bool activated;

    private void Reset()
    {
        // Make sure the collider acts as a trigger when first added.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && activated) return;
        if (!other.CompareTag(playerTag)) return;

        var killable = other.GetComponentInParent<PlayerKillable>();
        if (killable == null) return;

        killable.SetRespawnPoint(spawnPoint != null ? spawnPoint : transform);
        activated = true;
        onActivated?.Invoke();
        Activated?.Invoke(this);
    }
}

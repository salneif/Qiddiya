using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lava / hazard kill zone. When the player enters, it optionally erupts
/// (via <see cref="onTriggered"/>) and then kills the player through
/// <see cref="PlayerKillable.Kill"/> — so the player burns (DeathDissolveEffect)
/// and respawns at the last checkpoint. The burn colour is set on the player's
/// dissolve material (Edge Color).
///
/// Put this on a GameObject with a trigger Collider covering the lava / deadly ground.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LavaKill : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag of the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Kill")]
    [Tooltip("Delay before the player dies, so the eruption can play (seconds).")]
    [SerializeField] private float killDelay = 0f;
    [Tooltip("Only kill once until the object is re-enabled.")]
    [SerializeField] private bool triggerOnce = false;

    [Header("Events")]
    [Tooltip("Fired the moment the player enters — erupt volcanoes / play sound / VFX.")]
    [SerializeField] private UnityEvent onTriggered;

    private bool used;

    private void Reset()
    {
        // Make sure the collider acts as a trigger when first added.
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used && triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        var killable = other.GetComponentInParent<PlayerKillable>();
        if (killable == null || killable.IsDead) return;

        used = true;
        onTriggered?.Invoke();

        if (killDelay > 0f)
            StartCoroutine(KillAfter(killable, killDelay));
        else
            killable.Kill();
    }

    private IEnumerator KillAfter(PlayerKillable killable, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!killable.IsDead)
            killable.Kill();
    }
}

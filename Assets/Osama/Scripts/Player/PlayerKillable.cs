using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Death/respawn handler for the player, Little Nightmares style.
/// Attach it to the player object WITHOUT touching the movement script
/// (the movement controller is owned by someone else).
///
/// On <see cref="Kill"/>:
///  - Disables any movement/input scripts listed in <see cref="disableOnDeath"/>.
///  - Plays the burn/dissolve death effect (if assigned).
///  - Fires <see cref="onDeath"/> (sound / black screen / camera shake...).
///  - Teleports the player to the current respawn point and reforms it alive.
///
/// The respawn point can be updated at runtime by a checkpoint via
/// <see cref="SetRespawnPoint"/>.
/// </summary>
public class PlayerKillable : MonoBehaviour
{
    [Header("Disabled on death")]
    [Tooltip("Movement/input scripts to disable while dead. Re-enabled on respawn.")]
    [SerializeField] private Behaviour[] disableOnDeath;

    [Header("Death effect (burn)")]
    [Tooltip("Optional DeathDissolveEffect on the same player for a fiery death.")]
    [SerializeField] private DeathDissolveEffect deathEffect;

    [Header("Events")]
    [Tooltip("Invoked once the moment the player dies.")]
    [SerializeField] private UnityEvent onDeath;
    [Tooltip("Invoked the moment the player respawns.")]
    [SerializeField] private UnityEvent onRespawn;

    [Header("Respawn")]
    [Tooltip("Automatically respawn the player after death.")]
    [SerializeField] private bool autoRespawn = true;
    [Tooltip("How long the player stays gone after burning, before returning (seconds).")]
    [SerializeField] private float respawnDelay = 0.6f;
    [Tooltip("Current respawn point. Updated by checkpoints at runtime. " +
             "If null, the player returns to its start position.")]
    [SerializeField] private Transform respawnPoint;

    /// <summary>Is the player currently dead? (used by enemies to avoid double-kills)</summary>
    public bool IsDead { get; private set; }

    /// <summary>The current respawn point (last checkpoint), or null.</summary>
    public Transform RespawnPoint => respawnPoint;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        if (deathEffect == null)
            deathEffect = GetComponent<DeathDissolveEffect>();
    }

    /// <summary>Sets the last checkpoint the player will respawn at.</summary>
    public void SetRespawnPoint(Transform point)
    {
        if (point != null)
            respawnPoint = point;
    }

    /// <summary>Kills the player: stops control, burns the body, then returns it to the checkpoint.</summary>
    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;

        SetControlEnabled(false);
        onDeath?.Invoke();

        StopAllCoroutines();
        StartCoroutine(DeathRoutine());
    }

    /// <summary>Instantly respawns the player at the checkpoint and restores control.</summary>
    public void Respawn()
    {
        StopAllCoroutines();
        MoveToSpawn();
        if (deathEffect != null) deathEffect.ResetImmediate();
        IsDead = false;
        SetControlEnabled(true);
        onRespawn?.Invoke();
    }

    private IEnumerator DeathRoutine()
    {
        // 1) Burn away
        if (deathEffect != null)
            yield return deathEffect.PlayDeath();

        if (!autoRespawn)
            yield break;

        // 2) Stay gone for a moment
        if (respawnDelay > 0f)
            yield return new WaitForSeconds(respawnDelay);

        // 3) Teleport to the checkpoint (while invisible)
        MoveToSpawn();

        // 4) Reform from the ashes
        if (deathEffect != null)
            yield return deathEffect.PlayReform();

        // 5) Give control back
        IsDead = false;
        SetControlEnabled(true);
        onRespawn?.Invoke();
    }

    private void MoveToSpawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRotation;
        TeleportTo(pos, rot);
    }

    /// <summary>
    /// Safe teleport: temporarily disables the CharacterController so it does not
    /// fight the position change.
    /// </summary>
    private void TeleportTo(Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
    }

    private void SetControlEnabled(bool value)
    {
        if (disableOnDeath == null) return;
        foreach (var b in disableOnDeath)
            if (b != null) b.enabled = value;
    }
}

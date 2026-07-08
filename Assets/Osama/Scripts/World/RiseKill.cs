using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kills the player when this moving piece (e.g. a rising bucket) reaches the top.
/// Put it on the moving piece, with a trigger Collider covering the dangerous area
/// where the player stands / rides. When the piece reaches the top marker (or the
/// kill Y) while a player is inside the trigger, that player dies via
/// <see cref="PlayerKillable.Kill"/> — so the burn shader + checkpoint respawn kick in.
/// (The piece is expected to be moved by something else: animation, physics, or a mover.)
/// </summary>
[RequireComponent(typeof(Collider))]
public class RiseKill : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Tag of the player object.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Kill point (top)")]
    [Tooltip("Marker at the top position. If set, the piece kills when it reaches this point.")]
    [SerializeField] private Transform topMarker;
    [Tooltip("If no marker is set, kill when this piece's world Y reaches this value.")]
    [SerializeField] private float killAtY = 0f;
    [Tooltip("How close (meters) counts as 'reached the top'.")]
    [SerializeField] private float reachThreshold = 0.15f;

    [Header("Behaviour")]
    [Tooltip("Only kill once.")]
    [SerializeField] private bool triggerOnce = true;

    private readonly List<PlayerKillable> onboard = new List<PlayerKillable>();
    private bool used;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        var k = other.GetComponentInParent<PlayerKillable>();
        if (k != null && !onboard.Contains(k)) onboard.Add(k);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        var k = other.GetComponentInParent<PlayerKillable>();
        if (k != null) onboard.Remove(k);
    }

    private void Update()
    {
        if (used && triggerOnce) return;
        if (onboard.Count == 0) return;
        if (!ReachedTop()) return;

        used = true;
        foreach (var k in onboard)
            if (k != null && !k.IsDead)
                k.Kill();
    }

    private bool ReachedTop()
    {
        if (topMarker != null)
            return Vector3.Distance(transform.position, topMarker.position) <= reachThreshold;
        return transform.position.y >= killAtY - reachThreshold;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (topMarker != null)
            Gizmos.DrawWireSphere(topMarker.position, reachThreshold);
        else
            Gizmos.DrawLine(new Vector3(transform.position.x - 2f, killAtY, transform.position.z),
                            new Vector3(transform.position.x + 2f, killAtY, transform.position.z));
    }
}

using System;
using UnityEngine;

public class PlateHiddenPlatformBehavior : MonoBehaviour
{


    // This event is public as to call
    public event Action Activated;

    // private as it cannot be viewed by other scripts (classes), but it can be seen by the inspector.
    [SerializeField] private LeverBehavior lever;
    [SerializeField] private string playerTag = "Player";
        [SerializeField] private LayerMask playerLayers = ~0; 

    private bool leverPulled;
    private bool playerOnPlatform;
    private Collider zone;


    private void Awake()
    {
        zone = GetComponent<Collider>();
    }

    // this class will subscribe to the event & code, so as to not cause a memory leak.
    private void OnEnable()
    {
        if (lever != null)
            lever.Pulled += HandleLeverPulled;
    }

    // this class will unsubscribe to the event & code, so as to not cause a memory leak.
    private void OnDisable()
    {
        if (lever != null)
            lever.Pulled -= HandleLeverPulled;
    }

    private void Update()
    {
        bool overlapping = IsPlayerOverlapping();

        // fire only on the frame the player first steps on
        if (overlapping && !playerOnPlatform)
            OnPlayerStepped();

        playerOnPlatform = overlapping;
    }

    private void HandleLeverPulled() => leverPulled = true;

    // This code runs when the player enters the platform.
    private bool IsPlayerOverlapping()
    {
        Bounds b = zone.bounds;
        Vector3 halfExtents = b.extents + new Vector3(0f, 1f, 0f);  // taller box upward

        Collider[] hits = Physics.OverlapBox(
            b.center + new Vector3(0f, 1f, 0f),   // shift center up so it grows upward
            halfExtents, transform.rotation,
            playerLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider c in hits)
        {
            if (c.gameObject == gameObject) continue;
            if (c.CompareTag(playerTag)) return true;
        }
        return false;
    }

    private void OnPlayerStepped()
    {
        Debug.Log($"[PLATE] Player stepped on. leverPulled = {leverPulled}");
        if (leverPulled)
            Activated?.Invoke();
        else
            Debug.Log("The platform clicks... but nothing happens.");
    }
}
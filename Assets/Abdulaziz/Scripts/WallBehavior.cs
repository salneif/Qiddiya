using System.Collections;
using UnityEngine;

public class WallBehavior : MonoBehaviour
{

    // private as it cannot be viewed by other scripts (classes), but it can be seen by the inspector.
    [SerializeField] private PlateHiddenPlatformBehavior plate;
    [SerializeField] private float dropDistance = 4f;
    [SerializeField] private float dropSpeed = 2f;

    private bool isLowered;

    // this class will subscribe to the event & code, so as to not cause a memory leak.
    private void OnEnable()
    {
        if (plate != null)
            plate.Activated += LowerWall;
    }

    // this class will unsubscribe to the event & code, so as to not cause a memory leak.
    private void OnDisable()
    {
        if (plate != null)
            plate.Activated -= LowerWall;
    }

    // This class is the entire logic behind the wall; it will check the "isLowered" and will check if its true. If it

    private void LowerWall()
    {
        Debug.Log("[WALL] Activated received.");
        if (isLowered) return;
            isLowered = true;


            StartCoroutine(LowerRoutine());
    }


        private IEnumerator LowerRoutine()
    {
        Vector3 end = transform.position + Vector3.up * dropDistance;

        while (Vector3.Distance(transform.position, end) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, end, dropSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = end;   
        Debug.Log("Script: WallBehavior works.");
    }

}
using Unity.VisualScripting;
using UnityEngine;

public class A_LongBoatRide : MonoBehaviour
{
    [SerializeField] private Vector3 endPoint;
    [SerializeField] private float speed;
    [SerializeField] private BoxCollider BoatCllider;

    private bool StartMoving = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            StartMoving = true;
            BoatCllider.enabled = true;
        }
    }

    private void Update()
    {
        if (StartMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, endPoint, speed * Time.deltaTime);
        }
    }

}

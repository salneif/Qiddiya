using Unity.VisualScripting;
using UnityEngine;

public class A_LongBoatRide : MonoBehaviour
{
    [SerializeField] private Transform endPoint;
    [SerializeField] private float speed;
    [SerializeField] private BoxCollider BoatCllider;

    [SerializeField]private bool StartMoving = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            StartMoving = true;
            BoatCllider.enabled = true;
        }
    }
    private void Awake()
    {
        
    }

    private void Update()
    {
        if (StartMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, endPoint.position, speed * Time.deltaTime);

            if(Vector3.Distance(transform.position, endPoint.position) < 0.1f)
            {
                transform.position = endPoint.position;
            }
        }
    }

}

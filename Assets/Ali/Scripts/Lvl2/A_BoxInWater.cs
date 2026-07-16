using UnityEngine;

public class A_BoxInWater : MonoBehaviour
{
    [SerializeField] private Transform Point1;
    [SerializeField] private Transform Point2;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private float boxSpeed;
    private void Awake()
    {
        targetPoint = Point1;
    }

    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPoint.position, boxSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.1)
        {
            if (targetPoint == Point1)
            {
                targetPoint = Point2;
            }
            else
            {
                targetPoint = Point1;
            }
        }
    }
}

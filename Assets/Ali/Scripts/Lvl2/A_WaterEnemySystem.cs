using UnityEngine;

public class A_WaterEnemySystem : MonoBehaviour
{
    [SerializeReference] private Transform player;
    [SerializeField] private float rotationSpeed;


    private void Update()
    {
        Vector3 direction = player.position - transform.position;


        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}

using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class Ai : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform[] Points;
   [SerializeField] private float distancetoFollow;





    private void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

       
        if (distance < distancetoFollow)
        {
            if (agent.destination != player.position)
            {
                agent.SetDestination(player.position);
            }
        }
        
        else if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            agent.SetDestination(Points[Random.Range(0, Points.Length)].position);
        }
    }
}

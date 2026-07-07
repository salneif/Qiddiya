using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class NormalEnemy : MonoBehaviour , IDamgeable
{
    [SerializeField] private float enemyHp;
    [SerializeField] private Transform playerPOS;

    private NavMeshAgent agent;
    private Stats staInstance;
    private FpController playerInstance;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }
    private void Start()
    {
        staInstance = Stats.instance;
        playerInstance = FpController.instance;
    }


    private void Update()
    {
        float distance = Vector3. Distance(transform.position, transform.forward);
        //  if(!agent.pathPending && distance   )
        agent.SetDestination(playerPOS.position);
    }


    public void TakeDamage(float amount)
    {
        enemyHp -= amount;
        if (enemyHp <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if(other.gameObject == playerInstance.gameObject)
        {
            staInstance.TakeDamage(20 * Time.deltaTime);

        }
    }
}

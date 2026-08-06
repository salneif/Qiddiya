using Unity.VisualScripting;
using UnityEngine;

public class A_LongBoatRide : MonoBehaviour
{
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform startPoint;
    [SerializeField] private float speed;



    [SerializeField]private bool StartMoving = false;

    //ref
    [SerializeField] private A_PlayerDeath_WaterSection a_PlayerDeath_WaterSection;


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            StartMoving = true;
        }
    }

    private void Awake()
    {
    }
    private void OnEnable()
    {
        a_PlayerDeath_WaterSection.OnPlayerDeath += WaterSection_OnPlayerDeath;
    }
    private void OnDisable()
    {
        a_PlayerDeath_WaterSection.OnPlayerDeath -= WaterSection_OnPlayerDeath;
    }

    private void WaterSection_OnPlayerDeath()
    {
       transform.position = startPoint.position;
        StartMoving = false;




    }

    private void Update()
    {
        if (StartMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, endPoint.position, speed * Time.deltaTime);

            if(Vector3.Distance(transform.position, endPoint.position) < 0.1f)
            {
                //transform.position = endPoint.position;
            }
        }
    }

}

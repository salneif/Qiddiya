using UnityEngine;

public class A_WaterEnemySystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float rotationSpeed;

    [Header("Water System")]
    [SerializeField] private float timeAboveWater;
    [SerializeField] private float timeUnderWater;
    [SerializeField] private bool isUnderWater;
    private float _CountDown;
    


    private Animator animator;



    private void Awake()
    {
        animator = GetComponent<Animator>();
    }
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

    public void OnUnderWaterWater()
    {

    }
}

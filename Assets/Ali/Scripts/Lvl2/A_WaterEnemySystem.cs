using UnityEngine;

public class A_WaterEnemySystem : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float rotationSpeed;

    [Header("Water System")]
    [SerializeField] private float timeAboveWater;
    [SerializeField] private float timeUnderWater;
    [SerializeField] private float _CountDown;

    public bool isUnderWater;



    private Animator animator;



    private void Awake()
    {
        animator = GetComponent<Animator>();
        isUnderWater = false;
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


        if (_CountDown > 0)
        {
            _CountDown -= Time.deltaTime;

            if (_CountDown <= 0)
            {
                if (isUnderWater)
                {
                    animator.SetBool("isAboveWater", true);
                    _CountDown = -99;
                }
                else if (!isUnderWater)
                {
                    animator.SetBool("isAboveWater", false);
                    _CountDown = -99;

                }
            }
        }
    }

    public void OnUnderWaterStart()
    {
        isUnderWater = true;
        _CountDown = timeUnderWater;
    }

    public void OnAboveWaterStart()
    {
        isUnderWater = false;
        _CountDown = timeAboveWater;
    }
}

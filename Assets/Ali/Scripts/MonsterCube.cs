using MoreMountains.Feedbacks;
using UnityEngine;

public class MonsterCube : MonoBehaviour
{
    [SerializeField] private Animator animator;
     public int state = 0;
    [SerializeField] private float timer;
    [SerializeField]private float currentTime;


    //Feedback
    [SerializeField] private MMF_Player salmFeedback;
    [SerializeField] private MMF_Player xFeedback;
    [SerializeField] private MMF_Player yFeedback;
    [SerializeField] private MMF_Player zFeedback;


    private void Awake()
    {
        animator = GetComponent<Animator>();

        currentTime = timer;
    }


    private void Update()
    {

        if (currentTime >= 0)
        {
            currentTime -= Time.deltaTime;
            Debug.Log(Mathf.Ceil(currentTime));
        }
        else 
        {

            if (state == 0)
            {
                state = 1;
            }
            else if (state == 1)
            {
                state = 2;
            }
            else
            {
                state = 0;
            }
            animator.SetInteger("StateNumber", state);

        }
    }

    public void HandleAnimation()
    {
        currentTime = timer;
        if (state == 0)
        {
           xFeedback.PlayFeedbacks();
        }
        else if (state == 1)
        {
            yFeedback.PlayFeedbacks();
        }
        else
        {
            zFeedback.PlayFeedbacks();
        }
    }

    public void SlamHitFrame()
    {
        salmFeedback.PlayFeedbacks();
    }


}

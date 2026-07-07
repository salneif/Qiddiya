using MoreMountains.Feedbacks;
using UnityEngine;

public class PlayerInstance : MonoBehaviour , IDamgeable
{
   public static PlayerInstance instance {  get; private set; }

    [SerializeField] private float hp;

    // feedbacks
    [SerializeField] private MMF_Player hurtFeedBack;
    [SerializeField] private MMF_Player deathFeedback;
    private void Awake()
    {
        instance = this;
    }

    public void TakeDamage(float amount)
    {
        hp -= amount;

        if(hp<= 0)
        {

        }
        else
        {
            hurtFeedBack.PlayFeedbacks();
        }
    }
}

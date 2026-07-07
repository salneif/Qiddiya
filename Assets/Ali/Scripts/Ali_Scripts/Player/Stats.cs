using TMPro;
using UnityEngine;

public class Stats : MonoBehaviour , IDamgeable
{
    public static Stats instance { get; private set; }



    [SerializeField] private float hp;
    


    [Header("TMP REF HERE")]
    [SerializeField] private TMP_Text text_HP;


    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }
    public void TakeDamage(float amount)
    {
        hp -= amount;
    }

    private void Update()
    {
        if(text_HP == null)
        return;
        text_HP.text = hp.ToString();

        if (Input.GetKeyDown(KeyCode.H))
        {
            hp -= 20;
        }
    }
}

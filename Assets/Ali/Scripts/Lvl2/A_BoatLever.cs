using UnityEngine;

public class A_BoatLever : MonoBehaviour
{
    [SerializeField] private GameObject rightSideWood;
    [SerializeField] private GameObject leftSideWood;

    [SerializeField] private int State = 0;

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {

        }
    }
}


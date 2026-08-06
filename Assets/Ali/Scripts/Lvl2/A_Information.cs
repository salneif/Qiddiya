using UnityEngine;

public class A_Information : MonoBehaviour
{
    [SerializeField] private GameObject infoText;

    private void OnTriggerStay(Collider other)
    {
        infoText.SetActive(true);
    }
    private void OnTriggerExit(Collider other)
    {
        infoText.SetActive(false);
    }
}

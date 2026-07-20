using UnityEngine;

public class ExitActivate : MonoBehaviour
{
    [SerializeField] private GameObject objectToActivate;
    [SerializeField] private GameObject objectToDeactivate;

    private void OnTriggerExit(Collider other)
    {
        objectToDeactivate.SetActive(false);
        objectToActivate.SetActive(true);
    }
}

using UnityEngine;

public class ActivateObject : MonoBehaviour
{
    [SerializeField] private GameObject objectToActivate;
    [SerializeField] private GameObject objectToDeactivate;

    private void OnTriggerEnter(Collider other)
    {
        objectToDeactivate.SetActive(false);
        objectToActivate.SetActive(true);
    }
}

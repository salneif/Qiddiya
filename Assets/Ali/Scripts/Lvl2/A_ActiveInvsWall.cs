using UnityEngine;

public class A_ActiveInvsWall : MonoBehaviour
{
    [SerializeField] private BoxCollider InvsWall;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            InvsWall.enabled = true;
        }
    }
}

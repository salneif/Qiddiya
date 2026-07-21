using UnityEngine;

public class MonsterOff : MonoBehaviour
{
    [SerializeField] private MonoBehaviour scriptOne;
    [SerializeField] private MonoBehaviour scriptTwo;
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (scriptOne != null) scriptOne.enabled = false;
            if (scriptTwo != null) scriptTwo.enabled = false;
        }
    }
}

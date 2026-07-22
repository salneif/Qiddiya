using UnityEngine;

public class A_GroundCheck : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private LayerMask PlatformTriggers;



    private void Update()
    {
      RaycastHit[] hits = Physics.RaycastAll(player.position, -transform.up);
        
        foreach (RaycastHit hit in hits)
        {
            if(hit.collider.gameObject.layer != playerMask && hit.collider.gameObject.layer != PlatformTriggers)
            {
                transform.position = hit.point;
                return;
            }
           
        }
        
    }

}

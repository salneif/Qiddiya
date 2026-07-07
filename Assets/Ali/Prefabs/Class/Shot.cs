using UnityEngine;

public class Shot : MonoBehaviour
{
    [SerializeField] private GameObject bullet;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float bulletSpeed;
    [SerializeField] private float forceOntheGun;


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
        GameObject bulletInstance = Instantiate(bullet, spawnPoint.position, spawnPoint.rotation);
        bulletInstance.GetComponent<Rigidbody>().linearVelocity = bulletSpeed * spawnPoint.forward;
            
        }
    }
}

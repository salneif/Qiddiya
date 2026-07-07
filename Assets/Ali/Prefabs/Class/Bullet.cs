using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float timer;


    private void Update()
    {
        timer -= Time.deltaTime;
       if (timer < 0)
        {
            Destroy(gameObject);
        }
    }

    
    private void OnCollisionEnter(Collision hit)
    {
        {
            if (hit.gameObject.CompareTag("Enemy"))
            {
                Destroy(hit.gameObject);
                Destroy(gameObject);

            }

        }
    }
}

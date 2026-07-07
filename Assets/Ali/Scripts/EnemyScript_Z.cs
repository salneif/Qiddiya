using MoreMountains.Feedbacks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class EnemyScript_Z : MonoBehaviour , IDamgeable
{
    [SerializeField] private PlayerInstance player;
    [SerializeField] private float hp;
    [SerializeField] private float speed;
   // [SerializeField] private Rigidbody rb;
    [SerializeField] private float speedBoost;
    [SerializeField] private BoxCollider box;
    [SerializeField] private MeshRenderer mesh;



    // feedbacks
    [SerializeField] private MMF_Player hit;
    private void Start()
    {
        player = PlayerInstance.instance;
       // rb = GetComponent<Rigidbody>();

        InvokeRepeating("BoostSpeed", 1f, 1f);
    }


    private void Update()
    {
      //  Vector3 direction = (player.transform.position - transform.position).normalized;
       // direction.y = 0;
      //  rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y , direction.z * speed);

        Vector3 directionZ = new Vector3(player.transform.position.x, transform.position.y , player.transform.position.z);

        transform.position = Vector3.MoveTowards(transform.position, directionZ, speed * Time.deltaTime);



    }

    private void BoostSpeed()
    {
        speed += 1f;
    }


    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0)
        {
            box.enabled = false;
            mesh.enabled = false;
            hit.PlayFeedbacks();
            Invoke("DestroySelf", 2);
        }
    }

    private void DestroySelf()
    {
        Destroy(gameObject);

    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject == player.gameObject)
        {
            player.TakeDamage(25);
            Destroy(gameObject);
        }
    }
}

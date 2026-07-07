using MoreMountains.Feedbacks;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class EnemyScript : MonoBehaviour , IDamgeable
{
    [SerializeField] private PlayerInstance player;
    [SerializeField] private float hp;
    [SerializeField] private float speed;
    [SerializeField] private BoxCollider box;
    [SerializeField] private MeshRenderer mesh;
    

    public event EventHandler WeScored;


    // feedbacks
    [SerializeField] private MMF_Player hit;
    private void Start()
    {
        player = PlayerInstance.instance;
    }


    private void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, player.transform.position, speed * Time.deltaTime);


    }

    public void TakeDamage(float amount)
    {
       hp -= amount;
        if (hp <= 0)
        {
            box.enabled = false;
            mesh.enabled = false;
            hit.PlayFeedbacks();
            WeScored?.Invoke(this,EventArgs.Empty);
            Invoke("DestroySelf", 2);
        }
    }

    private void DestroySelf()
    {
        Destroy(gameObject);

    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject == player.gameObject)
        {
            player.TakeDamage(25);
            Destroy(gameObject);
        }
    }
}

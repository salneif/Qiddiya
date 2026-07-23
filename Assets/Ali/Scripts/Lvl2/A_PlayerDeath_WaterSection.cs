using System;
using UnityEngine;

public class A_PlayerDeath_WaterSection : MonoBehaviour
{

   [SerializeField] private Animator animator;
    private PlayerController playerController;
    [SerializeField] private Material deathMatrial;
    [SerializeField] private GameObject mesh;

    private Renderer rend;


    // ref for death triggers 
    [SerializeField] private A_WaterDeathZone waterDeathZone;
    [SerializeField] private ParticleSystem waterSplash;
    public  event Action OnPlayerFall;
    public event Action OnPlayerDeath;

    

    private void Start()
    {
        playerController = PlayerController.instance;
    }
    private void OnEnable()
    {
        // we subscrip to every event that kill the player in 
        //#1
        A_WaterSection.OnEnemySeeingPlayer += OnEnemySeeingPlayer;

        //#2
        waterDeathZone.OnPlayerDrowning += OnPlayerDrowning;
    }

  

    private void OnDisable()
    {
        // we unsub here so we dont have boom boom cpu 
        A_WaterSection.OnEnemySeeingPlayer -= OnEnemySeeingPlayer;
        waterDeathZone.OnPlayerDrowning -= OnPlayerDrowning;
    }
    private void OnPlayerDrowning()
    {
        // death material
        rend = mesh.GetComponent<Renderer>();
        rend.material = deathMatrial;


       // waterSplash.Play();
        Instantiate(waterSplash, transform.position ,transform.rotation);
        OnPlayerFall?.Invoke();


        // stop moving
        animator.SetBool("InAir", false);
        playerController.CanMove = false;


        animator.SetTrigger("Death");

        
        StartPlayerDeathSystem();
    }

    private void OnEnemySeeingPlayer()
    {
        // death material
         rend = mesh.GetComponent<Renderer>();
        rend.material = deathMatrial;

        // stop moving
        animator.SetBool("InAir", false);
        playerController.CanMove = false;


        animator.SetTrigger("Death");

        Invoke("StartPlayerDeathSystem", 5);
        
    }
    private void StartPlayerDeathSystem()
    {
        OnPlayerDeath?.Invoke();
    }

    private void Update()
    {
       
    }
}

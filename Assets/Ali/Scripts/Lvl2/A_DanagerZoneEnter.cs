using UnityEngine;

public class A_DanagerZoneEnter : MonoBehaviour
{
    [SerializeField] private AudioSource wishpers;
    [SerializeField] private bool isFirstTimeIn;
    [SerializeField] private bool originalState;
    [SerializeField] private A_PlayerDeath_WaterSection a_PlayerDeath_WaterSection;

    private void Awake()
    {
        originalState = isFirstTimeIn;
    }
    private void OnEnable()
    {
        a_PlayerDeath_WaterSection.OnPlayerDeath += WaterSection_OnPlayerDeath;
    }
    private void OnDisable()
    {
        a_PlayerDeath_WaterSection.OnPlayerDeath -= WaterSection_OnPlayerDeath;
    }
    private void WaterSection_OnPlayerDeath()
    {
        wishpers.Stop();
        isFirstTimeIn = originalState;
    }

    private void OnTriggerEnter(Collider other)
    {
       
        if (isFirstTimeIn)
        {
            wishpers.Play();
            isFirstTimeIn = false;
        }
        else
        {
            wishpers.Stop();
            isFirstTimeIn = true;
        }
    }
}

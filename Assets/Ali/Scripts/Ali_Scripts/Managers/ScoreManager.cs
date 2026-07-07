using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;
    private int score = 0;
    private GunManager gunManager;


    [SerializeField] private TMP_Text scoreNumber;
    


    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        gunManager = GunManager.instance;
    }
    private void Update()
    {
        scoreNumber.text = score.ToString();

    }


    public void AddHitScore(int amount)
    {
        score += amount;
    }
    public void AddKillScore(int amount)
    {
        score += amount;
    }

}

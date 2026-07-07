using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class GameManger : MonoBehaviour
{
   [SerializeField] private List<GameObject> allSpawnPoints = new List<GameObject>();
   [SerializeField] private List<GameObject> allSpawnPoints_Z = new List<GameObject>();

    [SerializeField] private GameObject enemyPrefabX;
    [SerializeField] private GameObject enemyPrefabY;
    [SerializeField] private GameObject enemyPrefabZ;

    [SerializeField] private MonsterCube MonsterCube;

    [SerializeField] private EnemyScript enemy;
    [SerializeField] private TextMeshProUGUI textMeshPro;
    [SerializeField] private int score = 0;


    private void Start()
    {
        GameObject[] arrayOfPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        GameObject[] arrayOfPoints_Z = GameObject.FindGameObjectsWithTag("SpawnPointZ");


        foreach (GameObject point in arrayOfPoints)
        {
            allSpawnPoints.Add(point);
        }
        foreach (GameObject point in arrayOfPoints_Z)
        {
            allSpawnPoints_Z.Add(point);
        }

        InvokeRepeating("SpawnEnemy", 2, 2);// after the first one it will take 4s to spawn new enemy


        enemy.WeScored += Enemy_WeScored;

    }

    private void Enemy_WeScored(object sender, System.EventArgs e)
    {
        score++;
        textMeshPro.text = score.ToString();

    }

    private void Update()
    {

    }



    private void SpawnEnemy()
    {
        int enemytype = Random.Range(0,3);
        if (enemytype == 0)
        {
            int randomPoint = Random.Range(0, allSpawnPoints.Count);
            GameObject targetSpawnPoint = allSpawnPoints[randomPoint];


            Instantiate(enemyPrefabX, targetSpawnPoint.transform.position, Quaternion.identity);
        }
        else if (enemytype == 1)
        {
            int randomPoint = Random.Range(0, allSpawnPoints.Count);
            GameObject targetSpawnPoint = allSpawnPoints[randomPoint];


            Instantiate(enemyPrefabY, targetSpawnPoint.transform.position, Quaternion.identity);
        }
        else if (enemytype == 2)
        {
            int randomPoint = Random.Range(0, allSpawnPoints_Z.Count);
            GameObject targetSpawnPoint = allSpawnPoints_Z[randomPoint];


            Instantiate(enemyPrefabZ, targetSpawnPoint.transform.position, Quaternion.identity);
        }
    }
}

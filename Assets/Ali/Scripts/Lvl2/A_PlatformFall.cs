using UnityEngine;
using System.Collections;
using TMPro;

public class A_PlatformFall : MonoBehaviour
{
    [SerializeField] private Transform Platform;
    [SerializeField] private float timeToFall;
    [SerializeField] private Transform fallDownPlace;
    [SerializeField] private Transform startPlace;
    [SerializeField] private float speedTofall;
    [SerializeField] private float speedToReturn;



    private void Awake()
    {
        startPlace.position = transform.position;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            StartCoroutine(HandleFallDown());
        }
    }
    private IEnumerator HandleFallDown()
    {
        yield return new WaitForSeconds(timeToFall);
        while( Vector3.Distance (Platform.position , fallDownPlace.position) > 0.1f)
        {
            Platform.position = Vector3.MoveTowards(Platform.position, fallDownPlace.position, speedTofall * Time.deltaTime);

            yield return null;
        }

        yield return new WaitForSeconds(speedToReturn);
        while (Vector3.Distance(Platform.position, startPlace.position) > 0.1f)
        {
            Platform.position = Vector3.MoveTowards(Platform.position, startPlace.position, speedToReturn * Time.deltaTime);

            yield return null;
        }
    }


}

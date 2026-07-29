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

    [Header("ReactionSetting")]
   [SerializeField] private Transform recationGoPoint;
    [SerializeField] private float recationSpeed;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {

            StartCoroutine( HnadleReaction());
            StartCoroutine(HandleFallDown());
            
        }
    }
    private void Update()
    {
    }
    private IEnumerator HnadleReaction()
    {
        while (Vector3.Distance(Platform.position, recationGoPoint.position) > 0.1f)
        {
            Platform.position = Vector3.MoveTowards(Platform.position, recationGoPoint.position, recationSpeed * Time.deltaTime);
            yield return null;
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

        yield return new WaitForSeconds(2);
        while (Vector3.Distance(Platform.position, startPlace.position) > 0.1f)
        {
            Platform.position = Vector3.MoveTowards(Platform.position, startPlace.position, speedToReturn * Time.deltaTime);

            yield return null;
        }
    }


}

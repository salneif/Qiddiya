using System.Collections.Generic;
using UnityEngine;

public class A_ChangeWorld : MonoBehaviour
{
    [SerializeField] private AudioSource soundtrack;
    [SerializeField] private GameObject dirLight;
    [SerializeField] private Material normalSkybox;
    [SerializeField] private Material evilSkybox;
    [SerializeField] private bool test;

    private bool firstTime;

    private List<GameObject> lightObjects;
    private List<GameObject> darkObjects;

    private void Update()
    {
        
        if (test)
        {

            // light
            RenderSettings.reflectionIntensity = 0;
            dirLight.SetActive(false);
            RenderSettings.skybox = evilSkybox;


            foreach (GameObject obj in lightObjects)
            {
                obj.SetActive(false);
            }
            foreach (GameObject obj in darkObjects)
            {
                obj?.SetActive(true);
            }
            soundtrack.pitch = 0.3f;

        }
        else
        {
            // light
            RenderSettings.reflectionIntensity = 1;
            dirLight.SetActive(true);
            RenderSettings.skybox = normalSkybox;


            foreach (GameObject obj in darkObjects)
            {
                obj.SetActive(false);
            }
            foreach (GameObject obj in lightObjects)
            {
                obj.SetActive(true);
            }
            soundtrack.pitch = 1f;
        }
     }
    private void Start()
    {
        GameObject[] foundLights = GameObject.FindGameObjectsWithTag("LightObject");
        lightObjects = new List<GameObject>(foundLights);

        GameObject[] foundDark = GameObject.FindGameObjectsWithTag("DarkObject");
        darkObjects = new List<GameObject>(foundDark);
    }
}

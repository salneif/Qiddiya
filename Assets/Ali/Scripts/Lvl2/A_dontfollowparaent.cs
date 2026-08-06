using TreeEditor;
using UnityEngine;

public class A_dontfollowparaent : MonoBehaviour
{

    private Quaternion originalRotation;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalRotation = transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = originalRotation;
    }
}

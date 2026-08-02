using UnityEngine;

public class A_TurnPlayerControllerOffAndOnAtStart : MonoBehaviour
{
    [SerializeField] private PlayerController controller;


    private void Start()
    {
        controller.enabled = false;
        controller.enabled = true;
    }
}

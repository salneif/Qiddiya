using UnityEngine;

public class LavaFlowTest : MonoBehaviour
{
    [SerializeField] private LavaFlow lavaFlow;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
            lavaFlow.StartFlow();

        if (Input.GetKeyDown(KeyCode.R))
            lavaFlow.ResetFlow();
    }
}
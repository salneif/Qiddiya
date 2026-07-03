using UnityEngine;

public class PressureTest : MonoBehaviour
{
    [SerializeField] private PressureMeter meter;
    [SerializeField] private SteamPipe pipe;
    [SerializeField] private float testPressure;

    void Update()
    {
        if (meter != null) meter.SetPressure(testPressure);
        if (pipe != null) pipe.SetPressure(testPressure);
    }
}
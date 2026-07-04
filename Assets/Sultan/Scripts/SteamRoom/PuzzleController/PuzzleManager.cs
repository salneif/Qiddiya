using UnityEngine;
using System;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private Lever[] levers;
    [SerializeField] private PressureMeter meter;
    [SerializeField] private SteamPipe pipe;
    [SerializeField] private float targetPressure = 85f;
    [SerializeField] private float tolerance = 2f;
    [SerializeField] private bool debug;

    public event Action OnPuzzleSolved;

    private bool _solved;

    void OnEnable()
    {
        foreach (var l in levers)
        {
            l.OnLeverActivated += onLeverChanged;
            l.OnLeverDeactivated += onLeverChanged;
        }
    }

    void OnDisable()
    {
        foreach (var l in levers)
        {
            l.OnLeverActivated -= onLeverChanged;
            l.OnLeverDeactivated -= onLeverChanged;
        }
    }

    void onLeverChanged(Lever lever)
    {
        if (_solved) return;

        float total = 0f;
        foreach (var l in levers)
            if (l.IsOn) total += l.PressureValue;

        if (debug)
            Debug.Log($"[Puzzle] {lever.ID} {(lever.IsOn ? "on" : "off")} | total={total}");

        meter.SetPressure(total);
        pipe.SetPressure(total);

        if (Mathf.Abs(total - targetPressure) <= tolerance)
        {
            _solved = true;
            Debug.Log("[Puzzle] SOLVED");
            OnPuzzleSolved?.Invoke();
        }
    }

    public bool IsSolved => _solved;
}

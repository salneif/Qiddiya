using UnityEngine;
using System;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private PressurePlate[] plates;
    [SerializeField] private PressureMeter meter;
    [SerializeField] private SteamPipe pipe;
    [SerializeField] private float targetPressure = 85f;
    [SerializeField] private float tolerance = 2f;
    [SerializeField] private bool debug;

    public event Action OnPuzzleSolved;

    private bool _solved;

    void OnEnable()
    {
        foreach (var p in plates)
        {
            p.OnPlatePressed += onPlateChanged;
            p.OnPlateReleased += onPlateChanged;
        }
    }

    void OnDisable()
    {
        foreach (var p in plates)
        {
            p.OnPlatePressed -= onPlateChanged;
            p.OnPlateReleased -= onPlateChanged;
        }
    }

    void onPlateChanged(PressurePlate plate)
    {
        if (_solved) return;

        float total = 0f;
        bool allBoxes = true;

        foreach (var p in plates)
        {
            if (!p.IsPressed) continue;
            total += p.PressureValue;
            if (!p.HasBox) allBoxes = false;
        }

        if (debug)
            Debug.Log($"[Puzzle] {plate.ID} {(plate.IsPressed ? "pressed" : "released")} | total={total} | allBoxes={allBoxes}");

        meter.SetPressure(total);
        pipe.SetPressure(total);

        if (Mathf.Abs(total - targetPressure) <= tolerance && allBoxes)
        {
            _solved = true;
            Debug.Log("[Puzzle] SOLVED");
            OnPuzzleSolved?.Invoke();
        }
    }

    public bool IsSolved => _solved;
}
using UnityEngine;
using System;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] private Lever[] levers;
    [SerializeField] private ClockFace clock;
    [SerializeField] private float targetMinutes = 180f;
    [SerializeField] private float tolerance = 2f;
    [SerializeField] private bool debug;

    // win animation
    [SerializeField] private Transform platform;
    [SerializeField] private float rotateSpeed = 30f;
    [SerializeField] private float riseSpeed = 0.5f;
    [SerializeField] private float riseHeight = 3f;

    public event Action OnPuzzleSolved;

    private bool _solved;
    private float _risen;

    void OnEnable()
    {
        foreach (var l in levers)
            l.OnLeverChanged += onLeverChanged;
    }

    void OnDisable()
    {
        foreach (var l in levers)
            l.OnLeverChanged -= onLeverChanged;
    }

    void Update()
    {
        if (!_solved || platform == null) return;

        platform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f);

        if (_risen < riseHeight)
        {
            float step = Mathf.Min(riseSpeed * Time.deltaTime, riseHeight - _risen);
            platform.position += Vector3.up * step;
            _risen += step;
        }
    }

    void onLeverChanged(Lever lever)
    {
        if (_solved) return;

        float total = 0f;
        foreach (var l in levers)
            total += l.PressureContribution;

        if (debug)
            Debug.Log($"[Puzzle] {lever.ID} {lever.State} | total={total} mins");

        clock.SetMinutes(total);

        if (Mathf.Abs(total - targetMinutes) <= tolerance)
        {
            _solved = true;
            Debug.Log("SOLVED");
            OnPuzzleSolved?.Invoke();
        }
    }

    public bool IsSolved => _solved;
}
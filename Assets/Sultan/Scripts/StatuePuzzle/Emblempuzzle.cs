using UnityEngine;
using UnityEngine.Events;

public class EmblemPuzzle : MonoBehaviour
{
    [SerializeField] private StatuePedestal[] pedestals;
    [SerializeField] private EmblemShape[] shapes;
    [SerializeField] private float valueTolerance = 0.01f;
    [SerializeField] private bool waitForSettle = true;
    [SerializeField] private bool lockPedestalsOnSolve = true;
    [SerializeField] private bool debug;

    public UnityEvent OnSolved;

    private bool _solved;

    void OnEnable()
    {
        for (int i = 0; i < pedestals.Length; i++)
            if (pedestals[i] != null) pedestals[i].OnAnimalChanged += onPedestalChanged;
    }

    void OnDisable()
    {
        for (int i = 0; i < pedestals.Length; i++)
            if (pedestals[i] != null) pedestals[i].OnAnimalChanged -= onPedestalChanged;
    }

    void Start()
    {
        validateSetup();
        refresh();
    }

    void Update()
    {
        if (_solved) return;
        if (!isCorrect()) return;
        if (waitForSettle && !allSettled()) return;

        solve();
    }

    void onPedestalChanged(StatuePedestal pedestal)
    {
        refresh();
    }

    void refresh()
    {
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] == null) continue;
            bool placed = tryHostValue(shapes[i].BoundAnimal, out float value);
            shapes[i].SetHost(value, placed);
        }

        if (debug) Debug.Log($"[Emblem] {stateString()}");
    }

    bool tryHostValue(StatuePedestal.Animal a, out float value)
    {
        value = 0f;
        if (a == StatuePedestal.Animal.None) return false;

        for (int i = 0; i < pedestals.Length; i++)
        {
            if (pedestals[i] == null) continue;
            if (pedestals[i].Held != a) continue;

            value = pedestals[i].RotationValue;
            return true;
        }

        return false;
    }

    bool isCorrect()
    {
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] == null) return false;
            if (!tryHostValue(shapes[i].BoundAnimal, out float value)) return false;
            if (Mathf.Abs(value - shapes[i].RequiredValue) > valueTolerance) return false;
        }

        return shapes.Length > 0;
    }

    bool allSettled()
    {
        for (int i = 0; i < shapes.Length; i++)
            if (shapes[i] != null && !shapes[i].IsSettled) return false;

        return true;
    }

    void solve()
    {
        _solved = true;

        if (lockPedestalsOnSolve)
        {
            for (int i = 0; i < pedestals.Length; i++)
                if (pedestals[i] != null) pedestals[i].Lock();
        }

        Debug.Log("[Emblem] SOLVED");
        OnSolved?.Invoke();
    }

    void validateSetup()
    {
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] == null) continue;

            bool reachable = false;
            for (int p = 0; p < pedestals.Length; p++)
            {
                if (pedestals[p] == null) continue;
                if (Mathf.Abs(pedestals[p].RotationValue - shapes[i].RequiredValue) <= valueTolerance)
                {
                    reachable = true;
                    break;
                }
            }

            if (!reachable)
                Debug.LogWarning($"[Emblem] {shapes[i].name} requires {shapes[i].RequiredValue} but no pedestal has that value. Puzzle is unsolvable.", shapes[i]);
        }

        for (int i = 0; i < shapes.Length; i++)
            for (int j = i + 1; j < shapes.Length; j++)
                if (shapes[i] != null && shapes[j] != null && shapes[i].BoundAnimal == shapes[j].BoundAnimal)
                    Debug.LogWarning($"[Emblem] {shapes[i].name} and {shapes[j].name} are both bound to {shapes[i].BoundAnimal}.", shapes[i]);
    }

    string stateString()
    {
        string s = "";
        for (int i = 0; i < shapes.Length; i++)
        {
            if (shapes[i] == null) continue;
            bool placed = tryHostValue(shapes[i].BoundAnimal, out float value);
            s += $"{shapes[i].BoundAnimal}={(placed ? value.ToString() : "off")}/{shapes[i].RequiredValue} ";
        }
        return s;
    }

    [ContextMenu("Log State")]
    void logState()
    {
        Debug.Log($"[Emblem] {stateString()} | correct={isCorrect()} settled={allSettled()} solved={_solved}");
    }

    public bool IsSolved => _solved;
}
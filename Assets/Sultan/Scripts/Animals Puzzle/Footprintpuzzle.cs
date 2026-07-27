using UnityEngine;

public class FootprintPuzzle : MonoBehaviour
{
    [SerializeField] private Transform[] slots;
    [SerializeField] private AnimalPlate[] plates;
    [SerializeField] private CluePlate[] clues;
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float settleDelay = 0.05f;
    [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0f, -0.6f);
    [SerializeField] private bool swapOnOccupied = true;
    [SerializeField] private bool debug = true;
    [SerializeField] private bool debugKeys = true;

    private AnimalPlate.AnimalId[] _solution;
    private float _lockTimer;
    private bool _locked;

    void Start()
    {
        if (!validate()) { enabled = false; return; }

        _solution = new AnimalPlate.AnimalId[clues.Length];
        for (int i = 0; i < clues.Length; i++)
            _solution[i] = clues[i].Id;

        for (int i = 0; i < plates.Length; i++)
            plates[i].SnapTo(slots[plates[i].StartSlot], plates[i].StartSlot);

        if (debug) logState("start");
    }

    void Update()
    {
        if (_lockTimer > 0f)
        {
            _lockTimer -= Time.deltaTime;
            if (_lockTimer <= 0f) onSettled();
        }

        if (debugKeys) readDebugKeys();
    }

    public void Advance(AnimalPlate plate)
    {
        if (plate == null || _locked || IsMoving) return;

        int from = plate.SlotIndex;
        if (from < 0) return;

        int to = (from + 1) % slots.Length;

        AnimalPlate displaced = Occupant(to);
        bool swapping = displaced != null && swapOnOccupied;

        if (swapping)
            displaced.MoveTo(slots[from], from, slideDuration, Vector3.zero);

        plate.MoveTo(slots[to], to, slideDuration, travelOffset);
        _lockTimer = slideDuration + settleDelay;
    }

    void onSettled()
    {
        if (debug) logState("settled");
    }

    void readDebugKeys()
    {
        for (int i = 0; i < plates.Length && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Advance(plates[i]);
    }

    bool arrangementMatchesSolution()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            AnimalPlate p = Occupant(i);
            if (p == null || p.Id != _solution[i]) return false;
        }
        return true;
    }

    bool validate()
    {
        if (slots == null || plates == null || clues == null || slots.Length < 2)
        {
            return false;
        }

        if (plates.Length != slots.Length || clues.Length != slots.Length)
        {
            return false;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || plates[i] == null || clues[i] == null)
            {
                return false;
            }
        }

        bool[] taken = new bool[slots.Length];
        for (int i = 0; i < plates.Length; i++)
        {
            int slot = plates[i].StartSlot;

            if (slot < 0 || slot >= slots.Length)
            {
                return false;
            }

            if (taken[slot])
            {
                return false;
            }

            taken[slot] = true;

            for (int j = i + 1; j < plates.Length; j++)
            {
                if (plates[i].Id == plates[j].Id)
                {
                    return false;
                }
            }
        }

        for (int i = 0; i < clues.Length; i++)
        {
            bool carried = false;
            for (int j = 0; j < plates.Length; j++)
                if (plates[j].Id == clues[i].Id) carried = true;

            if (!carried)
            {
                return false;
            }

            for (int j = i + 1; j < clues.Length; j++)
            {
                if (clues[i].Id == clues[j].Id)
                {
                    return false;
                }
            }
        }

        return true;
    }

    void logState(string label)
    {
        string current = "";
        string wanted = "";

        for (int i = 0; i < slots.Length; i++)
        {
            string sep = i < slots.Length - 1 ? ", " : "";
            AnimalPlate p = Occupant(i);
            current += (p != null ? p.Id.ToString() : "empty") + sep;
            wanted += _solution[i] + sep;
        }
    }

    void OnDrawGizmos()
    {
        if (slots == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            Gizmos.DrawWireCube(slots[i].position, new Vector3(2f, 1.6f, 0.15f));
        }
    }

    public AnimalPlate Occupant(int index)
    {
        for (int i = 0; i < plates.Length; i++)
            if (plates[i].SlotIndex == index) return plates[i];
        return null;
    }

    public void Lock() => _locked = true;

    public int SlotCount => slots.Length;
    public Transform Slot(int index) => slots[index];
    public bool IsMoving => _lockTimer > 0f;
    public bool IsLocked => _locked;
}
using UnityEngine;

public class PlateButton : MonoBehaviour
{
    [SerializeField] private FootprintPuzzle puzzle;
    [SerializeField] private AnimalPlate plate;
    [SerializeField] private bool bindToSlot;
    [SerializeField] private int slotIndex;
    [SerializeField] private Transform plunger;
    [SerializeField] private Vector3 pressAxis = new Vector3(0f, -1f, 0f);
    [SerializeField] private float pressDepth = 0.08f;
    [SerializeField] private float pressDuration = 0.25f;
    [SerializeField] private float rangeMemory = 0.2f;
    [SerializeField] private GameObject prompt;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool debug;

    private Vector3 _plungerRest;
    private float _rangeTimer;
    private float _pressT = -1f;
    private bool _promptShown;

    void Start()
    {
        if (plunger != null) _plungerRest = plunger.localPosition;
        if (prompt != null) prompt.SetActive(false);
    }

    void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(playerTag)) _rangeTimer = rangeMemory;
    }

    void Update()
    {
        if (_rangeTimer > 0f) _rangeTimer -= Time.deltaTime;

        bool inRange = _rangeTimer > 0f;
        updatePrompt(inRange);

        if (inRange && Input.GetKeyDown(interactKey)) press();

        animatePlunger();
    }

    void press()
    {
        if (puzzle == null) return;

        if (puzzle.IsLocked || puzzle.IsMoving)
        {
            if (debug)
                Debug.Log($"[Button] {name} refused — {(puzzle.IsLocked ? "puzzle locked" : "plates still moving")}.", this);
            return;
        }

        AnimalPlate target = bindToSlot ? puzzle.Occupant(slotIndex) : plate;
        if (target == null) return;

        puzzle.Advance(target);
        _pressT = 0f;
    }

    void animatePlunger()
    {
        if (_pressT < 0f || plunger == null) return;

        _pressT += Time.deltaTime / Mathf.Max(0.01f, pressDuration);

        float depth;

        if (_pressT >= 1f)
        {
            depth = 0f;
            _pressT = -1f;
        }
        else if (_pressT < 0.3f)
        {
            depth = Mathf.SmoothStep(0f, 1f, _pressT / 0.3f);
        }
        else
        {
            depth = Mathf.SmoothStep(1f, 0f, (_pressT - 0.3f) / 0.7f);
        }

        plunger.localPosition = _plungerRest + pressAxis.normalized * (depth * pressDepth);
    }

    void updatePrompt(bool inRange)
    {
        if (prompt == null) return;

        bool show = inRange && puzzle != null && !puzzle.IsLocked;
        if (show == _promptShown) return;

        _promptShown = show;
        prompt.SetActive(show);
    }

    void OnDrawGizmosSelected()
    {
        AnimalPlate target = bindToSlot ? null : plate;
        if (target == null) return;

        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.9f);
        Gizmos.DrawLine(transform.position + Vector3.up, target.transform.position);
    }

    public bool PlayerInRange => _rangeTimer > 0f;
}
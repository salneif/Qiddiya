using UnityEngine;

public class PedestalButton : MonoBehaviour
{
    [SerializeField] private StatuePedestal pedestal;
    [SerializeField] private Transform buttonTop;
    [SerializeField] private GameObject prompt;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float rangeMemory = 0.2f;
    [SerializeField] private float pressDepth = 0.05f;
    [SerializeField] private float pressDuration = 0.14f;

    private Vector3 _topRestPos;
    private float _rangeTimer;
    private float _pressTimer;
    private bool _playerInRange;

    void Awake()
    {
        if (buttonTop != null) _topRestPos = buttonTop.localPosition;
    }

    void Start()
    {
        if (prompt != null) prompt.SetActive(false);
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        _rangeTimer = rangeMemory;
    }

    void Update()
    {
        _rangeTimer -= Time.deltaTime;
        _playerInRange = _rangeTimer > 0f;

        bool showPrompt = _playerInRange && pedestal != null && !pedestal.IsLocked;
        if (prompt != null && prompt.activeSelf != showPrompt)
            prompt.SetActive(showPrompt);

        if (_playerInRange && pedestal != null && pedestal.CanInteract && Input.GetKeyDown(interactKey))
        {
            if (pedestal.Cycle()) _pressTimer = pressDuration;
        }

        pressVisual();
    }

    void pressVisual()
    {
        if (buttonTop == null) return;

        if (_pressTimer <= 0f)
        {
            buttonTop.localPosition = _topRestPos;
            return;
        }

        _pressTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(_pressTimer / Mathf.Max(0.0001f, pressDuration));
        buttonTop.localPosition = _topRestPos + Vector3.down * (Mathf.Sin(k * Mathf.PI) * pressDepth);
    }

    public bool PlayerInRange => _playerInRange;
}
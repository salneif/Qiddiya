using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class CurtainOpener : MonoBehaviour
{
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;
    [SerializeField] private Vector3 leftOpenOffset = new Vector3(-1.5f, 0f, 0f);
    [SerializeField] private Vector3 rightOpenOffset = new Vector3(1.5f, 0f, 0f);
    [SerializeField] private float openDuration = 1.8f;
    [SerializeField] private float closeDuration = 1.2f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool startOpen;

    public UnityEvent OnOpened;
    public UnityEvent OnClosed;

    private Vector3 _leftClosedPos;
    private Vector3 _rightClosedPos;
    private Coroutine _move;
    private float _progress;
    private bool _isOpen;

    void Awake()
    {
        if (leftPanel != null) _leftClosedPos = leftPanel.localPosition;
        if (rightPanel != null) _rightClosedPos = rightPanel.localPosition;

        _isOpen = startOpen;
        applyProgress(_isOpen ? 1f : 0f);
    }

    void OnDisable()
    {
        if (_move == null) return;

        StopCoroutine(_move);
        _move = null;
        applyProgress(_isOpen ? 1f : 0f);
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        startMove(1f, openDuration);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        startMove(0f, closeDuration);
    }

    void startMove(float target, float duration)
    {
        if (_move != null)
        {
            StopCoroutine(_move);
            _move = null;
        }

        if (duration <= 0f || !gameObject.activeInHierarchy)
        {
            applyProgress(target);
            fireEvent(target);
            return;
        }

        _move = StartCoroutine(moveRoutine(target, duration));
    }

    IEnumerator moveRoutine(float target, float duration)
    {
        float from = _progress;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = moveCurve.Evaluate(Mathf.Clamp01(t / duration));
            applyProgress(Mathf.LerpUnclamped(from, target, k));
            yield return null;
        }

        applyProgress(target);
        _move = null;
        fireEvent(target);
    }

    void applyProgress(float p)
    {
        _progress = p;

        if (leftPanel != null) leftPanel.localPosition = _leftClosedPos + leftOpenOffset * p;
        if (rightPanel != null) rightPanel.localPosition = _rightClosedPos + rightOpenOffset * p;
    }

    void fireEvent(float target)
    {
        if (target >= 0.999f) OnOpened?.Invoke();
        else if (target <= 0.001f) OnClosed?.Invoke();
    }

    [ContextMenu("Open")]
    void openFromMenu()
    {
        if (!Application.isPlaying) return;
        Open();
    }

    [ContextMenu("Close")]
    void closeFromMenu()
    {
        if (!Application.isPlaying) return;
        Close();
    }

    public bool IsOpen => _isOpen;
    public float Progress => _progress;
}
using UnityEngine;

public class ClockFace : MonoBehaviour
{
    [SerializeField] private Transform hourPivot;
    [SerializeField] private Transform minutePivot;
    [SerializeField] private float animSpeed = 5f;

    private float _targetMinutes;
    private float _currentMinutes;

    void Update()
    {
        _currentMinutes = Mathf.Lerp(_currentMinutes, _targetMinutes, animSpeed * Time.deltaTime);
        applyHands();
    }

    public void SetMinutes(float totalMinutes)
    {
        _targetMinutes = totalMinutes;
    }

    void applyHands()
    {
        float minuteAngle = (_currentMinutes / 60f) * 360f;
        if (minutePivot != null)
            minutePivot.localRotation = Quaternion.Euler(0f, 0f, -minuteAngle);

        float hourAngle = (_currentMinutes / 720f) * 360f;
        if (hourPivot != null)
            hourPivot.localRotation = Quaternion.Euler(0f, 0f, -hourAngle);
    }

    public float CurrentMinutes => _currentMinutes;
}
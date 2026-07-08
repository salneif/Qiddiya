using UnityEngine;

public class LavaFlow : MonoBehaviour
{
    enum Phase { Idle, Flowing, Holding, Reversing, Waiting, Restoring }

    [SerializeField] private float startY = 6.9338f;
    [SerializeField] private float endY = 5.67822f;
    [SerializeField] private float startZ = 1.5425f;
    [SerializeField] private float endZ = 0.92509f;
    [SerializeField] private float startScaleZ = 0.01319f;
    [SerializeField] private float endScaleZ = 0.114714f;
    [SerializeField] private float flowSpeed = 0.2f;
    [SerializeField] private float holdDuration = 5f;
    [SerializeField] private float waitDuration = 10f;
    [SerializeField] private Transform sphere;
    private static readonly Vector3 SphereMaxScale = new Vector3(1.2f, 1.2f, 0f);

    private Phase _phase = Phase.Idle;
    private float _progress;
    private float _timer;

    void Start()
    {
        StartFlow();
    }

    void Update()
    {
        switch (_phase)
        {
            case Phase.Flowing:
                advance(startY, endY, startZ, endZ, startScaleZ, endScaleZ, Phase.Holding);
                break;

            case Phase.Holding:
                _timer -= Time.deltaTime;
                if (sphere != null)
                {
                    float t = 1f - (_timer / holdDuration);
                    sphere.localScale = Vector3.Lerp(Vector3.zero, SphereMaxScale, t);
                }
                if (_timer <= 0f)
                {
                    _progress = 0f;
                    _phase = Phase.Reversing;
                }
                break;

            case Phase.Reversing:
                advance(endY, startY, endZ, startZ, endScaleZ, 0f, Phase.Waiting);
                break;

            case Phase.Waiting:
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    _progress = 0f;
                    _phase = Phase.Restoring;
                }
                break;

            case Phase.Restoring:
                advance(startY, startY, startZ, startZ, 0f, startScaleZ, Phase.Flowing);
                break;
        }
    }

    void advance(float fromY, float toY, float fromZ, float toZ, float fromSZ, float toSZ, Phase next)
    {
        _progress += flowSpeed * Time.deltaTime;
        if (_progress >= 1f)
        {
            _progress = 0f;
            applyLerp(fromY, toY, fromZ, toZ, fromSZ, toSZ, 1f);

            if (next == Phase.Holding) _timer = holdDuration;
            if (next == Phase.Waiting) _timer = waitDuration;

            _phase = next;
            return;
        }

        applyLerp(fromY, toY, fromZ, toZ, fromSZ, toSZ, _progress);
    }

    void applyLerp(float fromY, float toY, float fromZ, float toZ, float fromSZ, float toSZ, float t)
    {
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Lerp(fromY, toY, t);
        pos.z = Mathf.Lerp(fromZ, toZ, t);
        transform.localPosition = pos;

        Vector3 scl = transform.localScale;
        scl.z = Mathf.Lerp(fromSZ, toSZ, t);
        transform.localScale = scl;
    }

    public void StartFlow()
    {
        _progress = 0f;
        _phase = Phase.Flowing;
        if (sphere != null) sphere.localScale = Vector3.zero;
    }

    public void ResetFlow()
    {
        _phase = Phase.Idle;
        _progress = 0f;
        applyLerp(startY, startY, startZ, startZ, startScaleZ, startScaleZ, 0f);
        if (sphere != null) sphere.localScale = Vector3.zero;
    }

    public bool IsComplete => _phase == Phase.Idle && _progress == 0f;
    public bool IsFlowing => _phase != Phase.Idle;
    public float Progress => _progress;
}
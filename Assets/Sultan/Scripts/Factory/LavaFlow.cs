using UnityEngine;

public class LavaFlow : MonoBehaviour
{
    [SerializeField] private float flowSpeed = 1f;
    [SerializeField] private float startY = 6.9338f;
    [SerializeField] private float endY = 5.67822f;
    [SerializeField] private float startZ = 1.5425f;
    [SerializeField] private float endZ = 0.92509f;
    [SerializeField] private float startScaleZ = 0.01319f;
    [SerializeField] private float endScaleZ = 0.114714f;

    private float _progress;
    private bool _flowing;
    private bool _complete;

    void Start()
    {
        applyState(0f);
    }

    void Update()
    {
        if (!_flowing || _complete) return;

        _progress += flowSpeed * Time.deltaTime;
        if (_progress >= 1f)
        {
            _progress = 1f;
            _complete = true;
            _flowing = false;
        }

        applyState(_progress);
    }

    void applyState(float t)
    {
        Vector3 pos = transform.localPosition;
        pos.y = Mathf.Lerp(startY, endY, t);
        pos.z = Mathf.Lerp(startZ, endZ, t);
        transform.localPosition = pos;

        Vector3 scl = transform.localScale;
        scl.z = Mathf.Lerp(startScaleZ, endScaleZ, t);
        transform.localScale = scl;
    }

    public void StartFlow()
    {
        _progress = 0f;
        _complete = false;
        _flowing = true;
    }

    public void ResetFlow()
    {
        _flowing = false;
        _complete = false;
        _progress = 0f;
        applyState(0f);
    }

    public bool IsComplete => _complete;
    public bool IsFlowing => _flowing;
    public float Progress => _progress;
}
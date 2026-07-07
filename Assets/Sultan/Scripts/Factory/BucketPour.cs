using UnityEngine;

public class BucketPour : MonoBehaviour
{
    enum Phase { Idle, Rotating, Waiting, Pouring, Holding, Retracting, Returning, Draining }

    [SerializeField] private Transform bucket;
    [SerializeField] private GameObject lavaFlow;
    [SerializeField] private Transform sphere;
    [SerializeField] private GameObject secondSphere;
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float postRotationWait = 0.5f;
    [SerializeField] private float startY = 5.2813f;
    [SerializeField] private float endY = 2.99f;
    [SerializeField] private float startZ = 0.2076f;
    [SerializeField] private float endZ = -0.52f;
    [SerializeField] private float startScaleZ = -0.00036f;
    [SerializeField] private float endScaleZ = 0.174062f;
    [SerializeField] private float flowSpeed = 0.2f;
    [SerializeField] private float holdDuration = 5f;
    [SerializeField] private float sphereTargetScaleX = 0.4f;
    [SerializeField] private float sphereTargetScaleY = 0.1f;
    [SerializeField] private float sphereYDrop = 0.4f;
    [SerializeField] private Vector3 secondSphereTargetScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float secondSphereDrainDuration = 4f;

    private Phase _phase = Phase.Idle;
    private float _progress;
    private float _timer;
    private Quaternion _rotInitial;
    private Quaternion _rotTilted;
    private float _sphereStartScaleX;
    private float _sphereStartScaleY;
    private float _sphereStartY;
    private Vector3 _secondSphereStartScale;

    void Start()
    {
        _rotInitial = bucket.localRotation;
        StartPour();
    }

    public void StartPour()
    {
        _rotTilted = _rotInitial * Quaternion.Euler(rotationAngle, 0f, 0f);
        _progress = 0f;
        _phase = Phase.Rotating;
    }

    void Update()
    {
        switch (_phase)
        {
            case Phase.Rotating:
                _progress += Time.deltaTime / rotationDuration;
                if (_progress >= 1f)
                {
                    bucket.localRotation = _rotTilted;
                    _timer = postRotationWait;
                    _progress = 0f;
                    _phase = Phase.Waiting;
                    return;
                }
                bucket.localRotation = Quaternion.Lerp(_rotInitial, _rotTilted, _progress);
                break;

            case Phase.Waiting:
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    beginPour();
                    _phase = Phase.Pouring;
                }
                break;

            case Phase.Pouring:
                _progress += flowSpeed * Time.deltaTime;
                if (_progress >= 1f) _progress = 1f;
                applyPour(_progress);
                if (_progress >= 1f)
                {
                    _timer = holdDuration;
                    _progress = 0f;
                    if (secondSphere != null)
                    {
                        secondSphere.SetActive(true);
                        _secondSphereStartScale = secondSphere.transform.localScale;
                    }
                    if (sphere != null)
                    {
                        _sphereStartScaleX = sphere.localScale.x;
                        _sphereStartScaleY = sphere.localScale.y;
                        _sphereStartY = sphere.localPosition.y;
                    }
                    _phase = Phase.Holding;
                }
                break;

            case Phase.Holding:
                _timer -= Time.deltaTime;
                float ht = 1f - (_timer / holdDuration);
                if (secondSphere != null)
                {
                    secondSphere.transform.localScale = Vector3.Lerp(
                        _secondSphereStartScale, secondSphereTargetScale, ht);
                }
                if (sphere != null)
                {
                    Vector3 scl = sphere.localScale;
                    scl.x = Mathf.Lerp(_sphereStartScaleX, sphereTargetScaleX, ht);
                    scl.y = Mathf.Lerp(_sphereStartScaleY, sphereTargetScaleY, ht);
                    sphere.localScale = scl;

                    Vector3 pos = sphere.localPosition;
                    pos.y = Mathf.Lerp(_sphereStartY, _sphereStartY - sphereYDrop, ht);
                    sphere.localPosition = pos;
                }
                if (_timer <= 0f)
                {
                    if (sphere != null) sphere.gameObject.SetActive(false);
                    _progress = 0f;
                    _phase = Phase.Retracting;
                }
                break;

            case Phase.Retracting:
                _progress += flowSpeed * Time.deltaTime;
                if (_progress >= 1f) _progress = 1f;
                applyRetract(_progress);
                if (_progress >= 1f)
                {
                    if (lavaFlow != null) lavaFlow.SetActive(false);
                    _progress = 0f;
                    _phase = Phase.Returning;
                }
                break;

            case Phase.Returning:
                _progress += Time.deltaTime / rotationDuration;
                if (_progress >= 1f)
                {
                    bucket.localRotation = _rotInitial;
                    _timer = secondSphereDrainDuration;
                    _progress = 0f;
                    _phase = Phase.Draining;
                    return;
                }
                bucket.localRotation = Quaternion.Lerp(_rotTilted, _rotInitial, _progress);
                break;

            case Phase.Draining:
                _timer -= Time.deltaTime;
                if (secondSphere != null)
                {
                    float dt = 1f - (_timer / secondSphereDrainDuration);
                    secondSphere.transform.localScale = Vector3.Lerp(
                        secondSphereTargetScale, _secondSphereStartScale, dt);
                }
                if (_timer <= 0f)
                {
                    if (secondSphere != null) secondSphere.SetActive(false);
                    _phase = Phase.Idle;
                }
                break;
        }
    }

    void beginPour()
    {
        _progress = 0f;

        if (lavaFlow != null)
        {
            lavaFlow.SetActive(true);
            applyPour(0f);
        }

        if (sphere != null)
        {
            _sphereStartScaleX = sphere.localScale.x;
            _sphereStartScaleY = sphere.localScale.y;
            _sphereStartY = sphere.localPosition.y;
        }
    }

    void applyPour(float t)
    {
        if (lavaFlow == null) return;

        Transform lf = lavaFlow.transform;
        Vector3 pos = lf.localPosition;
        pos.y = Mathf.Lerp(startY, endY, t);
        pos.z = Mathf.Lerp(startZ, endZ, t);
        lf.localPosition = pos;

        Vector3 scl = lf.localScale;
        scl.z = Mathf.Lerp(startScaleZ, endScaleZ, t);
        lf.localScale = scl;
    }

    void applyRetract(float t)
    {
        if (lavaFlow == null) return;

        Transform lf = lavaFlow.transform;
        Vector3 pos = lf.localPosition;
        pos.y = Mathf.Lerp(endY, startY, t);
        pos.z = Mathf.Lerp(endZ, startZ, t);
        lf.localPosition = pos;

        Vector3 scl = lf.localScale;
        scl.z = Mathf.Lerp(endScaleZ, startScaleZ, t);
        lf.localScale = scl;
    }

    void applySphere(float t)
    {
        if (sphere == null) return;

        Vector3 scl = sphere.localScale;
        scl.y = Mathf.Lerp(_sphereStartScaleY, sphereTargetScaleY, t);
        sphere.localScale = scl;

        Vector3 pos = sphere.localPosition;
        pos.y = Mathf.Lerp(_sphereStartY, _sphereStartY - sphereYDrop, t);
        sphere.localPosition = pos;
    }

    public bool IsComplete => _phase == Phase.Idle;
}
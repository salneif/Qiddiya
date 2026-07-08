// using UnityEngine;

// public class LavaCycle : MonoBehaviour
// {
//     enum Phase
//     {
//         Idle, InitialWait,
//         PipeFlowing, PipeHolding, PipeReversing,
//         BucketRotating, BucketWaiting, BucketPouring, BucketHolding,
//         BucketRetracting, BucketReturning,
//         CycleRestoring
//     }

//     [SerializeField] private Transform pipeLava;
//     [SerializeField] private float pipeStartY = 6.9338f;
//     [SerializeField] private float pipeEndY = 5.67822f;
//     [SerializeField] private float pipeStartZ = 1.5425f;
//     [SerializeField] private float pipeEndZ = 0.92509f;
//     [SerializeField] private float pipeStartScaleZ = 0.01319f;
//     [SerializeField] private float pipeEndScaleZ = 0.114714f;
//     [SerializeField] private Transform bucket;
//     [SerializeField] private float rotationAngle = 90f;
//     [SerializeField] private float rotationDuration = 2f;
//     [SerializeField] private float postRotationWait = 0.5f;
//     [SerializeField] private GameObject bucketLava;
//     [SerializeField] private float bucketStartScaleZ = -0.01698f;
//     [SerializeField] private float bucketEndScaleZ = -0.50115f;
//     [SerializeField] private Transform sphere;
//     [SerializeField] private Vector3 sphereMaxScale = new Vector3(1.2f, 1.2f, 0f);
//     [SerializeField] private float sphereTargetScaleX = 0.4f;
//     [SerializeField] private float sphereTargetScaleY = 0.1f;
//     [SerializeField] private float sphereYDrop = 0.4f;
//     [SerializeField] private GameObject secondSphere;
//     [SerializeField] private Vector3 secondSphereTargetScale = new Vector3(3f, 3f, 2.75f);
//     [SerializeField] private float secondSphereDrainDuration = 4f;
//     [SerializeField] private GameObject lavaEffect;
//     [SerializeField] private GameObject lavaTrigger;
//     [SerializeField] private float lavaHazardDelay = 1f;
//     [SerializeField] private GameObject secondSphereEffect;
//     [SerializeField] private float secondSphereEffectDelay = 2f;
//     [SerializeField] private float initialDelay = 10f;
//     [SerializeField] private float flowSpeed = 0.2f;
//     [SerializeField] private float pipeHoldDuration = 5f;
//     [SerializeField] private float bucketHoldDuration = 5f;

//     private Phase _phase = Phase.Idle;
//     private float _phaseT;
//     private float _phaseTimer;
//     private Quaternion _rotInitial;
//     private Quaternion _rotTilted;
//     private float _sphereInitialY;
//     private float _sphereStartScaleX;
//     private float _sphereStartScaleY;
//     private float _sphereStartY;
//     private Vector3 _secondSphereStartScale;
//     private bool _draining;
//     private float _drainTimer;
//     private bool _hazardPending;
//     private float _hazardTimer;
//     private bool _secondEffectPending;
//     private float _secondEffectTimer;

//     void Start()
//     {
//         _rotInitial = bucket.localRotation;
//         if (sphere != null)
//             _sphereInitialY = sphere.localPosition.y;

//         if (initialDelay > 0f)
//         {
//             _phaseTimer = initialDelay;
//             _phase = Phase.InitialWait;
//         }
//         else
//         {
//             beginCycle();
//         }
//     }

//     void beginCycle()
//     {
//         _phaseT = 0f;
//         _phaseTimer = 0f;
//         _phase = Phase.PipeFlowing;

//         if (sphere != null)
//         {
//             sphere.gameObject.SetActive(true);
//             sphere.localScale = Vector3.zero;
//             Vector3 pos = sphere.localPosition;
//             pos.y = _sphereInitialY;
//             sphere.localPosition = pos;
//         }

//         if (lavaEffect != null) lavaEffect.SetActive(false);
//         if (lavaTrigger != null) lavaTrigger.SetActive(false);
//         _hazardPending = true;
//         _hazardTimer = lavaHazardDelay;
//     }

//     void enterPhase(Phase next)
//     {
//         _phaseT = 0f;
//         _phaseTimer = 0f;
//         _phase = next;
//     }

//     void enterTimedPhase(Phase next, float duration)
//     {
//         _phaseT = 0f;
//         _phaseTimer = duration;
//         _phase = next;
//     }

//     void Update()
//     {
//         updateDrain();
//         updateHazardDelay();
//         updateSecondEffectDelay();
//         updateMainPhase();
//     }

//     void updateDrain()
//     {
//         if (!_draining) return;

//         _drainTimer -= Time.deltaTime;
//         if (secondSphere != null)
//         {
//             float t = Mathf.Clamp01(1f - (_drainTimer / secondSphereDrainDuration));
//             secondSphere.transform.localScale = Vector3.Lerp(
//                 secondSphereTargetScale, _secondSphereStartScale, t);
//         }
//         if (_drainTimer <= 0f)
//         {
//             if (secondSphere != null) secondSphere.SetActive(false);
//             if (secondSphereEffect != null) secondSphereEffect.SetActive(false);
//             _secondEffectPending = false;
//             _draining = false;
//         }
//     }

//     void updateHazardDelay()
//     {
//         if (!_hazardPending) return;

//         _hazardTimer -= Time.deltaTime;
//         if (_hazardTimer <= 0f)
//         {
//             if (lavaEffect != null) lavaEffect.SetActive(true);
//             if (lavaTrigger != null) lavaTrigger.SetActive(true);
//             _hazardPending = false;
//         }
//     }

//     void updateSecondEffectDelay()
//     {
//         if (!_secondEffectPending) return;

//         _secondEffectTimer -= Time.deltaTime;
//         if (_secondEffectTimer <= 0f)
//         {
//             if (secondSphereEffect != null) secondSphereEffect.SetActive(true);
//             _secondEffectPending = false;
//         }
//     }


//     void updateMainPhase()
//     {
//         switch (_phase)
//         {

//             case Phase.InitialWait:
//             {
//                 _phaseTimer -= Time.deltaTime;
//                 if (_phaseTimer <= 0f)
//                     beginCycle();
//                 break;
//             }

//             case Phase.PipeFlowing:
//             {
//                 _phaseT += flowSpeed * Time.deltaTime;
//                 if (_phaseT >= 1f)
//                 {
//                     applyPipeLerp(pipeStartY, pipeEndY, pipeStartZ, pipeEndZ, pipeStartScaleZ, pipeEndScaleZ, 1f);
//                     enterTimedPhase(Phase.PipeHolding, pipeHoldDuration);
//                     return;
//                 }
//                 applyPipeLerp(pipeStartY, pipeEndY, pipeStartZ, pipeEndZ, pipeStartScaleZ, pipeEndScaleZ, _phaseT);
//                 break;
//             }

//             case Phase.PipeHolding:
//             {
//                 _phaseTimer -= Time.deltaTime;
//                 if (sphere != null)
//                 {
//                     float t = Mathf.Clamp01(1f - (_phaseTimer / pipeHoldDuration));
//                     sphere.localScale = Vector3.Lerp(Vector3.zero, sphereMaxScale, t);
//                 }
//                 if (_phaseTimer <= 0f)
//                     enterPhase(Phase.PipeReversing);
//                 break;
//             }

//             case Phase.PipeReversing:
//             {
//                 _phaseT += flowSpeed * Time.deltaTime;
//                 if (_phaseT >= 1f)
//                 {
//                     applyPipeLerp(pipeEndY, pipeStartY, pipeEndZ, pipeStartZ, pipeEndScaleZ, 0f, 1f);
//                     _rotTilted = _rotInitial * Quaternion.Euler(rotationAngle, 0f, 0f);
//                     if (lavaEffect != null) lavaEffect.SetActive(false);
//                     if (lavaTrigger != null) lavaTrigger.SetActive(false);
//                     _hazardPending = false;
//                     enterPhase(Phase.BucketRotating);
//                     return;
//                 }
//                 applyPipeLerp(pipeEndY, pipeStartY, pipeEndZ, pipeStartZ, pipeEndScaleZ, 0f, _phaseT);
//                 break;
//             }

//             case Phase.BucketRotating:
//             {
//                 _phaseT += Time.deltaTime / rotationDuration;
//                 if (_phaseT >= 1f)
//                 {
//                     bucket.localRotation = _rotTilted;
//                     enterTimedPhase(Phase.BucketWaiting, postRotationWait);
//                     return;
//                 }
//                 bucket.localRotation = Quaternion.Lerp(_rotInitial, _rotTilted, _phaseT);
//                 break;
//             }

//             case Phase.BucketWaiting:
//             {
//                 _phaseTimer -= Time.deltaTime;
//                 if (_phaseTimer <= 0f)
//                 {
//                     if (bucketLava != null)
//                     {
//                         bucketLava.SetActive(true);
//                         applyBucketLerp(0f);
//                     }
//                     enterPhase(Phase.BucketPouring);
//                 }
//                 break;
//             }

//             case Phase.BucketPouring:
//             {
//                 _phaseT += flowSpeed * Time.deltaTime;
//                 if (_phaseT >= 1f) _phaseT = 1f;
//                 applyBucketLerp(_phaseT);
//                 if (_phaseT >= 1f)
//                 {
//                     if (secondSphere != null)
//                     {
//                         secondSphere.SetActive(true);
//                         _secondSphereStartScale = secondSphere.transform.localScale;
//                     }
//                     if (sphere != null)
//                     {
//                         _sphereStartScaleX = sphere.localScale.x;
//                         _sphereStartScaleY = sphere.localScale.y;
//                         _sphereStartY = sphere.localPosition.y;
//                     }
//                     enterTimedPhase(Phase.BucketHolding, bucketHoldDuration);
//                     _secondEffectPending = true;
//                     _secondEffectTimer = secondSphereEffectDelay;
//                 }
//                 break;
//             }

//             case Phase.BucketHolding:
//             {
//                 _phaseTimer -= Time.deltaTime;
//                 float ht = Mathf.Clamp01(1f - (_phaseTimer / bucketHoldDuration));
//                 if (secondSphere != null)
//                 {
//                     secondSphere.transform.localScale = Vector3.Lerp(
//                         _secondSphereStartScale, secondSphereTargetScale, ht);
//                 }
//                 if (sphere != null)
//                 {
//                     Vector3 scl = sphere.localScale;
//                     scl.x = Mathf.Lerp(_sphereStartScaleX, sphereTargetScaleX, ht);
//                     scl.y = Mathf.Lerp(_sphereStartScaleY, sphereTargetScaleY, ht);
//                     sphere.localScale = scl;

//                     Vector3 pos = sphere.localPosition;
//                     pos.y = Mathf.Lerp(_sphereStartY, _sphereStartY - sphereYDrop, ht);
//                     sphere.localPosition = pos;
//                 }
//                 if (_phaseTimer <= 0f)
//                 {
//                     if (sphere != null) sphere.gameObject.SetActive(false);
//                     enterPhase(Phase.BucketRetracting);
//                 }
//                 break;
//             }

//             case Phase.BucketRetracting:
//             {
//                 _phaseT += flowSpeed * Time.deltaTime;
//                 if (_phaseT >= 1f) _phaseT = 1f;
//                 applyBucketRetract(_phaseT);
//                 if (_phaseT >= 1f)
//                 {
//                     if (bucketLava != null) bucketLava.SetActive(false);
//                     enterPhase(Phase.BucketReturning);
//                 }
//                 break;
//             }

//             case Phase.BucketReturning:
//             {
//                 _phaseT += Time.deltaTime / rotationDuration;
//                 if (_phaseT >= 1f)
//                 {
//                     bucket.localRotation = _rotInitial;
//                     _draining = true;
//                     _drainTimer = secondSphereDrainDuration;
//                     enterPhase(Phase.CycleRestoring);
//                     return;
//                 }
//                 bucket.localRotation = Quaternion.Lerp(_rotTilted, _rotInitial, _phaseT);
//                 break;
//             }

//             case Phase.CycleRestoring:
//             {
//                 _phaseT += flowSpeed * Time.deltaTime;
//                 if (_phaseT >= 1f)
//                 {
//                     applyPipeLerp(pipeStartY, pipeStartY, pipeStartZ, pipeStartZ, 0f, pipeStartScaleZ, 1f);
//                     beginCycle();
//                     return;
//                 }
//                 applyPipeLerp(pipeStartY, pipeStartY, pipeStartZ, pipeStartZ, 0f, pipeStartScaleZ, _phaseT);
//                 break;
//             }
//         }
//     }

//     void applyPipeLerp(float fromY, float toY, float fromZ, float toZ, float fromSZ, float toSZ, float t)
//     {
//         if (pipeLava == null) return;

//         Vector3 pos = pipeLava.localPosition;
//         pos.y = Mathf.Lerp(fromY, toY, t);
//         pos.z = Mathf.Lerp(fromZ, toZ, t);
//         pipeLava.localPosition = pos;

//         Vector3 scl = pipeLava.localScale;
//         scl.z = Mathf.Lerp(fromSZ, toSZ, t);
//         pipeLava.localScale = scl;
//     }

//     void applyBucketLerp(float t)
//     {
//         if (bucketLava == null) return;

//         Transform lf = bucketLava.transform;
//         Vector3 scl = lf.localScale;
//         scl.z = Mathf.Lerp(bucketStartScaleZ, bucketEndScaleZ, t);
//         lf.localScale = scl;
//     }

//     void applyBucketRetract(float t)
//     {
//         if (bucketLava == null) return;

//         Transform lf = bucketLava.transform;
//         Vector3 scl = lf.localScale;
//         scl.z = Mathf.Lerp(bucketEndScaleZ, bucketStartScaleZ, t);
//         lf.localScale = scl;
//     }
// }

using UnityEngine;

public class LavaCycle : MonoBehaviour
{
    enum Phase
    {
        Idle, InitialWait,
        PipeFlowing, PipeHolding, PipeReversing,
        BucketRotating, BucketWaiting, BucketPouring, BucketHolding,
        BucketRetracting, BucketReturning,
        CycleRestoring
    }

    [SerializeField] private Transform pipeLava;
    [SerializeField] private float pipeStartY = 6.9338f;
    [SerializeField] private float pipeEndY = 5.67822f;
    [SerializeField] private float pipeStartZ = 1.5425f;
    [SerializeField] private float pipeEndZ = 0.92509f;
    [SerializeField] private float pipeStartScaleZ = 0.01319f;
    [SerializeField] private float pipeEndScaleZ = 0.114714f;
    [SerializeField] private Transform bucket;
    [SerializeField] private float rotationAngle = 90f;
    [SerializeField] private float rotationDuration = 2f;
    [SerializeField] private float postRotationWait = 0.5f;
    [SerializeField] private GameObject bucketLava;
    [SerializeField] private float bucketStartScaleY = 3f;
    [SerializeField] private float bucketEndScaleY = 0.5f;
    [SerializeField] private float bucketStartScaleZ = -0.01698f;
    [SerializeField] private float bucketEndScaleZ = -0.50115f;
    [SerializeField] private Transform sphere;
    [SerializeField] private Vector3 sphereMaxScale = new Vector3(1.2f, 1.2f, 0f);
    [SerializeField] private float sphereTargetScaleX = 0.4f;
    [SerializeField] private float sphereTargetScaleY = 0.1f;
    [SerializeField] private float sphereYDrop = 0.4f;
    [SerializeField] private GameObject secondSphere;
    [SerializeField] private Vector3 secondSphereTargetScale = new Vector3(3f, 3f, 2.75f);
    [SerializeField] private float secondSphereDrainDuration = 4f;
    [SerializeField] private GameObject lavaEffect;
    [SerializeField] private GameObject lavaTrigger;
    [SerializeField] private float lavaHazardDelay = 1f;
    [SerializeField] private GameObject secondSphereEffect;
    [SerializeField] private float secondSphereEffectDelay = 2f;
    [SerializeField] private float initialDelay = 10f;
    [SerializeField] private float flowSpeed = 0.2f;
    [SerializeField] private float pipeHoldDuration = 5f;
    [SerializeField] private float bucketHoldDuration = 5f;

    private Phase _phase = Phase.Idle;
    private float _phaseT;
    private float _phaseTimer;
    private Quaternion _rotInitial;
    private Quaternion _rotTilted;
    private float _sphereInitialY;
    private float _sphereStartScaleX;
    private float _sphereStartScaleY;
    private float _sphereStartY;
    private Vector3 _secondSphereStartScale;
    private bool _draining;
    private float _drainTimer;
    private bool _hazardPending;
    private float _hazardTimer;
    private bool _secondEffectPending;
    private float _secondEffectTimer;

    void Start()
    {
        _rotInitial = bucket.localRotation;
        if (sphere != null)
            _sphereInitialY = sphere.localPosition.y;

        if (initialDelay > 0f)
        {
            _phaseTimer = initialDelay;
            _phase = Phase.InitialWait;
        }
        else
        {
            beginCycle();
        }
    }

    void beginCycle()
    {
        _phaseT = 0f;
        _phaseTimer = 0f;
        _phase = Phase.PipeFlowing;

        if (sphere != null)
        {
            sphere.gameObject.SetActive(true);
            sphere.localScale = Vector3.zero;
            Vector3 pos = sphere.localPosition;
            pos.y = _sphereInitialY;
            sphere.localPosition = pos;
        }

        if (lavaEffect != null) lavaEffect.SetActive(false);
        if (lavaTrigger != null) lavaTrigger.SetActive(false);
        _hazardPending = true;
        _hazardTimer = lavaHazardDelay;
    }

    void enterPhase(Phase next)
    {
        _phaseT = 0f;
        _phaseTimer = 0f;
        _phase = next;
    }

    void enterTimedPhase(Phase next, float duration)
    {
        _phaseT = 0f;
        _phaseTimer = duration;
        _phase = next;
    }

    void Update()
    {
        updateDrain();
        updateHazardDelay();
        updateSecondEffectDelay();
        updateMainPhase();
    }

    void updateDrain()
    {
        if (!_draining) return;

        _drainTimer -= Time.deltaTime;
        if (secondSphere != null)
        {
            float t = Mathf.Clamp01(1f - (_drainTimer / secondSphereDrainDuration));
            secondSphere.transform.localScale = Vector3.Lerp(
                secondSphereTargetScale, _secondSphereStartScale, t);
        }
        if (_drainTimer <= 0f)
        {
            if (secondSphere != null) secondSphere.SetActive(false);
            if (secondSphereEffect != null) secondSphereEffect.SetActive(false);
            _secondEffectPending = false;
            _draining = false;
        }
    }

    void updateHazardDelay()
    {
        if (!_hazardPending) return;

        _hazardTimer -= Time.deltaTime;
        if (_hazardTimer <= 0f)
        {
            if (lavaEffect != null) lavaEffect.SetActive(true);
            if (lavaTrigger != null) lavaTrigger.SetActive(true);
            _hazardPending = false;
        }
    }

    void updateSecondEffectDelay()
    {
        if (!_secondEffectPending) return;

        _secondEffectTimer -= Time.deltaTime;
        if (_secondEffectTimer <= 0f)
        {
            if (secondSphereEffect != null) secondSphereEffect.SetActive(true);
            _secondEffectPending = false;
        }
    }


    void updateMainPhase()
    {
        switch (_phase)
        {

            case Phase.InitialWait:
            {
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f)
                    beginCycle();
                break;
            }

            case Phase.PipeFlowing:
            {
                _phaseT += flowSpeed * Time.deltaTime;
                if (_phaseT >= 1f)
                {
                    applyPipeLerp(pipeStartY, pipeEndY, pipeStartZ, pipeEndZ, pipeStartScaleZ, pipeEndScaleZ, 1f);
                    enterTimedPhase(Phase.PipeHolding, pipeHoldDuration);
                    return;
                }
                applyPipeLerp(pipeStartY, pipeEndY, pipeStartZ, pipeEndZ, pipeStartScaleZ, pipeEndScaleZ, _phaseT);
                break;
            }

            case Phase.PipeHolding:
            {
                _phaseTimer -= Time.deltaTime;
                if (sphere != null)
                {
                    float t = Mathf.Clamp01(1f - (_phaseTimer / pipeHoldDuration));
                    sphere.localScale = Vector3.Lerp(Vector3.zero, sphereMaxScale, t);
                }
                if (_phaseTimer <= 0f)
                    enterPhase(Phase.PipeReversing);
                break;
            }

            case Phase.PipeReversing:
            {
                _phaseT += flowSpeed * Time.deltaTime;
                if (_phaseT >= 1f)
                {
                    applyPipeLerp(pipeEndY, pipeStartY, pipeEndZ, pipeStartZ, pipeEndScaleZ, 0f, 1f);
                    _rotTilted = _rotInitial * Quaternion.Euler(rotationAngle, 0f, 0f);
                    if (lavaEffect != null) lavaEffect.SetActive(false);
                    if (lavaTrigger != null) lavaTrigger.SetActive(false);
                    _hazardPending = false;
                    enterPhase(Phase.BucketRotating);
                    return;
                }
                applyPipeLerp(pipeEndY, pipeStartY, pipeEndZ, pipeStartZ, pipeEndScaleZ, 0f, _phaseT);
                break;
            }

            case Phase.BucketRotating:
            {
                _phaseT += Time.deltaTime / rotationDuration;
                if (_phaseT >= 1f)
                {
                    bucket.localRotation = _rotTilted;
                    enterTimedPhase(Phase.BucketWaiting, postRotationWait);
                    return;
                }
                bucket.localRotation = Quaternion.Lerp(_rotInitial, _rotTilted, _phaseT);
                break;
            }

            case Phase.BucketWaiting:
            {
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f)
                {
                    if (bucketLava != null)
                    {
                        bucketLava.SetActive(true);
                        applyBucketLerp(0f);
                    }
                    enterPhase(Phase.BucketPouring);
                }
                break;
            }

            case Phase.BucketPouring:
            {
                _phaseT += flowSpeed * Time.deltaTime;
                if (_phaseT >= 1f) _phaseT = 1f;
                applyBucketLerp(_phaseT);
                if (_phaseT >= 1f)
                {
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
                    enterTimedPhase(Phase.BucketHolding, bucketHoldDuration);
                    _secondEffectPending = true;
                    _secondEffectTimer = secondSphereEffectDelay;
                }
                break;
            }

            case Phase.BucketHolding:
            {
                _phaseTimer -= Time.deltaTime;
                float ht = Mathf.Clamp01(1f - (_phaseTimer / bucketHoldDuration));
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
                if (_phaseTimer <= 0f)
                {
                    if (sphere != null) sphere.gameObject.SetActive(false);
                    enterPhase(Phase.BucketRetracting);
                }
                break;
            }

            case Phase.BucketRetracting:
            {
                if (bucketLava != null) bucketLava.SetActive(false);
                enterPhase(Phase.BucketReturning);
                break;
            }

            case Phase.BucketReturning:
            {
                _phaseT += Time.deltaTime / rotationDuration;
                if (_phaseT >= 1f)
                {
                    bucket.localRotation = _rotInitial;
                    _draining = true;
                    _drainTimer = secondSphereDrainDuration;
                    enterPhase(Phase.CycleRestoring);
                    return;
                }
                bucket.localRotation = Quaternion.Lerp(_rotTilted, _rotInitial, _phaseT);
                break;
            }

            case Phase.CycleRestoring:
            {
                _phaseT += flowSpeed * Time.deltaTime;
                if (_phaseT >= 1f)
                {
                    applyPipeLerp(pipeStartY, pipeStartY, pipeStartZ, pipeStartZ, 0f, pipeStartScaleZ, 1f);
                    beginCycle();
                    return;
                }
                applyPipeLerp(pipeStartY, pipeStartY, pipeStartZ, pipeStartZ, 0f, pipeStartScaleZ, _phaseT);
                break;
            }
        }
    }

    void applyPipeLerp(float fromY, float toY, float fromZ, float toZ, float fromSZ, float toSZ, float t)
    {
        if (pipeLava == null) return;

        Vector3 pos = pipeLava.localPosition;
        pos.y = Mathf.Lerp(fromY, toY, t);
        pos.z = Mathf.Lerp(fromZ, toZ, t);
        pipeLava.localPosition = pos;

        Vector3 scl = pipeLava.localScale;
        scl.z = Mathf.Lerp(fromSZ, toSZ, t);
        pipeLava.localScale = scl;
    }

    void applyBucketLerp(float t)
    {
        if (bucketLava == null) return;

        Transform lf = bucketLava.transform;
        Vector3 scl = lf.localScale;
        scl.y = Mathf.Lerp(bucketStartScaleY, bucketEndScaleY, t);
        scl.z = Mathf.Lerp(bucketStartScaleZ, bucketEndScaleZ, t);
        lf.localScale = scl;
    }

}
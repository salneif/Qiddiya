// using UnityEngine;

// public class Box : MonoBehaviour
// {
//     public enum BoxID { A, B, C, D }
//     public enum BoxState { InStack, Scattered, OnPlate }

//     [SerializeField] private BoxID id;
//     [SerializeField] private float scatterForce = 4f;

//     private Rigidbody rb;
//     private BoxState state = BoxState.InStack;
//     private float settleTimer = 1f;

//     private void Awake()
//     {
//         rb = GetComponent<Rigidbody>();
//     }

//     private void FixedUpdate()
//     {
//         if (settleTimer > 0f) { settleTimer -= Time.fixedDeltaTime; return; }
//         if (state != BoxState.InStack) return;
//         if (rb.linearVelocity.sqrMagnitude < 0.25f) return;

//         state = BoxState.Scattered;
//         float xDir = Random.Range(0.3f, 1f) * (Random.value > 0.5f ? 1f : -1f);
//         Vector3 nudge = new Vector3(xDir, 0.5f, 0f).normalized * scatterForce;
//         rb.AddForce(nudge, ForceMode.Impulse);
//     }

//     public BoxID ID => id;
//     public BoxState State => state;
//     public void SetState(BoxState newState) => state = newState;
// }

using UnityEngine;

public class Box : MonoBehaviour
{
    public enum BoxID { A, B, C, D }
    public enum BoxState { InStack, Scattered, OnPlate }

    [SerializeField] private BoxID id;
    [SerializeField] private float scatterForce = 4f;

    private Rigidbody rb;
    private BoxState state = BoxState.InStack;
    private float settleTimer = 1f;
    private bool _settled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (settleTimer > 0f) { settleTimer -= Time.fixedDeltaTime; return; }

        if (state == BoxState.InStack)
        {
            if (rb.linearVelocity.sqrMagnitude < 0.25f) return;
            state = BoxState.Scattered;
            float xDir = Random.Range(0.3f, 1f) * (Random.value > 0.5f ? 1f : -1f);
            rb.AddForce(new Vector3(xDir, 0.5f, 0f).normalized * scatterForce, ForceMode.Impulse);
            return;
        }

        if (state != BoxState.Scattered) return;

        if (!_settled)
        {
            if (rb.linearVelocity.sqrMagnitude > 0.05f) return;

            Vector3 rayOrigin = transform.position + Vector3.down * 0.36f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 0.3f) &&
                hit.collider.GetComponent<Box>() != null)
            {
                float nudge = Random.value > 0.5f ? 1.5f : -1.5f;
                rb.AddForce(new Vector3(nudge, 0f, 0f), ForceMode.Impulse);
                return;
            }

            _settled = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            transform.rotation = Quaternion.identity;
        }

        if (rb.linearVelocity.y > 2f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 2f;
            rb.linearVelocity = v;
        }
    }

    public BoxID ID => id;
    public BoxState State => state;
    public void SetState(BoxState newState) => state = newState;
}
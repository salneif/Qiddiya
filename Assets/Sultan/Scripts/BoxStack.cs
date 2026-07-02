// using UnityEngine;

// public class BoxStack : MonoBehaviour
// {
//     [SerializeField] private float scatterForceMin = 4f;
//     [SerializeField] private float scatterForceMax = 8f;
//     [SerializeField] private float upwardForceMin = 3f;
//     [SerializeField] private float upwardForceMax = 6f;
//     [SerializeField] private float torqueRange = 200f;

//     private Box[] boxes;
//     private bool scattered;

//     private void Awake()
//     {
//         boxes = GetComponentsInChildren<Box>();
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         if (scattered) return;
//         if (!other.CompareTag("Player")) return;
//         Scatter();
//     }

//     private void Scatter()
//     {
//         scattered = true;
//         Vector3 center = transform.position;

//         foreach (var box in boxes)
//         {
//             float xOffset = box.transform.position.x - center.x;
//             float xSign = xOffset < 0f ? -1f : 1f;
//             float xForce = xSign * Random.Range(scatterForceMin, scatterForceMax);
//             float yForce = Random.Range(upwardForceMin, upwardForceMax);
//             float torque = Random.Range(-torqueRange, torqueRange);

//             box.Scatter(new Vector3(xForce, yForce, 0f), torque);
//         }

//         gameObject.SetActive(false);
//     }

//     private void OnDrawGizmosSelected()
//     {
//         var col = GetComponent<BoxCollider>();
//         if (col == null) return;

//         Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
//         Gizmos.DrawWireCube(transform.position + col.center, col.size);
//     }
// }
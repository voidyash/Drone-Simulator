using System.Collections.Generic;
using UnityEngine;

namespace DroneSimulator.Drones
{
    /// <summary>
    /// Spins the PBR racing drone's propeller meshes in place. Rotation-only:
    /// never touches positions, so it cannot fight the physics flight model
    /// (unlike the asset's DroneAnimator, which bobs the whole transform and
    /// is stripped on import). Diagonal motor pairs counter-rotate like a
    /// real quad. Speed idles on the ground and rises with airspeed.
    /// </summary>
    public sealed class PropSpinner : MonoBehaviour
    {
        [Tooltip("Prop speed on the ground (deg/s).")]
        [Min(0f)] [SerializeField] private float idleSpeed = 1500f;
        [Tooltip("Prop speed at full airspeed (deg/s).")]
        [Min(0f)] [SerializeField] private float fullSpeed = 7000f;

        private Transform[] props = System.Array.Empty<Transform>();
        private float[] directions = System.Array.Empty<float>();
        private FlightController flightController;

        private void Awake()
        {
            flightController = GetComponentInParent<FlightController>();
            CollectProps();
        }

        private void OnEnable()
        {
            if (props.Length == 0)
            {
                CollectProps();
            }
        }

        private void CollectProps()
        {
            var found = new List<Transform>();
            CollectRecursive(transform, found);
            props = found.ToArray();
            directions = new float[props.Length];
            var root = flightController != null ? flightController.transform : transform;
            for (int i = 0; i < props.Length; i++)
            {
                Vector3 local = root.InverseTransformPoint(props[i].position);
                // Diagonal pairs share direction (real quad layout).
                directions[i] = (local.x > 0f) == (local.z > 0f) ? 1f : -1f;
            }
        }

        private static void CollectRecursive(Transform parent, List<Transform> found)
        {
            foreach (Transform child in parent)
            {
                if (child.name == "Prop")
                {
                    found.Add(child);
                }

                CollectRecursive(child, found);
            }
        }

        private void Update()
        {
            if (props.Length == 0)
            {
                return;
            }

            float throttle = 0f;
            if (flightController != null && flightController.MaximumSpeed > 0f)
            {
                throttle = Mathf.Clamp01(flightController.Velocity.magnitude / flightController.MaximumSpeed);
            }

            float speed = Mathf.Lerp(idleSpeed, fullSpeed, throttle) * Time.deltaTime;
            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] != null)
                {
                    props[i].Rotate(0f, 0f, directions[i] * speed, Space.Self);
                }
            }
        }
    }
}

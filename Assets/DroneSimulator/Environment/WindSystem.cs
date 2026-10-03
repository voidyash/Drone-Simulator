using UnityEngine;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Phase 2: physical wind + controlled turbulence.
    /// Wind blows in a compass yaw (0 = +Z/north). Strength 0-100 maps to m/s.
    /// FlightController queries GetDisturbanceAcceleration() each FixedUpdate.
    /// </summary>
    public sealed class WindSystem : MonoBehaviour
    {
        [Header("Wind (0-100)")]
        [Range(0f, 100f)]
        [SerializeField] private float windStrength;

        [Tooltip("Compass yaw in degrees. 0 = +Z (north), 90 = +X (east).")]
        [Range(0f, 360f)]
        [SerializeField] private float windDirectionDegrees;

        [Header("Turbulence (0-100)")]
        [Range(0f, 100f)]
        [SerializeField] private float turbulence;

        [Header("Scaling")]
        [Tooltip("Wind speed in m/s when strength = 100.")]
        [Min(0f)]
        [SerializeField] private float maxWindSpeed = 22f;
        [Tooltip("Turbulence acceleration in m/s^2 when turbulence = 100.")]
        [Min(0f)]
        [SerializeField] private float maxTurbulenceAcceleration = 16f;
        [Tooltip("Turbulence drift in m/s when turbulence = 100. Added to target velocity so gusts visibly carry the drone.")]
        [Min(0f)]
        [SerializeField] private float maxTurbulenceVelocity = 7f;
        [Tooltip("Perlin frequency for gusts. Higher = choppier.")]
        [Min(0.01f)]
        [SerializeField] private float gustFrequency = 1.6f;

        public float WindStrength => windStrength;
        public float WindDirectionDegrees => windDirectionDegrees;
        public float Turbulence => turbulence;
        public Vector3 CurrentWindVector => GetWindVector();

        public Vector3 GetWindDirectionVector()
        {
            return Quaternion.Euler(0f, windDirectionDegrees, 0f) * Vector3.forward;
        }

        public Vector3 GetWindVector()
        {
            return GetWindDirectionVector() * (windStrength / 100f * maxWindSpeed);
        }

        /// <summary>
        /// Controlled random disturbance in m/s^2. Deterministic per-axis Perlin
        /// so it is gusty but bounded and frame-rate independent.
        /// </summary>
        public Vector3 GetTurbulenceAcceleration(float time)
        {
            if (turbulence <= 0.01f)
            {
                return Vector3.zero;
            }

            float scale = turbulence / 100f * maxTurbulenceAcceleration;
            float t = time * gustFrequency;
            float x = (Mathf.PerlinNoise(t, 11.3f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(t + 47.7f, 3.1f) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(83.9f, t + 29.5f) - 0.5f) * 2f;
            return new Vector3(x, y * 0.6f, z) * scale;
        }

        /// <summary>
        /// Turbulence as a velocity drift so it survives the velocity-chase
        /// controller and visibly carries the drone (paired with acceleration punch).
        /// </summary>
        public Vector3 GetTurbulenceVelocity(float time)
        {
            if (turbulence <= 0.01f)
            {
                return Vector3.zero;
            }

            float scale = turbulence / 100f * maxTurbulenceVelocity;
            float t = time * gustFrequency + 500f;
            float x = (Mathf.PerlinNoise(t, 71.7f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(t + 13.3f, 37.9f) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(97.2f, t + 5.5f) - 0.5f) * 2f;
            return new Vector3(x, y * 0.5f, z) * scale;
        }

        public Vector3 GetDisturbanceAcceleration(float time)
        {
            return GetWindVector() + GetTurbulenceAcceleration(time);
        }

        public void Configure(float strength, float directionDegrees, float turbulenceValue)
        {
            SetWindStrength(strength);
            SetWindDirection(directionDegrees);
            SetTurbulence(turbulenceValue);
        }

        public void SetWindStrength(float strength)
        {
            windStrength = Mathf.Clamp(strength, 0f, 100f);
        }

        public void SetWindDirection(float directionDegrees)
        {
            windDirectionDegrees = Mathf.Repeat(directionDegrees, 360f);
        }

        public void SetTurbulence(float turbulenceValue)
        {
            turbulence = Mathf.Clamp(turbulenceValue, 0f, 100f);
        }
    }
}

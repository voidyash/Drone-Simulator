using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Simulated IMU: degradation adds orientation + acceleration noise.
    /// True attitude comes from the drone transform; reported attitude drifts
    /// up to ~12 degrees per axis at degradation 100.
    /// </summary>
    public sealed class IMUSensor : SensorBase
    {
        public override string SensorName => "IMU";

        [Tooltip("Max attitude error in degrees per axis at degradation 100.")]
        [Min(0f)] [SerializeField] private float maxAttitudeErrorDegrees = 12f;
        [Tooltip("Max acceleration noise in m/s^2 at degradation 100.")]
        [Min(0f)] [SerializeField] private float maxAccelerationNoise = 3f;

        private Rigidbody body;
        private Vector3 previousVelocity;
        private bool hasPrevious;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (body != null)
            {
                previousVelocity = body.linearVelocity;
                hasPrevious = true;
            }
        }

        public Vector3 TrueEuler => transform.eulerAngles;

        public Vector3 ReportedEuler
        {
            get
            {
                Vector3 eureka = TrueEuler;
                float k = DegradationFactor;
                if (k <= 0.001f || !IsAvailable)
                {
                    return eureka;
                }

                float t = Time.time * noiseFrequency;
                float ex = SignedNoise(Mathf.PerlinNoise(t, 17.1f)) * maxAttitudeErrorDegrees * k;
                float ey = SignedNoise(Mathf.PerlinNoise(43.3f, t)) * maxAttitudeErrorDegrees * k;
                float ez = SignedNoise(Mathf.PerlinNoise(t + 11f, t)) * maxAttitudeErrorDegrees * k;
                return eureka + new Vector3(ex, ey, ez);
            }
        }

        public float ReportedYaw => ReportedEuler.y;

        public float YawError
        {
            get
            {
                if (!IsAvailable) return -1f;
                float d = Mathf.Abs(ReportedYaw - TrueEuler.y) % 360f;
                return d > 180f ? 360f - d : d;
            }
        }

        public Vector3 ReportedAcceleration
        {
            get
            {
                Vector3 trueAccel = Vector3.zero;
                if (body != null && hasPrevious)
                {
                    trueAccel = (body.linearVelocity - previousVelocity) / Mathf.Max(Time.fixedDeltaTime, 1e-4f);
                }

                float k = DegradationFactor;
                if (k <= 0.001f || !IsAvailable)
                {
                    return trueAccel;
                }

                float t = Time.time * noiseFrequency;
                Vector3 noise = new Vector3(
                    SignedNoise(Mathf.PerlinNoise(t, 5.5f)),
                    SignedNoise(Mathf.PerlinNoise(9.9f, t)) * 0.5f,
                    SignedNoise(Mathf.PerlinNoise(t + 3.3f, 8.8f))) * maxAccelerationNoise * k;
                return trueAccel + noise;
            }
        }
    }
}

using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Simulated GPS: degradation adds horizontal/vertical position error.
    /// At 0 the reported position equals true position; at 100 it drifts up
    /// to ~12 m horizontally with jitter, and reports unavailable.
    /// </summary>
    public sealed class GPSSensor : SensorBase
    {
        public override string SensorName => "GPS";

        [Tooltip("Max horizontal error in meters at degradation 100.")]
        [Min(0f)] [SerializeField] private float maxHorizontalError = 12f;
        [Tooltip("Max vertical error in meters at degradation 100.")]
        [Min(0f)] [SerializeField] private float maxVerticalError = 6f;

        public Vector3 TruePosition => transform.position;

        public Vector3 ReportedPosition
        {
            get
            {
                if (!IsAvailable)
                {
                    return TruePosition + Vector3.up * 1000f;
                }

                float k = DegradationFactor;
                if (k <= 0.001f)
                {
                    return TruePosition;
                }

                float t = Time.time;
                float dx = SignedNoise(Mathf.PerlinNoise(t * 0.25f, 7.3f)) * maxHorizontalError * k;
                float dz = SignedNoise(Mathf.PerlinNoise(31.7f, t * 0.25f)) * maxHorizontalError * k;
                float dy = SignedNoise(Mathf.PerlinNoise(t * noiseFrequency, 3.9f)) * maxVerticalError * k;
                return TruePosition + new Vector3(dx, dy, dz);
            }
        }

        public float PositionError => IsAvailable ? Vector3.Distance(TruePosition, ReportedPosition) : -1f;
    }
}

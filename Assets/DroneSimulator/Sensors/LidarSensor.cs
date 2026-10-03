using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Simulated LiDAR: degradation shrinks effective range and point reliability.
    /// Lightweight prototype model (no per-frame raycasts); a real raycast sweep
    /// can replace ReportedRange later behind the same interface.
    /// </summary>
    public sealed class LidarSensor : SensorBase
    {
        public override string SensorName => "LIDAR";

        [Tooltip("Nominal max range in meters at full health.")]
        [Min(1f)] [SerializeField] private float maxRange = 60f;

        public float EffectiveRange => IsAvailable ? maxRange * (1f - DegradationFactor * 0.8f) : 0f;

        public float PointReliability => Accuracy;

        public float ReportedRange
        {
            get
            {
                if (!IsAvailable) return -1f;
                float k = DegradationFactor;
                float wobble = SignedNoise(Mathf.PerlinNoise(Time.time * noiseFrequency, 51.2f)) * 0.15f * k;
                return Mathf.Max(0f, EffectiveRange * (0.9f + wobble));
            }
        }
    }
}

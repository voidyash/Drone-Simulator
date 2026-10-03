using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Simulated radar: degradation lowers detection probability and raises
    /// false alarms. Counts the 3 reference gates as prototype contacts.
    /// </summary>
    public sealed class RadarSensor : SensorBase
    {
        public override string SensorName => "RADAR";

        [Tooltip("Prototype contact count (reference gates).")]
        [Min(0)] [SerializeField] private int baseContacts = 3;

        public float DetectionProbability => Accuracy;

        public float FalseAlarmRate => Noise * 0.3f;

        public int ReportedContacts
        {
            get
            {
                if (!IsAvailable) return 0;
                float k = DegradationFactor;
                if (k <= 0.001f) return baseContacts;
                float jitter = SignedNoise(Mathf.PerlinNoise(Time.time * noiseFrequency, 77.7f));
                int dropped = Mathf.RoundToInt(k * baseContacts + jitter * k);
                return Mathf.Clamp(baseContacts - Mathf.Max(0, dropped), 0, baseContacts);
            }
        }
    }
}

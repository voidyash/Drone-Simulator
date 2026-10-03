using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Shared degradation model: Health = 100 - degradation, Accuracy = 1 - k,
    /// Noise = k, unavailable at 100. Subclasses only implement their output.
    /// </summary>
    public abstract class SensorBase : MonoBehaviour, ISensor
    {
        [Range(0f, 100f)]
        [SerializeField] protected float degradation;

        [Tooltip("Perlin frequency for this sensor's noise. Higher = jumpier.")]
        [Min(0.01f)]
        [SerializeField] protected float noiseFrequency = 1.2f;

        public abstract string SensorName { get; }

        public float Degradation => degradation;
        public float Health => 100f - degradation;
        public float Accuracy => 1f - Mathf.Clamp01(degradation / 100f);
        public float Noise => Mathf.Clamp01(degradation / 100f);
        public bool IsAvailable => degradation < 100f;

        public virtual void SetDegradation(float degradationValue)
        {
            degradation = Mathf.Clamp(degradationValue, 0f, 100f);
        }

        protected float DegradationFactor => Mathf.Clamp01(degradation / 100f);

        protected static float SignedNoise(float perlin)
        {
            return (perlin - 0.5f) * 2f;
        }
    }
}

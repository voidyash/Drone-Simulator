using DroneSimulator.Environment;
using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Simulated camera: degradation reduces effective visibility and adds
    /// noise/distortion. Combines scenario visibility with sensor health so a
    /// degraded camera in fog is worse than either alone.
    /// </summary>
    public sealed class CameraSensor : SensorBase
    {
        public override string SensorName => "CAM";

        private WeatherManager weatherManager;
        private TerrainManager terrainManager;

        private void Awake()
        {
            weatherManager = FindAnyObjectByType<WeatherManager>();
            terrainManager = FindAnyObjectByType<TerrainManager>();
        }

        public float ScenarioVisibility => weatherManager != null ? weatherManager.Visibility / 100f : 1f;

        public float EffectiveVisibility
        {
            get
            {
                if (!IsAvailable) return 0f;
                float obstruction = 1f;
                if (terrainManager == null)
                {
                    terrainManager = FindAnyObjectByType<TerrainManager>();
                }

                if (terrainManager != null)
                {
                    obstruction = terrainManager.ObstructionFactor;
                }

                return Mathf.Clamp01(ScenarioVisibility * Accuracy * obstruction);
            }
        }

        public float NoiseLevel => Noise;

        public float Distortion => IsAvailable ? Noise * 0.5f : 1f;

        public string Status => !IsAvailable ? "LOST" : Degradation >= 60f ? "DEGRADED" : Degradation > 0.5f ? "NOISY" : "OK";
    }
}

using DroneSimulator.Environment;
using UnityEngine;

namespace DroneSimulator.Configuration
{
    public enum SimulationSpeedPreset
    {
        Quarter,
        Half,
        Normal,
        Double,
        Quadruple
    }

    public enum TimeOfDayPreset
    {
        Dawn,
        Day,
        Dusk,
        Night
    }

    public enum WeatherPreset
    {
        Clear,
        Cloudy,
        Fog,
        Rain
    }

    [CreateAssetMenu(fileName = "ScenarioConfig", menuName = "Drone Simulator/Scenario Config")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [Header("Phase 1 Simulation")]
        [SerializeField] private SimulationSpeedPreset initialSimulationSpeed = SimulationSpeedPreset.Normal;

        [Header("Primary Drone")]
        [Min(1f)]
        [SerializeField] private float maximumFlightSpeed = 18f;

        [Header("Phase 2 Environment")]
        [SerializeField] private TimeOfDayPreset timeOfDay = TimeOfDayPreset.Day;
        [SerializeField] private WeatherPreset weather = WeatherPreset.Clear;
        [Range(0f, 100f)]
        [SerializeField] private float windStrength = 10f;
        [Range(0f, 360f)]
        [SerializeField] private float windDirectionDegrees;
        [Range(0f, 100f)]
        [SerializeField] private float turbulence;
        [Range(0f, 100f)]
        [SerializeField] private float visibility = 100f;

        [Header("Phase 3 Sensors")]
        [Tooltip("Overall sensor degradation 0-100 applied to all sensors at scenario start. Runtime adjustable with I/K.")]
        [Range(0f, 100f)]
        [SerializeField] private float sensorDegradation;

        [Header("Terrain")]
        [SerializeField] private TerrainPreset terrain = TerrainPreset.Rural;

        public SimulationSpeedPreset InitialSimulationSpeed => initialSimulationSpeed;
        public float MaximumFlightSpeed => maximumFlightSpeed;
        public TimeOfDayPreset TimeOfDay => timeOfDay;
        public WeatherPreset Weather => weather;
        public float WindStrength => windStrength;
        public float WindDirectionDegrees => windDirectionDegrees;
        public float Turbulence => turbulence;
        public float Visibility => visibility;
        public float SensorDegradation => sensorDegradation;
        public TerrainPreset Terrain => terrain;
    }
}

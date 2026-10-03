using DroneSimulator.Configuration;
using UnityEngine;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Phase 2 facade: single entry point for scenario + runtime environment control.
    /// Delegates only — no duplicated wind/weather/time logic here.
    /// All setters apply immediately so runtime changes affect the running sim.
    /// </summary>
    public sealed class EnvironmentManager : MonoBehaviour
    {
        [SerializeField] private WindSystem windSystem;
        [SerializeField] private WeatherManager weatherManager;
        [SerializeField] private TimeOfDayController timeOfDayController;
        [SerializeField] private TerrainManager terrainManager;

        public WindSystem Wind => windSystem;
        public WeatherManager Weather => weatherManager;
        public TimeOfDayController TimeOfDay => timeOfDayController;
        public TerrainManager Terrain => terrainManager;

        private void Awake()
        {
            ResolveReferences();
        }

        public void ApplyScenario(ScenarioConfig scenario)
        {
            if (scenario == null)
            {
                return;
            }

            ResolveReferences();
            windSystem?.Configure(scenario.WindStrength, scenario.WindDirectionDegrees, scenario.Turbulence);
            weatherManager?.Configure(scenario.Weather, scenario.Visibility);
            timeOfDayController?.Configure(scenario.TimeOfDay);
            terrainManager?.Configure(scenario.Terrain);
        }

        public void SetTerrain(TerrainPreset preset)
        {
            ResolveReferences();
            terrainManager?.SetTerrain(preset);
        }

        public void SetWindStrength(float strength)
        {
            ResolveReferences();
            windSystem?.SetWindStrength(strength);
        }

        public void SetWindDirection(float directionDegrees)
        {
            ResolveReferences();
            windSystem?.SetWindDirection(directionDegrees);
        }

        public void SetTurbulence(float turbulence)
        {
            ResolveReferences();
            windSystem?.SetTurbulence(turbulence);
        }

        public void SetVisibility(float visibility)
        {
            ResolveReferences();
            weatherManager?.SetVisibility(visibility);
        }

        public void SetWeather(WeatherPreset weather)
        {
            ResolveReferences();
            weatherManager?.SetWeather(weather);
        }

        public void SetTimeOfDay(TimeOfDayPreset preset)
        {
            ResolveReferences();
            timeOfDayController?.SetTimeOfDay(preset);
        }

        private void ResolveReferences()
        {
            if (windSystem == null)
            {
                windSystem = GetComponent<WindSystem>();
            }

            if (windSystem == null)
            {
                windSystem = FindAnyObjectByType<WindSystem>();
            }

            if (weatherManager == null)
            {
                weatherManager = GetComponent<WeatherManager>();
            }

            if (weatherManager == null)
            {
                weatherManager = FindAnyObjectByType<WeatherManager>();
            }

            if (timeOfDayController == null)
            {
                timeOfDayController = GetComponent<TimeOfDayController>();
            }

            if (timeOfDayController == null)
            {
                timeOfDayController = FindAnyObjectByType<TimeOfDayController>();
            }

            if (terrainManager == null)
            {
                terrainManager = GetComponent<TerrainManager>();
            }

            if (terrainManager == null)
            {
                terrainManager = FindAnyObjectByType<TerrainManager>();
            }
        }
    }
}

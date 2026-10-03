using UnityEngine;
using UnityEngine.InputSystem;
using DroneSimulator.Configuration;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Runtime keyboard controls for environment. Delegates to EnvironmentManager only.
    /// Uses Keyboard.current directly to avoid hand-editing the .inputactions asset;
    /// the full Runtime panel (spec Phase 14) will replace this with proper UI.
    /// Shortcuts (shown in HUD):
    /// R/F wind strength +/-, T/G wind direction +/-, Y/H turbulence +/-,
    /// U/J visibility +/-, V cycle weather, B cycle time of day.
    /// </summary>
    public sealed class EnvironmentRuntimeControls : MonoBehaviour
    {
        [SerializeField] private EnvironmentManager environmentManager;

        [Header("Steps")]
        [Min(0.1f)] [SerializeField] private float windStrengthStep = 10f;
        [Min(1f)] [SerializeField] private float windDirectionStep = 15f;
        [Min(1f)] [SerializeField] private float turbulenceStep = 10f;
        [Min(1f)] [SerializeField] private float visibilityStep = 10f;

        private void Awake()
        {
            ResolveManager();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            ResolveManager();
            if (environmentManager == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame) AdjustWindStrength(windStrengthStep);
            if (keyboard.fKey.wasPressedThisFrame) AdjustWindStrength(-windStrengthStep);
            if (keyboard.tKey.wasPressedThisFrame) AdjustWindDirection(windDirectionStep);
            if (keyboard.gKey.wasPressedThisFrame) AdjustWindDirection(-windDirectionStep);
            if (keyboard.yKey.wasPressedThisFrame) AdjustTurbulence(turbulenceStep);
            if (keyboard.hKey.wasPressedThisFrame) AdjustTurbulence(-turbulenceStep);
            if (keyboard.uKey.wasPressedThisFrame) AdjustVisibility(visibilityStep);
            if (keyboard.jKey.wasPressedThisFrame) AdjustVisibility(-visibilityStep);
            if (keyboard.vKey.wasPressedThisFrame) CycleWeather();
            if (keyboard.bKey.wasPressedThisFrame) CycleTimeOfDay();
        }

        public void AdjustWindStrength(float delta)
        {
            ResolveManager();
            var wind = environmentManager?.Wind;
            if (wind == null) return;
            environmentManager.SetWindStrength(wind.WindStrength + delta);
        }

        public void AdjustWindDirection(float deltaDegrees)
        {
            ResolveManager();
            var wind = environmentManager?.Wind;
            if (wind == null) return;
            environmentManager.SetWindDirection(wind.WindDirectionDegrees + deltaDegrees);
        }

        public void AdjustTurbulence(float delta)
        {
            ResolveManager();
            var wind = environmentManager?.Wind;
            if (wind == null) return;
            environmentManager.SetTurbulence(wind.Turbulence + delta);
        }

        public void AdjustVisibility(float delta)
        {
            ResolveManager();
            var weather = environmentManager?.Weather;
            if (weather == null) return;
            environmentManager.SetVisibility(weather.Visibility + delta);
        }

        public void CycleWeather()
        {
            ResolveManager();
            var weather = environmentManager?.Weather;
            if (weather == null) return;
            var next = (WeatherPreset)(((int)weather.CurrentWeather + 1) % 4);
            environmentManager.SetWeather(next);
        }

        public void CycleTimeOfDay()
        {
            ResolveManager();
            var tod = environmentManager?.TimeOfDay;
            if (tod == null) return;
            var next = (TimeOfDayPreset)(((int)tod.CurrentTimeOfDay + 1) % 4);
            environmentManager.SetTimeOfDay(next);
        }

        private void ResolveManager()
        {
            if (environmentManager == null)
            {
                environmentManager = GetComponent<EnvironmentManager>();
            }

            if (environmentManager == null)
            {
                environmentManager = FindAnyObjectByType<EnvironmentManager>();
            }
        }
    }
}

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

            if (Pressed(keyboard, "windUp")) AdjustWindStrength(windStrengthStep);
            if (Pressed(keyboard, "windDown")) AdjustWindStrength(-windStrengthStep);
            if (Pressed(keyboard, "windDirUp")) AdjustWindDirection(windDirectionStep);
            if (Pressed(keyboard, "windDirDown")) AdjustWindDirection(-windDirectionStep);
            if (Pressed(keyboard, "turbUp")) AdjustTurbulence(turbulenceStep);
            if (Pressed(keyboard, "turbDown")) AdjustTurbulence(-turbulenceStep);
            if (Pressed(keyboard, "visUp")) AdjustVisibility(visibilityStep);
            if (Pressed(keyboard, "visDown")) AdjustVisibility(-visibilityStep);
            if (Pressed(keyboard, "weather")) CycleWeather();
            if (Pressed(keyboard, "time")) CycleTimeOfDay();
        }

        private static bool Pressed(Keyboard keyboard, string bindingId)
        {
            Key key = KeyBindings.Get(bindingId);
            if (key == Key.None)
            {
                return false;
            }

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
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

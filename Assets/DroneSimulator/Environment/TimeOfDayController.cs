using DroneSimulator.Configuration;
using UnityEngine;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Phase 2: Dawn/Day/Dusk/Night lighting. Affects the Directional Light
    /// and ambient so FPV visibility genuinely changes at night, not just UI.
    /// </summary>
    public sealed class TimeOfDayController : MonoBehaviour
    {
        [SerializeField] private TimeOfDayPreset timeOfDay = TimeOfDayPreset.Day;
        [SerializeField] private Light directionalLight;

        public TimeOfDayPreset CurrentTimeOfDay => timeOfDay;

        private void Awake()
        {
            ResolveLight();
            ApplyTimeOfDay(timeOfDay);
        }

        public void SetTimeOfDay(TimeOfDayPreset preset)
        {
            timeOfDay = preset;
            ApplyTimeOfDay(preset);
        }

        public void Configure(TimeOfDayPreset preset)
        {
            SetTimeOfDay(preset);
        }

        private void ResolveLight()
        {
            if (directionalLight != null)
            {
                return;
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLight = light;
                    return;
                }
            }
        }

        private void ApplyTimeOfDay(TimeOfDayPreset preset)
        {
            ResolveLight();

            Color lightColor = Color.white;
            Color ambient = new Color(0.7f, 0.7f, 0.7f);
            float intensity = 2f;
            Vector3 rotation = new Vector3(50f, -30f, 0f);

            switch (preset)
            {
                case TimeOfDayPreset.Dawn:
                    rotation = new Vector3(12f, 90f, 0f);
                    intensity = 1.2f;
                    lightColor = new Color(1f, 0.72f, 0.52f);
                    ambient = new Color(0.5f, 0.45f, 0.45f);
                    break;
                case TimeOfDayPreset.Day:
                    rotation = new Vector3(50f, -30f, 0f);
                    intensity = 2f;
                    lightColor = Color.white;
                    ambient = new Color(0.7f, 0.7f, 0.7f);
                    break;
                case TimeOfDayPreset.Dusk:
                    rotation = new Vector3(10f, -90f, 0f);
                    intensity = 1f;
                    lightColor = new Color(1f, 0.6f, 0.42f);
                    ambient = new Color(0.45f, 0.4f, 0.45f);
                    break;
                case TimeOfDayPreset.Night:
                    rotation = new Vector3(-25f, 30f, 0f);
                    intensity = 0.15f;
                    lightColor = new Color(0.6f, 0.72f, 1f);
                    ambient = new Color(0.08f, 0.09f, 0.13f);
                    break;
            }

            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(rotation);
                directionalLight.intensity = intensity;
                directionalLight.color = lightColor;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
        }
    }
}

using DroneSimulator.Configuration;
using UnityEngine;

namespace DroneSimulator.Environment
{
    /// <summary>
    /// Phase 2: Clear/Cloudy/Fog/Rain + Visibility 0-100.
    /// Visibility genuinely changes fog density and FPV far clip,
    /// so it affects navigation, not just a slider label.
    /// </summary>
    public sealed class WeatherManager : MonoBehaviour
    {
        [SerializeField] private WeatherPreset weather = WeatherPreset.Clear;
        [Range(0f, 100f)]
        [SerializeField] private float visibility = 100f;
        [SerializeField] private Camera fpvCamera;

        private const float MinFarClip = 25f;
        private const float MaxFarClip = 1000f;

        public WeatherPreset CurrentWeather => weather;
        public float Visibility => visibility;

        private void Awake()
        {
            ResolveCamera();
            ApplyWeather(weather, visibility);
        }

        public void SetWeather(WeatherPreset preset)
        {
            weather = preset;
            ApplyWeather(weather, visibility);
        }

        public void SetVisibility(float visibilityValue)
        {
            visibility = Mathf.Clamp(visibilityValue, 0f, 100f);
            ApplyWeather(weather, visibility);
        }

        public void Configure(WeatherPreset preset, float visibilityValue)
        {
            weather = preset;
            visibility = Mathf.Clamp(visibilityValue, 0f, 100f);
            ApplyWeather(weather, visibility);
        }

        /// <summary>
        /// Re-applies current weather + visibility from stored state.
        /// Used by TerrainManager so stacked modifiers never compound.
        /// </summary>
        public void Reapply()
        {
            ApplyWeather(weather, visibility);
        }

        private void ResolveCamera()
        {
            if (fpvCamera != null)
            {
                return;
            }

            if (Camera.main != null)
            {
                fpvCamera = Camera.main;
                return;
            }

            fpvCamera = FindAnyObjectByType<Camera>();
        }

        private void ApplyWeather(WeatherPreset preset, float visibilityValue)
        {
            ResolveCamera();

            float clarity = Mathf.Clamp01(visibilityValue / 100f);
            float farClip = Mathf.Lerp(MinFarClip, MaxFarClip, clarity * clarity);

            // Visibility always matters: even Clear gets haze below 40 so the
            // slider is never a no-op. Other presets scale up from there.
            bool useFog = preset != WeatherPreset.Clear || clarity < 0.4f;
            float baseDensity = preset switch
            {
                WeatherPreset.Cloudy => 0.02f,
                WeatherPreset.Fog => 0.09f,
                WeatherPreset.Rain => 0.045f,
                _ => 0.015f,
            };
            float density = baseDensity * (1f - clarity * 0.9f);

            Color fogColor = preset switch
            {
                WeatherPreset.Cloudy => new Color(0.62f, 0.65f, 0.7f),
                WeatherPreset.Fog => new Color(0.75f, 0.78f, 0.82f),
                WeatherPreset.Rain => new Color(0.45f, 0.5f, 0.58f),
                _ => Color.gray,
            };

            RenderSettings.fog = useFog;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = useFog ? Mathf.Max(density, 0.0005f) : 0f;
            RenderSettings.fogColor = fogColor;

            if (fpvCamera != null)
            {
                fpvCamera.farClipPlane = farClip;
            }
        }
    }
}

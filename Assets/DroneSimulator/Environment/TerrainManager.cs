using UnityEngine;

namespace DroneSimulator.Environment
{
    public enum TerrainPreset
    {
        Rural,
        Urban
    }

    /// <summary>
    /// Terrain preset: Urban street canyons trap haze (denser fog, shorter
    /// view) and degrade optical sensing; Rural is open. Both rebuild the
    /// physical ObstacleField (buildings vs rocks/poles) so terrain affects
    /// navigation, not just visuals. Applies on top of WeatherManager via
    /// Reapply() so values never compound.
    /// </summary>
    public sealed class TerrainManager : MonoBehaviour
    {
        [SerializeField] private TerrainPreset terrain = TerrainPreset.Rural;

        private WeatherManager weatherManager;
        private ObstacleField obstacleField;
        private Camera fpvCamera;

        public TerrainPreset Terrain => terrain;

        /// <summary>Optical obstruction factor: Urban 0.7, Rural 1.0.</summary>
        public float ObstructionFactor => terrain == TerrainPreset.Urban ? 0.7f : 1f;

        private void Awake()
        {
            ResolveReferences();
        }

        public void SetTerrain(TerrainPreset preset)
        {
            terrain = preset;
            ApplyTerrain();
        }

        public void Configure(TerrainPreset preset)
        {
            SetTerrain(preset);
        }

        private void ApplyTerrain()
        {
            ResolveReferences();
            weatherManager?.Reapply();
            obstacleField?.Rebuild(terrain);

            if (terrain != TerrainPreset.Urban)
            {
                return;
            }

            RenderSettings.fogDensity *= 1.35f;
            if (fpvCamera != null)
            {
                fpvCamera.farClipPlane *= 0.8f;
            }
        }

        private void ResolveReferences()
        {
            if (weatherManager == null)
            {
                weatherManager = GetComponent<WeatherManager>();
            }

            if (weatherManager == null)
            {
                weatherManager = FindAnyObjectByType<WeatherManager>();
            }

            if (obstacleField == null)
            {
                obstacleField = GetComponent<ObstacleField>();
            }

            if (obstacleField == null)
            {
                obstacleField = FindAnyObjectByType<ObstacleField>();
            }

            if (fpvCamera == null)
            {
                fpvCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            }
        }
    }
}

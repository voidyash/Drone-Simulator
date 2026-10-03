using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Runtime sensor degradation keys. Delegates to SensorManager only.
    /// I/K: overall degradation -/+, O: reset to 0. Free keys (no clash with
    /// WASD/QE/ZC/mouse/[]/RFTGYHUJVB previous bindings).
    /// </summary>
    public sealed class SensorRuntimeControls : MonoBehaviour
    {
        [SerializeField] private SensorManager sensorManager;
        [Min(1f)] [SerializeField] private float degradationStep = 10f;

        private void Awake()
        {
            ResolveManager();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            ResolveManager();
            if (sensorManager == null) return;

            if (keyboard.iKey.wasPressedThisFrame) AdjustOverall(degradationStep);
            if (keyboard.kKey.wasPressedThisFrame) AdjustOverall(-degradationStep);
            if (keyboard.oKey.wasPressedThisFrame) sensorManager.SetOverallDegradation(0f);
        }

        public void AdjustOverall(float delta)
        {
            ResolveManager();
            if (sensorManager == null) return;
            sensorManager.SetOverallDegradation(sensorManager.OverallDegradation + delta);
        }

        private void ResolveManager()
        {
            if (sensorManager == null) sensorManager = GetComponent<SensorManager>();
            if (sensorManager == null) sensorManager = FindAnyObjectByType<SensorManager>();
        }
    }
}

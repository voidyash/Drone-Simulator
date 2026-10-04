using DroneSimulator.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSimulator.Simulation
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SimulationClock))]
    [RequireComponent(typeof(ScenarioManager))]
    public sealed class SimulationManager : MonoBehaviour
    {
        private SimulationClock simulationClock;
        private ScenarioManager scenarioManager;
        private InputActionMap droneActionMap;
        private InputAction speedDownAction;
        private InputAction speedUpAction;

        public SimulationClock Clock => simulationClock;

        private void Awake()
        {
            simulationClock = GetComponent<SimulationClock>();
            scenarioManager = GetComponent<ScenarioManager>();

            simulationClock.Initialize(SimulationSpeedPreset.Normal);
            BindRuntimeControls();
            scenarioManager.ApplyInitialScenario(this);
        }

        private void Update()
        {
            if (speedDownAction != null && speedDownAction.WasPressedThisFrame())
            {
                simulationClock.DecreaseMultiplier();
            }

            if (speedUpAction != null && speedUpAction.WasPressedThisFrame())
            {
                simulationClock.IncreaseMultiplier();
            }
        }

        public void SetTimeMultiplier(SimulationSpeedPreset preset)
        {
            simulationClock.Initialize(preset);
        }

        private void BindRuntimeControls()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("No project-wide InputActionAsset is configured. Simulation speed controls are unavailable.", this);
                return;
            }

            DroneSimulator.Configuration.InputRebindStore.Apply(actions);
            droneActionMap = actions.FindActionMap("Drone", false);
            if (droneActionMap == null)
            {
                Debug.LogError("The configured InputActionAsset does not contain the required Drone action map.", this);
                return;
            }

            speedDownAction = droneActionMap.FindAction("SimulationSpeedDown", true);
            speedUpAction = droneActionMap.FindAction("SimulationSpeedUp", true);
            droneActionMap.Enable();
        }

        private void OnDestroy()
        {
            droneActionMap?.Disable();
        }
    }
}

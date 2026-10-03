using DroneSimulator.Configuration;
using DroneSimulator.Drones;
using DroneSimulator.Environment;
using DroneSimulator.Sensors;
using UnityEngine;

namespace DroneSimulator.Simulation
{
    public sealed class ScenarioManager : MonoBehaviour
    {
        [SerializeField] private ScenarioConfig activeScenario;
        [SerializeField] private string defaultScenarioResourcePath = "Scenarios/DefaultDroneScenario";

        public ScenarioConfig ActiveScenario => activeScenario;

        public void ApplyInitialScenario(SimulationManager simulationManager)
        {
            var scenario = activeScenario != null
                ? activeScenario
                : Resources.Load<ScenarioConfig>(defaultScenarioResourcePath);

            if (scenario == null)
            {
                Debug.LogWarning(
                    "ScenarioManager could not load a ScenarioConfig. Assign one or add the default resource before running the simulation.",
                    this);
                return;
            }

            simulationManager.SetTimeMultiplier(scenario.InitialSimulationSpeed);

            var environment = FindAnyObjectByType<EnvironmentManager>();
            if (environment != null)
            {
                environment.ApplyScenario(scenario);
            }

            var primaryDrone = FindAnyObjectByType<DroneController>();
            if (primaryDrone == null)
            {
                Debug.LogError("ScenarioManager could not find a DroneController to configure.", this);
                return;
            }

            primaryDrone.ConfigureMaximumSpeed(scenario.MaximumFlightSpeed);

            var sensors = FindAnyObjectByType<SensorManager>();
            if (sensors != null)
            {
                sensors.ApplyScenario(scenario.SensorDegradation);
            }
        }
    }
}

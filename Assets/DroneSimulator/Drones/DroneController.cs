using DroneSimulator.Sensors;
using UnityEngine;

namespace DroneSimulator.Drones
{
    [RequireComponent(typeof(FlightController))]
    [RequireComponent(typeof(DroneTelemetry))]
    public sealed class DroneController : MonoBehaviour
    {
        [SerializeField] private string droneId = "DRONE-001";
        [SerializeField] private ThreatState threatState = ThreatState.Unknown;

        private FlightController flightController;
        private DroneTelemetry telemetry;
        private SensorManager sensorManager;

        public string DroneId => droneId;
        public ThreatState Threat => threatState;
        public FlightController FlightController => flightController;
        public DroneTelemetry Telemetry => telemetry;
        public SensorManager Sensors
        {
            get
            {
                ResolveReferences();
                return sensorManager;
            }
        }

        private void Awake()
        {
            ResolveReferences();
        }

        public void ConfigureMaximumSpeed(float maximumSpeed)
        {
            ResolveReferences();
            if (flightController == null)
            {
                Debug.LogError("DroneController is missing its required FlightController.", this);
                return;
            }

            flightController.ConfigureMaximumSpeed(maximumSpeed);
        }

        public void SetDroneId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            droneId = id;
            gameObject.name = id;
        }

        public void SetThreatState(ThreatState state)
        {
            threatState = state;
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<FlightController>();
            }

            if (telemetry == null)
            {
                telemetry = GetComponent<DroneTelemetry>();
            }

            if (sensorManager == null)
            {
                sensorManager = GetComponent<SensorManager>();
            }
        }
    }
}

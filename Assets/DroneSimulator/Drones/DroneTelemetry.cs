using DroneSimulator.Sensors;
using UnityEngine;

namespace DroneSimulator.Drones
{
    [RequireComponent(typeof(FlightController))]
    public sealed class DroneTelemetry : MonoBehaviour
    {
        private FlightController flightController;
        private SensorManager sensors;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => flightController == null ? Vector3.zero : flightController.Velocity;
        public float Speed => Velocity.magnitude;
        public float Altitude => transform.position.y;
        public float Heading => transform.eulerAngles.y;
        public float MaximumSpeed => flightController == null ? 0f : flightController.MaximumSpeed;

        public SensorManager Sensors
        {
            get
            {
                if (sensors == null)
                {
                    sensors = GetComponent<SensorManager>();
                }

                if (sensors == null)
                {
                    sensors = FindAnyObjectByType<SensorManager>();
                }

                return sensors;
            }
        }

        public Vector3 ReportedPosition => Sensors != null && Sensors.Gps != null ? Sensors.Gps.ReportedPosition : Position;
        public float PositionError => Sensors != null && Sensors.Gps != null ? Sensors.Gps.PositionError : 0f;
        public float ReportedHeading => Sensors != null && Sensors.Imu != null ? Sensors.Imu.ReportedYaw : Heading;

        private void Awake()
        {
            flightController = GetComponent<FlightController>();
        }
    }
}

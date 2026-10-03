using DroneSimulator.Drones;
using DroneSimulator.Environment;
using DroneSimulator.Sensors;
using DroneSimulator.Simulation;
using UnityEngine;

namespace DroneSimulator.UI
{
    public sealed class DroneTelemetryDisplay : MonoBehaviour
    {
        [SerializeField] private DroneTelemetry telemetry;

        private SimulationClock simulationClock;
        private EnvironmentManager environmentManager;

        private void Awake()
        {
            if (telemetry == null)
            {
                telemetry = FindAnyObjectByType<DroneTelemetry>();
            }

            simulationClock = FindAnyObjectByType<SimulationClock>();
            environmentManager = FindAnyObjectByType<EnvironmentManager>();
        }

        private void OnGUI()
        {
            if (telemetry == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 380f, 560f), GUI.skin.box);
            GUILayout.Label("DRONE SIMULATOR | FPV TELEMETRY");
            GUILayout.Label($"Speed: {telemetry.Speed:0.0} / {telemetry.MaximumSpeed:0.0} m/s");
            GUILayout.Label($"Altitude: {telemetry.Altitude:0.0} m");
            GUILayout.Label($"Heading: {telemetry.Heading:000}° (IMU: {telemetry.ReportedHeading:000}°)");
            GUILayout.Label($"Velocity: {telemetry.Velocity.x:0.0}, {telemetry.Velocity.y:0.0}, {telemetry.Velocity.z:0.0}");
            GUILayout.Label($"GPS: {telemetry.ReportedPosition.x:0.0}, {telemetry.ReportedPosition.y:0.0}, {telemetry.ReportedPosition.z:0.0} (err {telemetry.PositionError:0.0} m)");
            GUILayout.Space(8f);
            GUILayout.Label($"Simulation: {(simulationClock == null ? 1f : simulationClock.TimeMultiplier):0.##}x  ([ / ] to change)");
            GUILayout.Label("WASD: move | Q/E: descend/ascend | Z/C or mouse: turn");

            var wind = environmentManager != null ? environmentManager.Wind : null;
            var weather = environmentManager != null ? environmentManager.Weather : null;
            var tod = environmentManager != null ? environmentManager.TimeOfDay : null;
            if (wind != null && weather != null && tod != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Wind: {wind.WindStrength:0} / 100 @ {wind.WindDirectionDegrees:000}°  (R/F str, T/G dir)");
                GUILayout.Label($"Turbulence: {wind.Turbulence:0} / 100  (Y/H)");
                GUILayout.Label($"Visibility: {weather.Visibility:0} / 100  (U/J)");
                GUILayout.Label($"Weather: {weather.CurrentWeather}  (V to cycle)");
                GUILayout.Label($"Time: {tod.CurrentTimeOfDay}  (B to cycle)");
            }

            var sensors = telemetry.Sensors;
            if (sensors != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"SENSORS deg {sensors.OverallDegradation:0} (I/K +/-, O reset)");
                GUILayout.Label($"GPS {(sensors.Gps != null ? sensors.Gps.Health : 0):0} err {(sensors.Gps != null ? sensors.Gps.PositionError : 0):0.0}m | IMU {(sensors.Imu != null ? sensors.Imu.Health : 0):0} yawerr {(sensors.Imu != null ? sensors.Imu.YawError : 0):0.0}°");
                GUILayout.Label($"CAM {(sensors.Camera != null ? sensors.Camera.Status : "-")} vis {(sensors.Camera != null ? sensors.Camera.EffectiveVisibility * 100f : 0):0}% | LIDAR rng {(sensors.Lidar != null ? sensors.Lidar.ReportedRange : 0):0}m rel {(sensors.Lidar != null ? sensors.Lidar.PointReliability * 100f : 0):0}%");
                GUILayout.Label($"RADAR contacts {(sensors.Radar != null ? sensors.Radar.ReportedContacts : 0)} rel {(sensors.Radar != null ? sensors.Radar.DetectionProbability * 100f : 0):0}%");
            }

            GUILayout.EndArea();
        }
    }
}

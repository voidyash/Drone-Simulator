using UnityEngine;

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Per-drone sensor aggregator. Owns the 5 sensors, applies scenario +
    /// runtime degradation, and is the single telemetry source for sensors.
    /// </summary>
    public sealed class SensorManager : MonoBehaviour
    {
        [SerializeField] private GPSSensor gps;
        [SerializeField] private IMUSensor imu;
        [SerializeField] private CameraSensor cameraSensor;
        [SerializeField] private LidarSensor lidar;
        [SerializeField] private RadarSensor radar;

        [Range(0f, 100f)]
        [SerializeField] private float overallDegradation;

        public GPSSensor Gps => gps;
        public IMUSensor Imu => imu;
        public CameraSensor Camera => cameraSensor;
        public LidarSensor Lidar => lidar;
        public RadarSensor Radar => radar;
        public float OverallDegradation => overallDegradation;

        private void Awake()
        {
            ResolveSensors();
        }

        public void ApplyScenario(float degradation)
        {
            SetOverallDegradation(degradation);
        }

        public void SetOverallDegradation(float degradation)
        {
            overallDegradation = Mathf.Clamp(degradation, 0f, 100f);
            ResolveSensors();
            gps?.SetDegradation(overallDegradation);
            imu?.SetDegradation(overallDegradation);
            cameraSensor?.SetDegradation(overallDegradation);
            lidar?.SetDegradation(overallDegradation);
            radar?.SetDegradation(overallDegradation);
        }

        public bool SetSensorDegradation(string sensorName, float degradation)
        {
            ResolveSensors();
            switch (sensorName?.Trim().ToUpperInvariant())
            {
                case "GPS": gps?.SetDegradation(degradation); return gps != null;
                case "IMU": imu?.SetDegradation(degradation); return imu != null;
                case "CAM": case "CAMERA": cameraSensor?.SetDegradation(degradation); return cameraSensor != null;
                case "LIDAR": lidar?.SetDegradation(degradation); return lidar != null;
                case "RADAR": radar?.SetDegradation(degradation); return radar != null;
                default: return false;
            }
        }

        private void ResolveSensors()
        {
            if (gps == null) gps = GetComponent<GPSSensor>();
            if (imu == null) imu = GetComponent<IMUSensor>();
            if (cameraSensor == null) cameraSensor = GetComponent<CameraSensor>();
            if (lidar == null) lidar = GetComponent<LidarSensor>();
            if (radar == null) radar = GetComponent<RadarSensor>();
        }
    }
}

namespace DroneSimulator.Sensors
{
    /// <summary>
    /// Common contract for all drone sensors. Health/Accuracy/Noise/Availability
    /// are derived from Degradation 0-100 so every sensor degrades uniformly.
    /// </summary>
    public interface ISensor
    {
        string SensorName { get; }
        float Health { get; }
        float Accuracy { get; }
        float Noise { get; }
        bool IsAvailable { get; }
        float Degradation { get; }
        void SetDegradation(float degradation);
    }
}

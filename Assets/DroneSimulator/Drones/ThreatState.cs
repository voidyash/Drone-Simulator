namespace DroneSimulator.Drones
{
    /// <summary>
    /// Prototype classification only. Armed means "flagged as a threat",
    /// never a weapon — no weapon mechanics exist in this project.
    /// </summary>
    public enum ThreatState
    {
        Unknown,
        Unarmed,
        Armed
    }
}

namespace DialysisServer.Models;

public class SensorData
{
    public int Id { get; set; }
    public int Cadence { get; set; }
    public double Speed { get; set; }
    public DateTime Timestamp { get; set; }
}
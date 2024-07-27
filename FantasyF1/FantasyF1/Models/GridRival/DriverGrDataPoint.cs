namespace FantasyF1.Models.GridRival;

public class DriverGrDataPoint
{
    public String Name { get; set; }
    public float AveragePoints { get; set; }
    public float AveragePointsAdjusted { get; set; }
    public float CurrentValue { get; set; }
    public bool IsAvailable { get; set; }
}
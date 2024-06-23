namespace FantasyF1.Models.OpenData;

public class DriverFpDataPoint
{
    public String Name { get; set; }
    public List<FpData> FpData { get; set; } = new List<FpData>();
}
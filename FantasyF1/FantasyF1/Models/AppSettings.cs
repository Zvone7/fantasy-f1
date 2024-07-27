using FantasyF1.Models.GridRival;

namespace FantasyF1.Models;

public class AppSettings
{
    public int MaxResults { get; set; }
    public int HighAveragePointsDeductionInPercentForDrivers { get; set; }
    public int HighAveragePointsDeductionInPercentForConstructors { get; set; }
    public float ConstructorPerformanceImportanceMultiplier { get; set; }
    public List<TyrePointsMultiplier> TyrePointsMultipliers { get; set; }
    public List<Driver> DriverInformation { get; set; }
    public List<Constructor> ConstructorInformation { get; set; }
    public GridRivalSettings GridRivalSettings { get; set; }
}
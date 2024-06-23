using FantasyF1.Models.GridRival;

namespace FantasyF1.Models;

public class AppSettings
{
    public int MaxResults { get; set; }
    public List<TyrePointsMultiplier> TyrePointsMultipliers { get; set; }
    public List<Driver> DriverInformation { get; set; }
    public GridRivalSettings GridRivalSettings { get; set; }
}
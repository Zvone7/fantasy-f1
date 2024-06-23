using FantasyF1.Helpers;
using FantasyF1.Models;
using FantasyF1.Models.GridRival;
using FantasyF1.Models.OpenData;

namespace FantasyF1.Services;

public class LineupSuggestor
{
    private readonly AppSettings _appSettings_;
    private readonly RoundSettings _roundSettings_;
    private const int BASE_POINTS_PER_FP = 100;
    public LineupSuggestor(AppSettings appSettings, RoundSettings roundSettings)
    {
        _appSettings_ = appSettings;
        _roundSettings_ = roundSettings;

    }
    public void Suggest(
        List<Driver> drivers,
        List<DriverGrDataPoint> driverGrDataPoints,
        List<DriverFpDataPoint> driverFpDataPoints
    )
    {
        var driverValues = CalculateValues(drivers, driverGrDataPoints, driverFpDataPoints);
        // find all the combinations of 5 drivers
        // where the sum of their ExpectedPointsToGain is biggest but
        // their total value will be at most equal to value of _roundSettings_.Budget
        // order the results by combinations with most gained points being at the top
        // give me maximum of 5 options (which are all unique)
        // in each option order the drivers by the amount of points they will gain individually

        var topCombinations = GetTopDriverCombinations(driverValues.Where(x => x.IsAvailable).ToList());
        if (!topCombinations.Any())
        {
            Console.WriteLine("No combinations found!");
        }

        foreach (var combination in topCombinations)
        {
            foreach (var driver in combination)
            {
                Console.Write($"{driver.Name}({driver.PerformanceModifier:F2}) | ");
            }
            Console.WriteLine();
            Console.WriteLine($"_______{(_roundSettings_.Budget - combination.Sum(x => x.CurrentValue)):F2} mil leftover");
        }
    }

    private HashSet<HashSet<DriverValue>> GetTopDriverCombinations(List<DriverValue> drivers)
    {
        var allCombinations = GetCombinations(drivers, 5);
        var validCombinations = allCombinations
            .Where(combination => combination.Sum(driver => driver.CurrentValue) <= _roundSettings_.Budget)
            .OrderByDescending(combination => combination.Sum(driver => driver.ExpectedPointsToGain))
            .Take(_appSettings_.MaxResults)
            .Select(l => l
                .OrderByDescending(y => y.CurrentValue)
                .ToHashSet())
            .ToList()
            .ToHashSet();


        foreach (var combination in validCombinations)
        {
            combination.ToList().Sort((d1, d2) => d2.CurrentValue.CompareTo(d1.CurrentValue));
        }

        return validCombinations;
    }

    private HashSet<HashSet<DriverValue>> GetCombinations(List<DriverValue> drivers, int teamSize)
    {
        HashSet<HashSet<DriverValue>> result = new HashSet<HashSet<DriverValue>>();

        if (teamSize == 1)
        {
            foreach (var item in drivers)
            {
                result.Add(new HashSet<DriverValue> { item });
            }
        }
        else
        {
            for (int i = 0; i < drivers.Count - teamSize + 1; i++)
            {
                var head = drivers.Skip(i).Take(1).ToHashSet();
                var tailCombinations = GetCombinations(drivers.Skip(i + 1).ToList(), teamSize - 1);

                foreach (var tail in tailCombinations)
                {
                    result.Add(head.Concat(tail).ToHashSet());
                }
            }
        }

        return result;
    }

    private List<DriverValue> CalculateValues(
        List<Driver> drivers,
        List<DriverGrDataPoint> driverGrDataPoints,
        List<DriverFpDataPoint> driverFpDataPoints
    )
    {
        var driverValues = new List<DriverValue>();
        foreach (var driver in drivers)
        {
            var dgr = driverGrDataPoints.First(x => x.Name.Equals(driver.Name));
            var dfp = driverFpDataPoints.First(x => x.Name.Equals(driver.Name));
            var dv = new DriverValue();
            dv.Name = dfp.Name;
            dv.Fp1Points = GetPointsForFp(driverFpDataPoints, dfp, 0);
            dv.Fp2Points = GetPointsForFp(driverFpDataPoints, dfp, 1);
            dv.Fp3Points = GetPointsForFp(driverFpDataPoints, dfp, 2);
            dv.CurrentAvgPoints = dgr.AveragePoints;
            dv.FillInPointsForMissedFp();
            dv.FpTotalPoints = dv.Fp1Points + dv.Fp2Points + dv.Fp3Points;
            dv.CurrentValue = dgr.CurrentValue;
            dv.IsAvailable = dgr.IsAvailable;
            driverValues.Add(dv);
        }

        var mostPointsBasedOnFpData = driverValues
            .Max(dv => dv.FpTotalPoints);

        foreach (var dv in driverValues)
        {
            if (dv.Name == "SAR")
            {
                Console.WriteLine();
            }
            var performanceModifier = dv.FpTotalPoints / mostPointsBasedOnFpData;
            dv.PerformanceModifier = performanceModifier;
            // maximum amount of points a certain driver is expected to gain
            // is the average they have gained so far
            dv.ExpectedPointsToGain = dv.CurrentAvgPoints * performanceModifier;
        }

        return driverValues;
    }

    private float GetPointsForFp(
        List<DriverFpDataPoint> driverFpDataPoints,
        DriverFpDataPoint driverFpDataPoint, int fpIndex)
    {
        var hasDriverBeenInThisFp = false;
        if (driverFpDataPoint.FpData[fpIndex].LapDuration < 0)
        {
            return -1;
        }
        var rankInFp1 = driverFpDataPoints.OrderBy(d => d.FpData[fpIndex].LapDuration).ToList().IndexOf(driverFpDataPoint);
        var tyrePointsModifier = _appSettings_
            .TyrePointsMultipliers.First(x => x.Type.Equals(driverFpDataPoint.FpData[fpIndex].TyreType))
            .Multiplier;
        var points = (21 - rankInFp1) * BASE_POINTS_PER_FP * tyrePointsModifier;
        return points;
    }
}
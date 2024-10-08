using System.Data;
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
        (List<Driver>, List<Constructor>) participants,
        (List<DriverGrDataPoint> driverGrDataPoints, List<ConstructorGrDataPoint> constructorGrDataPoints) grData,
        List<DriverFpDataPoint> driverFpDataPoints
    )
    {
        var values = CalculateValues(participants, grData, driverFpDataPoints);
        // generate c# code that will find all the combinations.
        // Each combination is an object of class CombinationValue that contains Constructor (type ConstructorValue) and Drivers (type List<DriverValue>)
        // both ConstructorValue and DriverValue have a property Name, ExpectedPointsToGain, CurrentValue and PerformanceModifier
        // give me maximum of 5 CombinationValues (which are all unique)
        // where the sum of their ExpectedPointsToGain is biggest but
        // their total value will be at most equal to value of BUDGET
        // order the results by combinations with most gained points being at the top
        // in each option order the drivers by the performance amount they will gain individually
        // print out in the format that first is the ConstructorValue Name, then 5 DriverValue Names such as this:
        // Ferrari (0.85) | PER (0.9) | GAS (0.8) | ALB (0.7) | RIC (0.6) | MAX (0.5)

        var topCombinations = GetTopCombinations(
            drivers: values.driverValues.Where(x => x.IsAvailable).ToList(),
            constructors: values.constructorValues.Where(x => x.IsAvailable).ToList());
        if (topCombinations.Count == 0)
        {
            Console.WriteLine("No combinations found!");
        }

        Console.WriteLine();
        foreach (var combination in topCombinations)
        {
            var constructor = combination.Constructor;
            var driversList = combination.Drivers;
            Console.Write($"{constructor.Name} ({constructor.PerformanceModifier:F2}| {constructor.CurrentValue:F2}$)");
            foreach (var driver in driversList)
            {
                Console.Write($"|__| {driver.Name} ({driver.PerformanceModifier:F2}| {driver.CurrentValue:F2}$)");
            }
            Console.WriteLine();
            Console.WriteLine($"{_roundSettings_.Budget:F2} mil $ - " +
                              $"{(combination.Constructor.CurrentValue + combination.Drivers.Sum(x => x.CurrentValue)):F2} mil $ = " +
                              $"{(_roundSettings_.Budget - (combination.Constructor.CurrentValue + combination.Drivers.Sum(x => x.CurrentValue))):F2} mil $");
            Console.WriteLine();
        }
    }

    private HashSet<CombinationValue> GetTopCombinations(List<DriverValue> drivers, List<ConstructorValue> constructors)
    {
        var allDriverCombinations = GetCombinations(drivers, 5);
        var allCombinations = new List<CombinationValue>();
        foreach (var constructor in constructors)
        {
            foreach (var driverCombination in allDriverCombinations)
            {
                var totalValue = constructor.CurrentValue + driverCombination.Sum(driver => driver.CurrentValue);
                if (totalValue <= _roundSettings_.Budget)
                {
                    allCombinations.Add(new CombinationValue
                    {
                        Constructor = constructor,
                        Drivers = driverCombination.ToList()
                    });
                }
            }
        }
        var validCombinations = allCombinations
            .Where(combination => (combination.Constructor.CurrentValue + combination.Drivers.Sum(driver => driver.CurrentValue)) <= _roundSettings_.Budget)
            // .OrderByDescending(combination => combination.Sum(driver => driver.ExpectedPointsToGain))
            .Take(_appSettings_.MaxResults)
            // .Select(l => l
            // .OrderByDescending(y => y.CurrentValue)
            // .ToHashSet())
            // .ToList()
            .ToHashSet();


        foreach (var combination in validCombinations)
        {
            combination.Drivers = combination.Drivers.OrderByDescending(d => d.PerformanceModifier).ToList();
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

    private (List<DriverValue> driverValues, List<ConstructorValue> constructorValues) CalculateValues(
        (List<Driver> drivers, List<Constructor> constructors) participants,
        (List<DriverGrDataPoint> driverGrDataPoints, List<ConstructorGrDataPoint> constructorGrDataPoints) grData,
        List<DriverFpDataPoint> driverFpDataPoints
    )
    {
        var driverValues = new List<DriverValue>();
        SetupFpDataForDrivers(participants.drivers, grData.driverGrDataPoints, driverFpDataPoints, driverValues);
        CalculatePerformanceModifierForDrivers(driverValues);

        var constructorValues = new List<ConstructorValue>();
        SetupFpDataForConstructors(participants.drivers, participants.constructors, grData.constructorGrDataPoints, driverValues, constructorValues);
        CalculatePerformanceModifierForConstructors(constructorValues);

        return (driverValues, constructorValues);
    }
    private void CalculatePerformanceModifierForDrivers(List<DriverValue> driverValues)
    {
        var mostPointsBasedOnFpData = driverValues
            .Max(dv => dv.FpTotalPoints);

        foreach (var dv in driverValues)
        {
            var performanceModifier = dv.FpTotalPoints / mostPointsBasedOnFpData;
            dv.PerformanceModifier = performanceModifier;
            // maximum amount of points a certain driver is expected to gain
            // is the average they have gained so far
            dv.ExpectedPointsToGain = dv.CurrentAvgPointsAdjusted * performanceModifier;
        }
    }

    private void CalculatePerformanceModifierForConstructors(List<ConstructorValue> constructorValues)
    {
        var mostPointsBasedOnFpData = constructorValues
            .Max(dv => dv.DriverExpectedPointsToGain);

        foreach (var cv in constructorValues)
        {
            var performanceModifier = cv.DriverExpectedPointsToGain / mostPointsBasedOnFpData;
            cv.PerformanceModifier = performanceModifier;
            // maximum amount of points a certain driver is expected to gain
            // is the average they have gained so far
            cv.ExpectedPointsToGain = cv.CurrentAvgPointsAdjusted * performanceModifier * _appSettings_.ConstructorPerformanceImportanceMultiplier;
        }
    }

    private void SetupFpDataForConstructors(List<Driver> drivers, List<Constructor> constructors, List<ConstructorGrDataPoint> constructorGrDataPoints, List<DriverValue> driverValues, List<ConstructorValue> constructorValues)
    {
        foreach (var constructor in constructors)
        {
            var cv = new ConstructorValue();
            var cgr = constructorGrDataPoints.FirstOrDefault(x => x.Name.Equals(constructor.Name, StringComparison.OrdinalIgnoreCase));
            cv.CurrentValue = cgr.CurrentValue;
            cv.IsAvailable = cgr.IsAvailable;
            cv.Name = constructor.Name;
            cv.CurrentAvgPoints = cgr.AveragePoints;
            cv.CurrentAvgPointsAdjusted = cgr.AveragePointsAdjusted;

            var driversForTheConstructor =
                drivers
                    .Where(d => d.TeamName.Equals(constructor.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            float totalExpectedPointsToGain = driversForTheConstructor
                .Select(constructorDriver => driverValues
                    .FirstOrDefault(dv => dv.Name.Equals(constructorDriver.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(driverValue => driverValue.ExpectedPointsToGain).Sum();

            cv.DriverExpectedPointsToGain = totalExpectedPointsToGain;
            constructorValues.Add(cv);
        }
    }

    private void SetupFpDataForDrivers(List<Driver> drivers, List<DriverGrDataPoint> driverGrDataPoints, List<DriverFpDataPoint> driverFpDataPoints, List<DriverValue> driverValues)
    {
        foreach (var driver in drivers)
        {
            var dgr = driverGrDataPoints.First(x => x.Name.Equals(driver.Name, StringComparison.OrdinalIgnoreCase));
            var dfp = driverFpDataPoints.First(x => x.Name.Equals(driver.Name, StringComparison.OrdinalIgnoreCase));
            var dv = new DriverValue();
            dv.Name = dfp.Name;
            dv.Fp1Points = GetPointsForFp(driverFpDataPoints, dfp, 0);
            dv.Fp2Points = GetPointsForFp(driverFpDataPoints, dfp, 1);
            dv.Fp3Points = GetPointsForFp(driverFpDataPoints, dfp, 2);
            dv.CurrentAvgPoints = dgr.AveragePoints;
            dv.CurrentAvgPointsAdjusted = dgr.AveragePointsAdjusted;
            dv.FillInPointsForMissedFp();
            dv.FpTotalPoints = dv.Fp1Points + dv.Fp2Points + dv.Fp3Points;
            dv.CurrentValue = dgr.CurrentValue;
            dv.IsAvailable = dgr.IsAvailable;
            driverValues.Add(dv);
        }
    }

    private float GetPointsForFp(
        List<DriverFpDataPoint> driverFpDataPoints,
        DriverFpDataPoint driverFpDataPoint, int fpIndex)
    {
        if (driverFpDataPoint.FpData.Count - 1 < fpIndex || driverFpDataPoint.FpData[fpIndex].LapDuration < 0)
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
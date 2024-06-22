using System;
using System.Collections.Generic;
using System.Linq;
using FantasyF1.Helpers;
using FantasyF1.Models;

namespace FantasyF1.Services
{
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
        public void Suggest()
        {
            var driverValues = CalculateValues(_roundSettings_.DriverInputs);
            // find all the combinations of 5 drivers
            // where the sum of their ExpectedPointsToGain is biggest but
            // their total value will be at most equal to value of _roundSettings_.Budget
            // order the results by combinations with most gained points being at the top
            // give me maximum of 5 options (which are all unique)
            // in each option order the drivers by the amount of points they will gain individually

            var topCombinations = GetTopDriverCombinations(driverValues.Where(x => x.IsAvailable).ToList(), _roundSettings_.Budget, 5);

            foreach (var combination in topCombinations)
            {
                Console.WriteLine("Combination:");
                foreach (var driver in combination)
                {
                    Console.Write($"{driver.Name}({driver.PerformanceModifier:F2}) | ");
                }
                Console.WriteLine();
                Console.WriteLine($"______{combination.Sum(x => x.CurrentValue)}");
            }
        }

        private HashSet<HashSet<DriverValue>> GetTopDriverCombinations(List<DriverValue> drivers, float budget, int maxResults)
        {
            var allCombinations = GetCombinations(drivers, 5);
            var validCombinations = allCombinations
                .Where(combination => combination.Sum(driver => driver.CurrentValue) <= budget)
                .OrderByDescending(combination => combination.Sum(driver => driver.ExpectedPointsToGain))
                .Take(maxResults)
                .ToHashSet();


            foreach (var combination in validCombinations)
            {
                combination.ToList().Sort((d1, d2) => d2.ExpectedPointsToGain.CompareTo(d1.ExpectedPointsToGain));
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

        private List<DriverValue> CalculateValues(List<DriverInput> driverInputs)
        {
            var driverValues = new List<DriverValue>();
            foreach (var di in driverInputs)
            {
                var dv = new DriverValue();
                dv.Name = di.Name;
                dv.Fp1Points = GetPointsForFp(driverInputs, di, 0);
                dv.Fp2Points = GetPointsForFp(driverInputs, di, 1);
                dv.Fp3Points = GetPointsForFp(driverInputs, di, 2);
                dv.CurrentAvgPoints = di.AveragePoints;
                dv.FillInPointsForMissedFp();
                dv.CurrentValue = di.CurrentValue;
                dv.IsAvailable = di.IsAvailable;
                driverValues.Add(dv);
            }

            var mostPointsBasedOnFpData = driverValues
                .Max(dv => dv.FpTotalPoints);

            foreach (var dv in driverValues)
            {
                var performanceModifier = dv.FpTotalPoints / mostPointsBasedOnFpData;
                dv.PerformanceModifier = performanceModifier;
                // maximum amount of points a certain driver is expected to gain
                // is the average they have gained so far
                dv.ExpectedPointsToGain = dv.CurrentAvgPoints * performanceModifier;
            }

            return driverValues;
        }

        private float GetPointsForFp(List<DriverInput> driverInputs, DriverInput di, int fpIndex)
        {
            var hasDriverBeenInThisFp = false;
            if (di.FpData[fpIndex].LapDuration < 0)
            {
                return -1;
            }
            var rankInFp1 = driverInputs.OrderBy(d => d.FpData[fpIndex].LapDuration).ToList().IndexOf(di);
            var tyrePointsModifier = _appSettings_
                .TyrePointsMultipliers.First(x => x.Type.Equals(di.FpData[fpIndex].TyreType))
                .Multiplier;
            var points = (21 - rankInFp1) * BASE_POINTS_PER_FP * tyrePointsModifier;
            return points;
        }
    }
}
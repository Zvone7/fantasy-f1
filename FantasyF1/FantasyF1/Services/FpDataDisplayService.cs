using FantasyF1.Models;
using FantasyF1.Models.OpenData;

namespace FantasyF1.Services;

public class FpDataDisplayService
{
    public FpDataDisplayService()
    {

    }

    public void DisplayData(List<DriverFpDataPoint> driverFpDataPoints)
    {
        int numberOfDataPoints = driverFpDataPoints.First().FpData.Count;

        for (int i = 0; i < numberOfDataPoints; i++)
        {
            // Group the same order items from FpData list
            var groupedData = driverFpDataPoints.Select(driver => new 
                {
                    DriverName = driver.Name,
                    TyreType = driver.FpData[i].TyreType,
                    LapDuration = driver.FpData[i].LapDuration
                })
                .Where(a => a.LapDuration > 0)
                .OrderBy(data => data.LapDuration) // Order by shortest time
                .ToList();

            // Get the fastest time (first in the ordered list)
            float fastestTime = groupedData.First().LapDuration;

            // Display the data for the current group
            Console.WriteLine($"------------------ FP {i + 1} ------------------");

            int position = 1; // To track the driver's position in order
            foreach (var data in groupedData)
            {
                // Calculate the difference from the fastest time
                float timeDifference = data.LapDuration - fastestTime;

                // Format the position to ensure alignment
                string formattedPosition = position < 10 ? $"#_{position}" : $"#{position}";

                // Display the first driver with +00.00, others with the difference
                if (timeDifference == 0)
                {
                    Console.WriteLine($"{formattedPosition} {data.DriverName} | {data.LapDuration:F2} s | +0.00 s | TyreType {data.TyreType}");
                }
                else
                {
                    Console.WriteLine($"{formattedPosition} {data.DriverName} | {data.LapDuration:F2} s | +{timeDifference:F2} s | TyreType {data.TyreType}");
                }

                position++; // Increment the position for the next driver
            }
            Console.WriteLine();
        }
    }

}
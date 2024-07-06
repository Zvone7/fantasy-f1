using System.Text.Json;
using FantasyF1.Helpers;
using FantasyF1.Models;
using FantasyF1.Models.OpenData;

namespace FantasyF1.Services;

public class OpenF1DataProvider
{
    private readonly Int32 _round_;
    private readonly RoundSettings _roundSettings_;
    public OpenF1DataProvider(int round, RoundSettings roundSettings)
    {
        _round_ = round;
        _roundSettings_ = roundSettings;

    }
    public async Task<List<DriverFpDataPoint>> FillInSessionDataAsync(
        int round,
        List<Driver> drivers,
        Boolean runInCachedMode)
    {
        var driverFpDataPoints = new List<DriverFpDataPoint>();

        if (runInCachedMode)
        {
            var cachedFpDataContent = await File.ReadAllTextAsync($"CachedData{Path.DirectorySeparatorChar}r{round}_cached_fpdata.json");
            driverFpDataPoints = JsonSerializer.Deserialize<List<DriverFpDataPoint>>(cachedFpDataContent, new JsonSerializerOptions
            {
                // Converters = { new TyreTypesConverter() }
            });
        }
        else
        {
            var sessionInfos = await HttpHelper.GetAsync<List<SessionInfo>>($"sessions?year=2024&circuit_key={_roundSettings_.CircuitKey}&session_type=Practice");
            foreach (var driver in drivers)
            {
                foreach (var si in sessionInfos)
                {
                    var driverFpDataPoint = await GetDriverSessionBestLapDetailsAsync(si.session_key, driver);
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    driverFpDataPoints.Add(driverFpDataPoint);
                    Console.WriteLine($"Done for {si.session_key} {driver.Name}");
                }
            }
        }
        Console.WriteLine("Driver OpenF1Data retrieved " + (runInCachedMode ? "(fromCache)" : "(from server)"));
        var driverFpDataPointsJson = JsonSerializer.Serialize(driverFpDataPoints);
        var filePath = $"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName}{Path.DirectorySeparatorChar}CachedData{Path.DirectorySeparatorChar}r{_round_}_cached_fpdata.json";
        await File.WriteAllTextAsync(filePath, driverFpDataPointsJson);
        return driverFpDataPoints;
    }

    public async Task<DriverFpDataPoint> GetDriverSessionBestLapDetailsAsync(int sessionKey, Driver driver)
    {
        var driverFpDataPoint = new DriverFpDataPoint();
        driverFpDataPoint.Name = driver.Name;
        var laps = await HttpHelper.GetAsync<LapInfo[]>($"laps?session_key={sessionKey}&driver_number={driver.Number}&is_pit_out_lap=false");
        var fastestLap = laps
            .Where(l => l.lap_duration is not null)
            .MinBy(x => x.lap_duration);

        // if there are no laps for this driver, means he wasn't part of session
        if (fastestLap == null)
        {
            driverFpDataPoint.FpData.Add(new FpData() { LapDuration = -1, TyreType = TyreType.Soft });
        }
        else
        {
            var stintInfos = await HttpHelper.GetAsync<List<StintInfo>>($"stints?session_key={sessionKey}&driver_number={driver.Number}&lap_start<={fastestLap.lap_number}&lap_end>={fastestLap.lap_number}");
            var stintInfoToUse = stintInfos.Count != 1 ? DecideStintInfoToUse(stintInfos) : stintInfos.First();
            Enum.TryParse(stintInfoToUse.compound, ignoreCase: true, out TyreType tyreType);

            driverFpDataPoint.FpData.Add(new FpData() { LapDuration = fastestLap.lap_duration!.Value, TyreType = tyreType });
        }
        return driverFpDataPoint;
    }

    private StintInfo DecideStintInfoToUse(List<StintInfo> stintInfos)
    {
        return stintInfos.MinBy(x => x.tyre_age_at_start);
    }
}
using System.Data;
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
        Boolean forceDataRefresh)
    {
        var driverFpDataPoints = new List<DriverFpDataPoint>();
        var filePath = $"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName}{Path.DirectorySeparatorChar}CachedData{Path.DirectorySeparatorChar}r{_round_}_cached_fpdata.json";
        
        if (forceDataRefresh || !(await CachedFileHelpers.IsValidCachedFileAsync(filePath)))
        {
            Console.WriteLine("Retrieving OpenF1Data from server");
            var sessionInfos = await HttpHelper.GetAsync<List<SessionInfo>>($"sessions?year=2024&circuit_key={_roundSettings_.CircuitKey}&session_type=Practice");
            if (!sessionInfos.Any())
                throw new DataException($"No sessions found for circuit key {_roundSettings_.CircuitKey}");
            foreach (var driver in drivers)
            {
                foreach (var si in sessionInfos)
                {
                    var driverFpDataPoint = await GetDriverSessionBestLapDetailsAsync(si.session_key, driver);
                    if (driverFpDataPoint.FpData.Count == 0)
                        Console.WriteLine($"No fp data found for {driver.Name} session {si.session_key}");
                    if (driverFpDataPoint.FpData.Count > 1)
                        throw new Exception($"Impossibe FP Data case for driver {driver.Name} on session {si.session_key}: more than best time found");
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    var existing = driverFpDataPoints.FirstOrDefault(x => x.Name.Equals(driver.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                        existing.FpData.Add(driverFpDataPoint.FpData.First());
                    else
                        driverFpDataPoints.Add(driverFpDataPoint);
                    Console.WriteLine($"Done for {si.session_key} {driver.Name}");
                }
            }
            var driverFpDataPointsJson = JsonSerializer.Serialize(driverFpDataPoints);
            await File.WriteAllTextAsync(filePath, driverFpDataPointsJson);
        }
        else
        {
            var cachedFpDataContent = await File.ReadAllTextAsync(filePath);
            driverFpDataPoints = JsonSerializer.Deserialize<List<DriverFpDataPoint>>(cachedFpDataContent, new JsonSerializerOptions
            {
                // Converters = { new TyreTypesConverter() }
            });
        }
        Console.WriteLine("Driver OpenF1Data retrieved " + (forceDataRefresh ? "(from server)" : "(from cache)"));
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
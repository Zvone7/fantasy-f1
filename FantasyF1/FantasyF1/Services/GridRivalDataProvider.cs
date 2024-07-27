using System.Data;
using System.Text;
using System.Text.Json;
using FantasyF1.Helpers;
using FantasyF1.Models;
using FantasyF1.Models.GridRival;

namespace FantasyF1.Services;

public class GridRivalDataProvider
{
    private readonly AppSettings _appSettings_;
    private readonly Int32 _round_;
    private readonly RoundSettings _roundSettings_;
    private readonly GrSecrets _grSecrets_;
    public GridRivalDataProvider(AppSettings appSettings, int round, RoundSettings roundSettings, GrSecrets grSecrets)
    {
        _appSettings_ = appSettings;
        _round_ = round;
        _roundSettings_ = roundSettings;
        _grSecrets_ = grSecrets;
    }

    public async Task<(List<DriverGrDataPoint>, List<ConstructorGrDataPoint>)> FetchGrDataAsync(
        int round,
        List<Driver> drivers,
        List<Constructor> constructors,
        Boolean forceDataRefresh)
    {
        GrListResponse gpRawData = await RetrieveDataAsync(round, forceDataRefresh);

        var driverGrDataPoints = MapDriverGrDataPoints(drivers, gpRawData);
        var constructorGrDataPoints = MapConstructorGrDataPoints(constructors, gpRawData);
        AdjustGrDriverData(driverGrDataPoints);
        AdjustGrConstructorData(constructorGrDataPoints);

        foreach (var constructor in constructorGrDataPoints.Where(x => x.AveragePointsAdjusted != x.AveragePoints))
        {
            Console.WriteLine($"{constructor.Name} taking a {(constructor.AveragePoints - constructor.AveragePointsAdjusted):F2} points deduction.");
        }
        foreach (var driver in driverGrDataPoints.Where(x => x.AveragePointsAdjusted != x.AveragePoints))
        {
            Console.WriteLine($"{driver.Name} taking a {(driver.AveragePoints - driver.AveragePointsAdjusted):F2} points deduction.");
        }

        Console.WriteLine("Driver GridRivalData retrieved " + (forceDataRefresh ? "(from server)" : "(from cache)"));
        return (driverGrDataPoints, constructorGrDataPoints);
    }

    private void AdjustGrConstructorData(List<ConstructorGrDataPoint> constructorGrDataPoints)
    {
        var averageAll = constructorGrDataPoints.Average(d => d.AveragePoints);
        foreach (var constructor in constructorGrDataPoints)
        {
            var d = (float)_appSettings_.HighAveragePointsDeductionInPercentForConstructors / 100;
            var possibleAdjustedPoints = constructor.AveragePoints - d * constructor.AveragePoints;
            if (possibleAdjustedPoints > averageAll)
                constructor.AveragePointsAdjusted = (float)possibleAdjustedPoints;
            else
                constructor.AveragePointsAdjusted = constructor.AveragePoints;
        }
    }

    private void AdjustGrDriverData(List<DriverGrDataPoint> driverGrDataPoints)
    {
        var averageAll = driverGrDataPoints.Average(d => d.AveragePoints);
        foreach (var driver in driverGrDataPoints)
        {
            var d = (float)_appSettings_.HighAveragePointsDeductionInPercentForDrivers / 100;
            var possibleAdjustedPoints = driver.AveragePoints - d * driver.AveragePoints;
            if (possibleAdjustedPoints > averageAll)
                driver.AveragePointsAdjusted = (float)possibleAdjustedPoints;
            else
                driver.AveragePointsAdjusted = driver.AveragePoints;
        }
    }

    private List<ConstructorGrDataPoint> MapConstructorGrDataPoints(List<Constructor> constructors, GrListResponse gpRawData)
    {
        var constructorGrDataPoints = new List<ConstructorGrDataPoint>();
        foreach (var constructor in constructors)
        {
            var match = gpRawData.previous_elements.FirstOrDefault(pe => pe.full_name.Equals(constructor.Name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                var message = $"Cannot find gridrival data for driver {constructor.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var gpDataConstructorId = match.eid;
            if (!gpRawData.fp_by_element.ContainsKey(gpDataConstructorId))
            {
                var message = $"cannot find gridrival (fp_by_element) data for driver {constructor.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var currentValue = gpRawData.fp_by_element[gpDataConstructorId].value_previous - gpRawData.fp_by_element[gpDataConstructorId].value_flux;
            constructorGrDataPoints.Add(new ConstructorGrDataPoint()
            {
                Name = constructor.Name,
                AveragePoints = match.appr,
                CurrentValue = currentValue / 1_000_000,
                IsAvailable = !_roundSettings_.UnavailableConstructors.Contains(constructor.Name, StringComparer.OrdinalIgnoreCase)
            });
        }
        return constructorGrDataPoints;
    }

    private List<DriverGrDataPoint> MapDriverGrDataPoints(List<Driver> drivers, GrListResponse gpRawData)
    {
        var driverGrDataPoints = new List<DriverGrDataPoint>();
        foreach (var driver in drivers)
        {
            var match = gpRawData.previous_elements.FirstOrDefault(pe => pe.full_name.Equals(driver.FullName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                var message = $"Cannot find gridrival (previous_elements) data for driver {driver.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var gpDataDriverId = match.eid;
            if (!gpRawData.fp_by_element.ContainsKey(gpDataDriverId))
            {
                var message = $"cannot find gridrival (fp_by_element) data for driver {driver.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var currentValue = gpRawData.fp_by_element[gpDataDriverId].value_previous - gpRawData.fp_by_element[gpDataDriverId].value_flux;
            driverGrDataPoints.Add(new DriverGrDataPoint()
            {
                Name = driver.Name,
                AveragePoints = match.appr,
                CurrentValue = currentValue / 1_000_000,
                IsAvailable = !_roundSettings_.UnavailableDrivers.Contains(driver.Name, StringComparer.OrdinalIgnoreCase)
            });
        }
        return driverGrDataPoints;
    }

    private async Task<GrListResponse> RetrieveDataAsync(Int32 round, Boolean forceDataRefresh)
    {
        GrListResponse gpRawData;
        var filePath = $"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName}{Path.DirectorySeparatorChar}CachedData{Path.DirectorySeparatorChar}r{_round_}_cached_grdata.json";
        if (forceDataRefresh || !(await CachedFileHelpers.IsValidCachedFileAsync(filePath)))
        {
            Console.WriteLine("Retrieving OpenF1Data from server");
            var token = await GetAuthTokenAsync();
            gpRawData = await GetDriverDataAsync(token);
            var json = JsonSerializer.Serialize(gpRawData);
            await File.WriteAllTextAsync(filePath, json);
        }
        else
        {
            var cachedGrDataContent = await File.ReadAllTextAsync($"CachedData{Path.DirectorySeparatorChar}r{round}_cached_grdata.json");
            gpRawData = JsonSerializer.Deserialize<GrListResponse>(cachedGrDataContent);
        }
        return gpRawData;
    }

    private async Task<GrListResponse> GetDriverDataAsync(string authToken)
    {
        try
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri(_appSettings_.GridRivalSettings.BaseUrl);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
            client.DefaultRequestHeaders.Add("version", "3");
            var response = await client.GetAsync(_appSettings_.GridRivalSettings.StatusEndpoint);

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            var res = JsonSerializer.Deserialize<GrListResponse>(responseBody);

            return res;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    private async Task<String> GetAuthTokenAsync()
    {
        try
        {
            using var client = new HttpClient();
            var url = new Uri($"{_appSettings_.GridRivalSettings.BaseUrl}{_appSettings_.GridRivalSettings.LoginEndpoint}");

            var postContent = JsonSerializer.Serialize(
                new GrAuthPost()
                {
                    sub = _grSecrets_.Sub,
                    type = "apple",
                    user = new GrUser()
                    {
                        email = _grSecrets_.AppleEmail,
                        name = null,
                        name2 = null
                    }
                });
            var response = await client.PostAsync(url,
                new StringContent(
                    postContent,
                    Encoding.UTF8,
                    "application/json"));

            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            var res = JsonSerializer.Deserialize<GrAuthResponse>(responseBody);

            return res.token;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
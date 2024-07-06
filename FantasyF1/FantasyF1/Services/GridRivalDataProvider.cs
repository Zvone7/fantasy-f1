using System.Data;
using System.Text;
using System.Text.Json;
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
        Boolean runInCachedMode)
    {
        var driverGrDataPoints = new List<DriverGrDataPoint>();
        var constructorGrDataPoints = new List<ConstructorGrDataPoint>();
        GrListResponse gpRawData = await RetrieveDataAsync(round, runInCachedMode);
        foreach (var driver in drivers)
        {
            var match = gpRawData.previous_elements.FirstOrDefault(pe => pe.full_name.Equals(driver.FullName));
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
            var currentValue = gpRawData.fp_by_element[gpDataDriverId].value_previous-gpRawData.fp_by_element[gpDataDriverId].value_flux;
            driverGrDataPoints.Add(new DriverGrDataPoint()
            {
                Name = driver.Name,
                AveragePoints = match.appr,
                CurrentValue = currentValue / 1_000_000,
                IsAvailable = !_roundSettings_.UnavailableDrivers.Contains(driver.Name, StringComparer.OrdinalIgnoreCase)
            });
        }
        foreach (var constructor in constructors)
        {
            var match = gpRawData.previous_elements.FirstOrDefault(pe => pe.full_name.Equals(constructor.Name));
            if (match == null)
            {
                var message = $"Cannot find gridrival data for driver {constructor.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var gpDataDriverId = match.eid;
            if (!gpRawData.fp_by_element.ContainsKey(gpDataDriverId))
            {
                var message = $"cannot find gridrival (fp_by_element) data for driver {constructor.Name}";
                Console.WriteLine(message);
                throw new DataException(message);
            }
            var currentValue = gpRawData.fp_by_element[gpDataDriverId].value_previous-gpRawData.fp_by_element[gpDataDriverId].value_flux;
            constructorGrDataPoints.Add(new ConstructorGrDataPoint()
            {
                Name = constructor.Name,
                AveragePoints = match.appr,
                CurrentValue = match.value / 1_000_000,
                IsAvailable = !_roundSettings_.UnavailableConstructors.Contains(constructor.Name, StringComparer.OrdinalIgnoreCase)
            });
        }

        Console.WriteLine("Driver GridRivalData retrieved " + (runInCachedMode ? "(fromCache)" : "(from server)"));
        return (driverGrDataPoints, constructorGrDataPoints);
    }
    private async Task<GrListResponse> RetrieveDataAsync(Int32 round, Boolean runInCachedMode)
    {

        GrListResponse gpRawData;
        if (runInCachedMode)
        {
            var cachedGrDataContent = await File.ReadAllTextAsync($"CachedData{Path.DirectorySeparatorChar}r{round}_cached_grdata.json");
            gpRawData = JsonSerializer.Deserialize<GrListResponse>(cachedGrDataContent);
        }
        else
        {
            var token = await GetAuthTokenAsync();
            gpRawData = await GetDriverDataAsync(token);
            var json = JsonSerializer.Serialize(gpRawData);
            var filePath = $"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName}{Path.DirectorySeparatorChar}CachedData{Path.DirectorySeparatorChar}r{_round_}_cached_grdata.json";
            await File.WriteAllTextAsync(filePath, json);
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
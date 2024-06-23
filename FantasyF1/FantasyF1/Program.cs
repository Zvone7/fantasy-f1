using System.Text.Json;
using FantasyF1.Models;
using FantasyF1.Models.GridRival;
using FantasyF1.Services;

namespace FantasyF1;

class Program
{
    static async Task Main(string[] args)
    {
        const int round = 10;
        const Boolean runInCachedMode = true;

        var options = new JsonSerializerOptions
        {
            Converters = { new TyreTypesConverter() }
        };
        var appSettingsContent = await File.ReadAllTextAsync($"appsettings.json");
        var appSettings = JsonSerializer.Deserialize<AppSettings>(appSettingsContent, options);

        var roundSettingsContent = await File.ReadAllTextAsync($"RoundSettings{Path.DirectorySeparatorChar}r{round}.json");
        var roundSettings = JsonSerializer.Deserialize<RoundSettings>(roundSettingsContent);

        var gridRivalSecretsContent = await File.ReadAllTextAsync($"gridrivalsecrets.json");
        var gridRivalSecrets = JsonSerializer.Deserialize<GrSecrets>(gridRivalSecretsContent);

        var drivers = appSettings.DriverInformation;
        var constructors = appSettings.ConstructorInformation;

        var gridRivalDataProvider = new GridRivalDataProvider(appSettings, roundSettings, gridRivalSecrets);
        var grData = await gridRivalDataProvider.FetchGrDataAsync(round, drivers, constructors, runInCachedMode);

        var f1DataProvider = new OpenF1DataProvider(roundSettings);
        var driverFpDataPoints = await f1DataProvider.FillInSessionDataAsync(round, drivers, runInCachedMode);

        var lineupSuggestor = new LineupSuggestor(appSettings, roundSettings);
        lineupSuggestor.Suggest((drivers, constructors), grData, driverFpDataPoints);
    }
}
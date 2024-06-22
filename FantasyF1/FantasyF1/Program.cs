using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FantasyF1.Models;
using FantasyF1.Services;

namespace FantasyF1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new TyreTypesConverter() }
            };
            var appSettingsContent = await File.ReadAllTextAsync($"appsettings.json");
            var appSettings = JsonSerializer.Deserialize<AppSettings>(appSettingsContent, options);
            var roundSettingsFilePath = $"RoundSettings{Path.DirectorySeparatorChar}r11.json";
            var roundSettingsContent = await File.ReadAllTextAsync(roundSettingsFilePath);
            var roundSettings = JsonSerializer.Deserialize<RoundSettings>(roundSettingsContent, options);

            var f1DataProvider = new OpenF1DataProvider(appSettings, roundSettings);
            // await f1DataProvider.FillInSessionData();
            ////// var driverInputsJson = JsonSerializer.Serialize(roundSettings.DriverInputs);
            // comment out to not spam the server - temp debug in r11


            var lineupSuggestor = new LineupSuggestor(appSettings, roundSettings);
            lineupSuggestor.Suggest();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FantasyF1.Helpers;
using FantasyF1.Models;
using FantasyF1.Models.OpenData;

namespace FantasyF1.Services
{
    public class OpenF1DataProvider
    {
        private readonly AppSettings _appSettings_;
        private readonly RoundSettings _roundSettings_;
        public OpenF1DataProvider(AppSettings appSettings, RoundSettings roundSettings)
        {
            _appSettings_ = appSettings;
            _roundSettings_ = roundSettings;

        }
        public async Task FillInSessionData()
        {
            var sessionInfos = await HttpHelper.GetAsync<List<SessionInfo>>($"sessions?year=2024&circuit_key={_roundSettings_.CircuitKey}&session_type=Practice");
            foreach (var di in _roundSettings_.DriverInputs)
            {
                foreach (var si in sessionInfos)
                {
                    var driverData = _appSettings_.DriverInformation.First(d => d.Name.Equals(di.Name, StringComparison.OrdinalIgnoreCase));
                    await GetDriverSessionBestLapDetails(si.session_key, driverData.Number, di);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    Console.WriteLine($"Done for {si.session_key} {di.Name}");
                    var resDi = new DriverInput();

                }
            }
        }

        public async Task GetDriverSessionBestLapDetails(int sessionKey, int driverNumber, DriverInput driverInput)
        {
            var laps = await HttpHelper.GetAsync<LapInfo[]>($"laps?session_key={sessionKey}&driver_number={driverNumber}&is_pit_out_lap=false");
            var fastestLap = laps
                .Where(l => l.lap_duration is not null)
                .MinBy(x => x.lap_duration);

            // if there are no laps for this driver, means he wasn't part of session
            if (fastestLap == null)
            {
                driverInput.FpData.Add(new FpData() { LapDuration = -1, TyreType = TyreType.Soft });
            }

            else
            {
                var stintInfos = await HttpHelper.GetAsync<List<StintInfo>>($"stints?session_key={sessionKey}&driver_number={driverNumber}&lap_start<={fastestLap.lap_number}&lap_end>={fastestLap.lap_number}");
                StintInfo stintInfoToUse = stintInfos.Count != 1 ? DecideStintInfoToUse(stintInfos) : stintInfos.First();
                TyreType.TryParse(stintInfoToUse.compound, ignoreCase: true, out TyreType tyreType);

                driverInput.FpData.Add(new FpData() { LapDuration = fastestLap.lap_duration!.Value, TyreType = tyreType });
            }
        }

        private StintInfo DecideStintInfoToUse(List<StintInfo> stintInfos)
        {
            return stintInfos.OrderBy(x => x.tyre_age_at_start)
                .FirstOrDefault();
        }
    }
}
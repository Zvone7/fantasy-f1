using System.Data;
using FantasyF1.Models;

namespace FantasyF1.Helpers;

public static class DriverValueExtensions
{
    public static void FillInPointsForMissedFp(this DriverValue dv)
    {
        if (dv.Fp1Points < 0 && dv.Fp2Points < 0 && dv.Fp3Points < 0)
            throw new DataException($"Impossible case: Driver {dv.Name} needs to be in an at least one FP to drive a race");
        if (dv.Fp1Points > 0 && dv.Fp2Points > 0 && dv.Fp3Points > 0)
            return;
        var fpsToCount = 0;
        float fpPointSum = 0;
        if (dv.Fp1Points > 0)
        {
            fpsToCount++;
            fpPointSum += dv.Fp1Points;
        }
        if (dv.Fp2Points > 0)
        {
            fpsToCount++;
            fpPointSum += dv.Fp2Points;
        }
        if (dv.Fp3Points > 0)
        {
            fpsToCount++;
            fpPointSum += dv.Fp3Points;
        }
        if (fpsToCount < 3)
        {
            var avgFpPoints = fpPointSum / fpsToCount;
            if (dv.Fp1Points < 0) dv.Fp1Points = avgFpPoints;
            if (dv.Fp2Points < 0) dv.Fp2Points = avgFpPoints;
            if (dv.Fp3Points < 0) dv.Fp3Points = avgFpPoints;
        }
    }
}
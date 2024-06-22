using System;

namespace FantasyF1.Models
{
    public class DriverValue
    {
        public string Name { get; set; }
        public Boolean IsAvailable { get; set; }
        public float CurrentAvgPoints { get; set; }
        public float CurrentValue { get; set; }
        public float ExpectedPointsToGain { get; set; }
        public float Fp1Points { get; set; }
        public float Fp2Points { get; set; }
        public float Fp3Points { get; set; }
        public float FpTotalPoints { get; set; }
        public float PerformanceModifier { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace FantasyF1.Models
{
    
    public class DriverInput
    {
        public String Name { get; set; }
        public int AveragePoints { get; set; }
        public float CurrentValue { get; set; }
        public bool IsAvailable { get; set; }
        public List<FpData> FpData { get; set; } = new List<FpData>();
    }

}
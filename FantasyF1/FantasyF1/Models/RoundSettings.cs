using System.Collections.Generic;

namespace FantasyF1.Models
{
    public class RoundSettings
    {
        public int Round { get; set; }
        public float Budget { get; set; }
        public string CircuitName { get; set; }
        public string CircuitShortName { get; set; }
        public int CircuitKey { get; set; }
        public List<DriverInput> DriverInputs { get; set; }
    }
}
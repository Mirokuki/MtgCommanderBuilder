using System.Collections.Generic;

namespace MtgCommanderBuilder.Models
{
    public class CustomStaplesTab
    {
        public string Name { get; set; } = string.Empty;
        public List<string> CardNames { get; set; } = new();
    }
}

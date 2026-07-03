using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MtgCommanderBuilder.Models
{
    public class Deck : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = "New Commander Deck";

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("commander")]
        public Card? Commander { get; set; }

        [JsonPropertyName("cards")]
        public List<Card> Cards { get; set; } = new();

        [JsonPropertyName("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [JsonPropertyName("last_modified_date")]
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;

        [JsonIgnore]
        public int DeckSize => (Commander != null ? 1 : 0) + Cards.Sum(c => c.Quantity);

        [JsonIgnore]
        public List<string> ColorIdentity
        {
            get
            {
                if (Commander == null) return new List<string>();
                return Commander.ColorIdentity;
            }
        }

        [JsonIgnore]
        public double AverageManaValue
        {
            get
            {
                var nonLandCards = Cards.Where(c => !c.IsLand).ToList();
                if (Commander != null && !Commander.IsLand)
                {
                    nonLandCards.Add(Commander);
                }
                int totalCount = nonLandCards.Sum(c => c.Quantity);
                if (totalCount == 0) return 0;
                double totalCmc = nonLandCards.Sum(c => c.Cmc * c.Quantity);
                return Math.Round(totalCmc / totalCount, 2);
            }
        }
    }
}

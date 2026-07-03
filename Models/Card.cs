using System.Text.Json.Serialization;

namespace MtgCommanderBuilder.Models
{
    public class Card
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mana_cost")]
        public string? ManaCost { get; set; }

        [JsonPropertyName("cmc")]
        public double Cmc { get; set; }

        [JsonPropertyName("type_line")]
        public string TypeLine { get; set; } = string.Empty;

        [JsonPropertyName("oracle_text")]
        public string? OracleText { get; set; }

        private List<string> _colors = new();
        private List<string> _colorIdentity = new();

        [JsonPropertyName("colors")]
        public List<string> Colors 
        { 
            get => _colors ??= new List<string>(); 
            set => _colors = value ?? new List<string>(); 
        }

        [JsonPropertyName("color_identity")]
        public List<string> ColorIdentity 
        { 
            get => _colorIdentity ??= new List<string>(); 
            set => _colorIdentity = value ?? new List<string>(); 
        }

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "common";

        [JsonPropertyName("edhrec_rank")]
        public int? EdhrecRank { get; set; }

        [JsonPropertyName("set")]
        public string Set { get; set; } = string.Empty;

        [JsonPropertyName("set_name")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("collector_number")]
        public string CollectorNumber { get; set; } = string.Empty;

        // Local computed fields or flattened properties
        public string NormalImageUrl { get; set; } = string.Empty;
        public string ArtCropImageUrl { get; set; } = string.Empty;
        public string? PriceUsd { get; set; }

        // UI-only properties (ignored during deck save/load or marked as non-persistent)
        [JsonIgnore]
        public bool IsCommander { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        private List<string> _categories = new();
        [JsonPropertyName("categories")]
        public List<string> Categories
        {
            get => _categories ??= new List<string>();
            set => _categories = value ?? new List<string>();
        }

        // Custom helpers
        [JsonIgnore]
        public bool IsCreature => TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsLand => TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsInstant => TypeLine.Contains("Instant", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsSorcery => TypeLine.Contains("Sorcery", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsArtifact => TypeLine.Contains("Artifact", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsEnchantment => TypeLine.Contains("Enchantment", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore]
        public bool IsPlaneswalker => TypeLine.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public string PrimaryType
        {
            get
            {
                if (IsCreature) return "Creature";
                if (IsPlaneswalker) return "Planeswalker";
                if (IsInstant) return "Instant";
                if (IsSorcery) return "Sorcery";
                if (IsEnchantment) return "Enchantment";
                if (IsArtifact) return "Artifact";
                if (IsLand) return "Land";
                return "Other";
            }
        }

        [JsonIgnore]
        public string ColorsString => Colors.Count > 0 ? string.Join("", Colors) : "C";

        [JsonIgnore]
        public string SetDisplay => Set?.ToUpper() ?? string.Empty;

        [JsonIgnore]
        public string PriceDisplay => !string.IsNullOrEmpty(PriceUsd) ? $"${PriceUsd}" : "N/A";

        [JsonIgnore]
        public bool IsValidCommander
        {
            get
            {
                bool isEligible = (!string.IsNullOrEmpty(TypeLine) && TypeLine.Contains("Legendary", StringComparison.OrdinalIgnoreCase) && TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase)) ||
                                  (OracleText != null && OracleText.Contains("can be your commander", StringComparison.OrdinalIgnoreCase));
                return isEligible;
            }
        }

        [JsonIgnore]
        public int ColorSortIndex
        {
            get
            {
                if (IsLand) return 8;
                if (Colors == null || Colors.Count == 0) return 7;
                if (Colors.Count > 1) return 6;
                return Colors[0] switch
                {
                    "W" => 1,
                    "U" => 2,
                    "B" => 3,
                    "R" => 4,
                    "G" => 5,
                    _ => 7
                };
            }
        }

        [JsonIgnore]
        public string ColorSortString => Colors != null ? string.Join("", Colors.OrderBy(c => c)) : "";

        [JsonIgnore]
        public int RaritySortIndex
        {
            get
            {
                if (string.IsNullOrEmpty(Rarity)) return 4;
                return Rarity.ToLower() switch
                {
                    "mythic" => 0,
                    "rare" => 1,
                    "uncommon" => 2,
                    "common" => 3,
                    _ => 4
                };
            }
        }

        [JsonIgnore]
        public double PriceUsdValue
        {
            get
            {
                if (double.TryParse(PriceUsd, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
                return -1.0;
            }
        }

        [JsonIgnore]
        public double CollectorNumberValue
        {
            get
            {
                if (string.IsNullOrEmpty(CollectorNumber)) return double.MaxValue;
                int idx = 0;
                while (idx < CollectorNumber.Length && char.IsDigit(CollectorNumber[idx]))
                {
                    idx++;
                }
                if (idx > 0 && double.TryParse(CollectorNumber.Substring(0, idx), out double val))
                {
                    return val;
                }
                return double.MaxValue;
            }
        }
    }

    // Helper classes to deserialize directly from Scryfall bulk JSON
    public class ScryfallImageUris
    {
        [JsonPropertyName("normal")]
        public string Normal { get; set; } = string.Empty;

        [JsonPropertyName("large")]
        public string Large { get; set; } = string.Empty;

        [JsonPropertyName("png")]
        public string Png { get; set; } = string.Empty;

        [JsonPropertyName("art_crop")]
        public string ArtCrop { get; set; } = string.Empty;
    }

    public class ScryfallPrices
    {
        [JsonPropertyName("usd")]
        public string? Usd { get; set; }
    }

    public class ScryfallCardDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("mana_cost")]
        public string? ManaCost { get; set; }

        [JsonPropertyName("cmc")]
        public double Cmc { get; set; }

        [JsonPropertyName("type_line")]
        public string TypeLine { get; set; } = string.Empty;

        [JsonPropertyName("oracle_text")]
        public string? OracleText { get; set; }

        [JsonPropertyName("colors")]
        public List<string>? Colors { get; set; }

        [JsonPropertyName("color_identity")]
        public List<string>? ColorIdentity { get; set; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "common";

        [JsonPropertyName("edhrec_rank")]
        public int? EdhrecRank { get; set; }

        [JsonPropertyName("set")]
        public string Set { get; set; } = string.Empty;

        [JsonPropertyName("set_name")]
        public string SetName { get; set; } = string.Empty;

        [JsonPropertyName("collector_number")]
        public string CollectorNumber { get; set; } = string.Empty;

        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }

        [JsonPropertyName("prices")]
        public ScryfallPrices? Prices { get; set; }

        [JsonPropertyName("card_faces")]
        public List<ScryfallCardFaceDto>? CardFaces { get; set; }

        public Card ToCard()
        {
            string normalImg = ImageUris?.Large ?? string.Empty;
            if (string.IsNullOrEmpty(normalImg)) normalImg = ImageUris?.Normal ?? string.Empty;
            if (string.IsNullOrEmpty(normalImg)) normalImg = ImageUris?.Png ?? string.Empty;

            string artCropImg = ImageUris?.ArtCrop ?? string.Empty;

            if (string.IsNullOrEmpty(normalImg) && CardFaces != null && CardFaces.Count > 0)
            {
                normalImg = CardFaces[0].ImageUris?.Large ?? string.Empty;
                if (string.IsNullOrEmpty(normalImg)) normalImg = CardFaces[0].ImageUris?.Normal ?? string.Empty;
                if (string.IsNullOrEmpty(normalImg)) normalImg = CardFaces[0].ImageUris?.Png ?? string.Empty;
                
                artCropImg = CardFaces[0].ImageUris?.ArtCrop ?? string.Empty;
            }

            return new Card
            {
                Id = Id,
                Name = Name,
                ManaCost = ManaCost,
                Cmc = Cmc,
                TypeLine = TypeLine,
                OracleText = OracleText,
                Colors = Colors ?? new List<string>(),
                ColorIdentity = ColorIdentity ?? new List<string>(),
                Rarity = Rarity,
                EdhrecRank = EdhrecRank,
                Set = Set,
                SetName = SetName,
                CollectorNumber = CollectorNumber,
                NormalImageUrl = normalImg,
                ArtCropImageUrl = artCropImg,
                PriceUsd = Prices?.Usd,
                Categories = new List<string>()
            };
        }
    }

    public class ScryfallCardFaceDto
    {
        [JsonPropertyName("image_uris")]
        public ScryfallImageUris? ImageUris { get; set; }
    }
}

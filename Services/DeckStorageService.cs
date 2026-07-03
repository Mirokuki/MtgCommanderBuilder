using System.IO;
using System.Text.Json;
using MtgCommanderBuilder.Models;

namespace MtgCommanderBuilder.Services
{
    public class DeckStorageService
    {
        private readonly string _decksDir;
        private readonly string _settingsPath;

        public DeckStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _decksDir = Path.Combine(appData, "MtgCommanderBuilder", "Decks");
            Directory.CreateDirectory(_decksDir);
            _settingsPath = Path.Combine(appData, "MtgCommanderBuilder", "settings.json");
        }

        public string GetDecksDirectory() => _decksDir;

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings { UseFullDatabase = false, IsGridViewActive = false };
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Ignore
            }
        }

        public List<Deck> GetAllDecks()
        {
            var decks = new List<Deck>();
            if (!Directory.Exists(_decksDir)) return decks;

            var files = Directory.GetFiles(_decksDir, "*.json");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var deck = JsonSerializer.Deserialize<Deck>(json);
                    if (deck != null)
                    {
                        if (deck.Commander != null)
                        {
                            deck.Commander.IsCommander = true;
                        }
                        decks.Add(deck);
                    }
                }
                catch
                {
                    // Ignore corrupted files for listing
                }
            }

            return decks.OrderByDescending(d => d.LastModifiedDate).ToList();
        }

        public void SaveDeck(Deck deck)
        {
            deck.LastModifiedDate = DateTime.Now;
            var filePath = Path.Combine(_decksDir, $"{deck.Id}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(deck, options);
            File.WriteAllText(filePath, json);
        }

        public Deck? LoadDeck(Guid id)
        {
            var filePath = Path.Combine(_decksDir, $"{id}.json");
            if (!File.Exists(filePath)) return null;

            try
            {
                var json = File.ReadAllText(filePath);
                var deck = JsonSerializer.Deserialize<Deck>(json);
                if (deck?.Commander != null)
                {
                    deck.Commander.IsCommander = true;
                }
                return deck;
            }
            catch
            {
                return null;
            }
        }

        public void DeleteDeck(Guid id)
        {
            var filePath = Path.Combine(_decksDir, $"{id}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    public class AppSettings
    {
        public bool UseFullDatabase { get; set; }
        public bool IsGridViewActive { get; set; }
        public double CardGridScale { get; set; } = 1.0;
        public bool SearchAffectsStaples { get; set; }
        public List<CustomStaplesTab> CustomStaplesTabs { get; set; } = new();

        // New UI settings properties
        public string DefaultFormat { get; set; } = "Commander (EDH)";
        public int DefaultDeckSize { get; set; } = 100;
        public int DefaultCommanderTax { get; set; } = 2;
        public bool ConfirmCardRemovals { get; set; } = true;
        public bool WarnOffColorCards { get; set; } = true;
        public bool EnableAdvancedWarnings { get; set; } = true;
        public bool ShowCardRoles { get; set; } = true;
        public bool AutoSortDeck { get; set; } = false;
        public string Currency { get; set; } = "USD ($)";
        public string Language { get; set; } = "English";
        public string DeckListLayoutMode { get; set; } = "Spreadsheet Grid";
    }
}

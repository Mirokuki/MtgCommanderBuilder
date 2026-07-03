using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MtgCommanderBuilder.Models;

namespace MtgCommanderBuilder.Services
{
    public class DatabaseService
    {
        private readonly string _dataPath;
        private readonly HttpClient _httpClient;
        private bool _useFullDatabase;

        public bool UseFullDatabase
        {
            get => _useFullDatabase;
            set
            {
                if (_useFullDatabase != value)
                {
                    _useFullDatabase = value;
                    IsLoaded = false;
                }
            }
        }

        public string DbFilePath => Path.Combine(_dataPath, UseFullDatabase ? "default-cards.json" : "oracle-cards.json");
        
        public List<Card> Cards { get; private set; } = new();
        public bool IsLoaded { get; private set; }
        public bool IsDatabaseDownloaded => File.Exists(DbFilePath) && new FileInfo(DbFilePath).Length > 10 * 1024 * 1024; // > 10MB

        public event Action<double>? DownloadProgressChanged;
        public event Action<string>? StatusTextChanged;

        public DatabaseService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _dataPath = Path.Combine(appData, "MtgCommanderBuilder", "Data");
            Directory.CreateDirectory(_dataPath);

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MtgCommanderBuilder/1.0 (local desktop deck builder; rsuff)");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            _httpClient.Timeout = TimeSpan.FromMinutes(10); // JSON file can take time to download
        }

        public string GetDbFilePath() => DbFilePath;

        public async Task DownloadDatabaseAsync()
        {
            StatusTextChanged?.Invoke("Querying Scryfall bulk-data registry...");
            
            // 1. Fetch bulk-data catalog from Scryfall
            var catalogResponse = await _httpClient.GetStringAsync("https://api.scryfall.com/bulk-data");
            using var doc = JsonDocument.Parse(catalogResponse);
            var root = doc.RootElement;
            var dataArray = root.GetProperty("data");

            string? downloadUri = null;
            string targetType = UseFullDatabase ? "default_cards" : "oracle_cards";
            foreach (var item in dataArray.EnumerateArray())
            {
                if (item.GetProperty("type").GetString() == targetType)
                {
                    downloadUri = item.GetProperty("download_uri").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUri))
            {
                throw new Exception($"Could not find {targetType} download URI in Scryfall bulk catalog.");
            }

            StatusTextChanged?.Invoke("Connecting to Scryfall data mirror...");

            // 2. Download the bulk data file with progress tracking
            using var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(DbFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            var totalReadBytes = 0L;
            var bytesRead = 0;

            StatusTextChanged?.Invoke("Downloading MTG Card Database...");
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalReadBytes += bytesRead;

                if (totalBytes != -1)
                {
                    var progress = (double)totalReadBytes / totalBytes * 100.0;
                    DownloadProgressChanged?.Invoke(progress);
                }
            }

            StatusTextChanged?.Invoke("Database download complete!");
        }

        public async Task InitializeDatabaseAsync()
        {
            if (!IsDatabaseDownloaded)
            {
                throw new FileNotFoundException("Local MTG Card database file was not found. Please download it first.", DbFilePath);
            }

            IsLoaded = false;
            StatusTextChanged?.Invoke("Parsing MTG database file...");
            
            await Task.Run(() =>
            {
                try
                {
                    using var stream = new FileStream(DbFilePath, FileMode.Open, FileAccess.Read);
                    var dtos = JsonSerializer.Deserialize<List<ScryfallCardDto>>(stream);

                    if (dtos != null)
                    {
                        StatusTextChanged?.Invoke("Indexing cards in memory...");
                        
                        // Convert DTOs to Cards and sort them by EDHREC Rank (null ranks pushed to the end)
                        var list = dtos.Select(dto => dto.ToCard()).ToList();
                        
                        // Deduplicate or pre-filter if needed, but Oracle Cards dataset contains unique names already.
                        Cards = list.OrderBy(c => c.EdhrecRank ?? int.MaxValue)
                                    .ThenBy(c => c.Name)
                                    .ToList();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error parsing local JSON database: " + ex.Message, ex);
                }
            });

            IsLoaded = true;
            StatusTextChanged?.Invoke($"Database loaded! {Cards.Count:N0} cards ready.");
        }
    }
}

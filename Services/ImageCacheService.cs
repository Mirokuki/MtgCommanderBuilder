using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MtgCommanderBuilder.Services
{
    public class ImageCacheService
    {
        private readonly string _cacheDir;
        private readonly HttpClient _httpClient;

        public ImageCacheService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheDir = Path.Combine(appData, "MtgCommanderBuilder", "Cache", "Images");
            Directory.CreateDirectory(_cacheDir);

            _httpClient = new HttpClient();
            // Scryfall API requires a custom User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MtgCommanderBuilder/1.0 (local desktop deck builder; rsuff)");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        }

        public async Task<string> GetImageAsync(string cardId, string remoteUrl)
        {
            if (string.IsNullOrEmpty(remoteUrl)) return string.Empty;

            var localPath = Path.Combine(_cacheDir, $"{cardId}_large.jpg");

            // If it exists in cache, return it immediately
            if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
            {
                return localPath;
            }

            try
            {
                // Download and cache the image asynchronously
                var bytes = await _httpClient.GetByteArrayAsync(remoteUrl);
                await File.WriteAllBytesAsync(localPath, bytes);
                return localPath;
            }
            catch
            {
                // Return remote url as fallback if download fails (so user still sees card if online but cache write failed)
                return remoteUrl;
            }
        }
    }
}

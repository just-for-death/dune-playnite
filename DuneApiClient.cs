using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using Newtonsoft.Json;
using Playnite.SDK;

namespace DunePlayniteAddon
{
    public class DuneApiClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly HttpClient client;
        private readonly string baseUrl;

        public DuneApiClient(string baseUrl)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.client = new HttpClient();
        }

        public async Task<bool> UploadSaves(string gameName, string localPath)
        {
            try
            {
                logger.Info($"Uploading saves for {gameName} from {localPath}");
                if (!Directory.Exists(localPath)) return false;

                // Create a temporary zip file
                string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
                ZipFile.CreateFromDirectory(localPath, zipPath);

                using (var content = new MultipartFormDataContent())
                {
                    var fileStream = File.OpenRead(zipPath);
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.Add("Content-Type", "application/octet-stream");
                    streamContent.Headers.Add("x-game", gameName);
                    streamContent.Headers.Add("x-path", "saves.zip");
                    streamContent.Headers.Add("x-sync-id", DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
                    
                    content.Add(streamContent, "file", "saves.zip");

                    var response = await client.PostAsync($"{baseUrl}/api/upload-file", streamContent);
                    
                    fileStream.Close();
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        logger.Error($"Server rejected upload: {error}");
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to upload saves for {gameName}");
                return false;
            }
        }

        public async Task<bool> DownloadSaves(string gameName, string localPath)
        {
            try
            {
                logger.Info($"Downloading saves for {gameName} to {localPath}");
                
                var response = await client.GetAsync($"{baseUrl}/api/download-file?game={Uri.EscapeDataString(gameName)}&path=saves.zip");
                if (!response.IsSuccessStatusCode) return false;

                byte[] data = await response.Content.ReadAsByteArrayAsync();
                string zipPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");
                File.WriteAllBytes(zipPath, data);

                if (!Directory.Exists(localPath)) Directory.CreateDirectory(localPath);
                ZipFile.ExtractToDirectory(zipPath, localPath);
                
                File.Delete(zipPath);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to download saves for {gameName}");
                return false;
            }
        }

        public async Task<bool> TestConnection()
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}/api/saves");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;

namespace DunePlayniteAddon
{
    public class DuneApiClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        // Static singleton HttpClient: avoids socket exhaustion that occurs when
        // constructing a new HttpClient per request / per plugin instance.
        // Timeout covers normal LAN uploads; increase if saves are very large.
        private static readonly HttpClient client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly string baseUrl;

        public DuneApiClient(string baseUrl)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Zips <paramref name="localPath"/> and streams it to the server.
        /// Bug fix: previously the MultipartFormDataContent was built but the raw
        /// StreamContent was sent — losing multipart boundaries AND setting custom
        /// headers on the content object (not the HTTP request) where the server
        /// could not read them. Now uses HttpRequestMessage so x-game / x-path /
        /// x-sync-id arrive as proper HTTP headers.
        /// </summary>
        public async Task<bool> UploadSaves(
            string gameName, string localPath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Info($"[Dune] Uploading saves for '{gameName}' from '{localPath}'");

                if (!Directory.Exists(localPath))
                {
                    logger.Warn($"[Dune] Save path missing: {localPath}");
                    return false;
                }

                string zipPath = Path.Combine(Path.GetTempPath(), $"dune_{Guid.NewGuid():N}.zip");
                try
                {
                    ZipFile.CreateFromDirectory(localPath, zipPath);

                    using (var fileStream = File.OpenRead(zipPath))
                    using (var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/upload-file"))
                    {
                        var streamContent = new StreamContent(fileStream);
                        streamContent.Headers.ContentType =
                            new MediaTypeHeaderValue("application/octet-stream");

                        // Fix: set as HTTP request headers (req.headers on server),
                        // not as StreamContent content-headers.
                        request.Headers.Add("x-game", gameName);
                        request.Headers.Add("x-path", "saves.zip");
                        request.Headers.Add("x-sync-id",
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                        request.Content = streamContent;

                        var response = await client.SendAsync(request, cancellationToken)
                            .ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            string error = await response.Content.ReadAsStringAsync()
                                .ConfigureAwait(false);
                            logger.Error($"[Dune] Server rejected upload for '{gameName}': {error}");
                            return false;
                        }
                    }

                    logger.Info($"[Dune] Upload successful for '{gameName}'.");
                    return true;
                }
                finally
                {
                    // Always clean up temp zip even on failure
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info($"[Dune] Upload cancelled for '{gameName}'.");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"[Dune] Upload failed for '{gameName}'.");
                return false;
            }
        }

        /// <summary>
        /// Downloads the saves zip for a game and extracts it to <paramref name="localPath"/>.
        /// Bug fix: ExtractToDirectory without the overwriteFiles:true overload throws
        /// InvalidOperationException when files already exist, making every re-download fail.
        /// </summary>
        public async Task<bool> DownloadSaves(
            string gameName, string localPath,
            CancellationToken cancellationToken = default)
        {
            try
            {
                logger.Info($"[Dune] Downloading saves for '{gameName}' to '{localPath}'");

                var response = await client.GetAsync(
                    $"{baseUrl}/api/download-file?game={Uri.EscapeDataString(gameName)}&path=saves.zip",
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    logger.Error($"[Dune] Server returned {(int)response.StatusCode} for download of '{gameName}'.");
                    return false;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                string zipPath = Path.Combine(Path.GetTempPath(), $"dune_{Guid.NewGuid():N}.zip");
                try
                {
                    File.WriteAllBytes(zipPath, data);

                    if (!Directory.Exists(localPath))
                        Directory.CreateDirectory(localPath);

                    // .NET Framework 4.6.2 does not natively support overwriteFiles:true overload safely.
                    // Instead, we manually iterate and extract to ensure compatibility and safety.
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories
                            string destinationPath = Path.GetFullPath(Path.Combine(localPath, entry.FullName));
                            if (destinationPath.StartsWith(Path.GetFullPath(localPath), StringComparison.OrdinalIgnoreCase))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }
                        }
                    }

                    logger.Info($"[Dune] Download and extraction successful for '{gameName}'.");
                    return true;
                }
                finally
                {
                    try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info($"[Dune] Download cancelled for '{gameName}'.");
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"[Dune] Download failed for '{gameName}'.");
                return false;
            }
        }

        /// <summary>Checks reachability of the Dune server.</summary>
        public async Task<bool> TestConnection(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}/api/saves", cancellationToken)
                    .ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

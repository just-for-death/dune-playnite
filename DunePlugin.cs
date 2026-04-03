using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace DunePlayniteAddon
{
    public class DunePlugin : GenericPlugin, IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private DuneSettingsViewModel settings;
        private DuneScanner scanner;

        // Used to cancel in-flight syncs on plugin shutdown.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public override Guid Id { get; } = Guid.Parse("d9e27c94-eba8-46f1-b4ea-6c444cc0500e");

        // Always construct from current settings so URL changes take effect immediately
        // without requiring a Playnite restart. DuneApiClient is cheap to construct
        // since HttpClient is a static singleton inside it.
        private DuneApiClient ApiClient => new DuneApiClient(settings.Settings.ServerUrl);

        public DunePlugin(IPlayniteAPI api) : base(api)
        {
            settings = new DuneSettingsViewModel(this);
            scanner = new DuneScanner();
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override ISettings GetSettings(bool firstRunSettings) => settings;

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"[Dune] Plugin started. Server: {settings.Settings.ServerUrl}");
        }

        /// <summary>
        /// Pulls the latest saves from the server before the game launches.
        /// Only active when SyncOnGameStart is enabled. Runs silently — does
        /// not block or cancel the launch on failure.
        /// </summary>
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (settings.Settings.SyncOnGameStart)
            {
                _ = PullSavesAsync(args.Game);
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (settings.Settings.AutoSyncOnClose)
            {
                _ = SyncSavesAsync(args.Game);
            }
        }

        private async Task SyncSavesAsync(Game game)
        {
            string savePath = scanner.FindSavePathForGame(game);
            if (string.IsNullOrEmpty(savePath))
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    // Unique per-game ID prevents collisions when multiple games sync concurrently.
                    $"dune-no-path-{game.Id}",
                    $"Dune: Could not auto-detect save path for \"{game.Name}\". Please map it in the Dune client.",
                    NotificationType.Error));
                return;
            }

            bool success = await ApiClient.UploadSaves(game.Name, savePath, _cts.Token);

            PlayniteApi.Notifications.Add(new NotificationMessage(
                $"dune-upload-{game.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                success
                    ? $"Dune: Saves synced for \"{game.Name}\"! ✓"
                    : $"Dune: Failed to sync saves for \"{game.Name}\". Check server connection.",
                success ? NotificationType.Info : NotificationType.Error));
        }

        private async Task PullSavesAsync(Game game)
        {
            string savePath = scanner.FindSavePathForGame(game);
            if (string.IsNullOrEmpty(savePath)) return; // Silent — don't block game launch

            bool success = await ApiClient.DownloadSaves(game.Name, savePath, _cts.Token);
            if (success)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"dune-pull-{game.Id}",
                    $"Dune: Latest saves restored for \"{game.Name}\" before launch.",
                    NotificationType.Info));
            }
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            return new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    MenuSection = "Dune Sync",
                    Description = "Push Saves to Server",
                    Action = (mainArgs) => { _ = SyncSavesAsync(mainArgs.Games[0]); }
                },
                new GameMenuItem
                {
                    MenuSection = "Dune Sync",
                    Description = "Pull Saves from Server",
                    Action = async (mainArgs) =>
                    {
                        var game = mainArgs.Games[0];
                        string savePath = scanner.FindSavePathForGame(game);
                        if (string.IsNullOrEmpty(savePath))
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage(
                                $"Could not find save path for \"{game.Name}\".",
                                "Dune Sync");
                            return;
                        }

                        bool success = await ApiClient.DownloadSaves(game.Name, savePath, _cts.Token);
                        if (success)
                            PlayniteApi.Dialogs.ShowMessage($"Saves restored for \"{game.Name}\".", "Dune Sync");
                        else
                            PlayniteApi.Dialogs.ShowErrorMessage(
                                $"Failed to restore saves for \"{game.Name}\".", "Dune Sync");
                    }
                }
            };
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}

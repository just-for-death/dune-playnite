using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Windows.Controls;

namespace DunePlayniteAddon
{
    public class DunePlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private DuneSettingsViewModel settings;
        private DuneScanner scanner;

        // Used to cancel in-flight syncs on plugin shutdown.
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public override Guid Id { get; } = Guid.Parse("d9e27c94-eba8-46f1-b4ea-6c444cc0500e");

        // Always construct from current settings so URL changes take effect immediately
        // without requiring a Playnite restart.
        private DuneApiClient ApiClient => new DuneApiClient(settings?.Settings?.ServerUrl ?? "http://localhost:3030");

        public DunePlugin(IPlayniteAPI api) : base(api)
        {
            try
            {
                settings = new DuneSettingsViewModel(this);
                scanner = new DuneScanner();
                Properties = new GenericPluginProperties { HasSettings = true };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to initialize Dune Save Sync plugin.");
            }
        }

        public override ISettings GetSettings(bool firstRunSettings) => settings;

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new DuneSettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"[Dune] Plugin started. Server: {settings.Settings.ServerUrl}");
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (settings.Settings.OfflineMode) return;
            
            if (settings.Settings.SyncOnGameStart)
            {
                PlayniteApi.Dialogs.ActivateGlobalProgress(async (progressArgs) =>
                {
                    await PullSavesAsync(args.Game, progressArgs.CancelToken);
                }, new GlobalProgressOptions($"Dune: Downloading saves for {args.Game.Name}...", cancelable: true));
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (settings.Settings.OfflineMode) return;

            if (settings.Settings.AutoSyncOnClose)
            {
                PlayniteApi.Dialogs.ActivateGlobalProgress(async (progressArgs) =>
                {
                    await SyncSavesAsync(args.Game, progressArgs.CancelToken);
                }, new GlobalProgressOptions($"Dune: Uploading saves for {args.Game.Name}...", cancelable: true));
            }
        }

        private async Task SyncSavesAsync(Game game, CancellationToken cancelToken = default)
        {
            string savePath = scanner.FindSavePathForGame(game);
            if (string.IsNullOrEmpty(savePath))
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"dune-no-path-{game.Id}",
                    $"Dune: Could not auto-detect save path for \"{game.Name}\". Please map it in the Dune client.",
                    NotificationType.Error));
                return;
            }

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancelToken))
            {
                bool success = await ApiClient.UploadSaves(game.Name, savePath, linkedCts.Token);

                // Use consistent ID to replace old sync notifications for the same game
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    $"dune-upload-{game.Id}",
                    success
                        ? $"Dune: Saves synced for \"{game.Name}\"! ✓"
                        : $"Dune: Failed to sync saves for \"{game.Name}\". Check server connection.",
                    success ? NotificationType.Info : NotificationType.Error));
            }
        }

        private async Task PullSavesAsync(Game game, CancellationToken cancelToken = default)
        {
            string savePath = scanner.FindSavePathForGame(game);
            if (string.IsNullOrEmpty(savePath)) return;

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancelToken))
            {
                bool success = await ApiClient.DownloadSaves(game.Name, savePath, linkedCts.Token);
                if (success)
                {
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        $"dune-pull-{game.Id}",
                        $"Dune: Latest saves restored for \"{game.Name}\".",
                        NotificationType.Info));
                }
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
                    Action = (mainArgs) => 
                    { 
                        PlayniteApi.Dialogs.ActivateGlobalProgress(async (progressArgs) =>
                        {
                            await SyncSavesAsync(mainArgs.Games[0], progressArgs.CancelToken);
                        }, new GlobalProgressOptions($"Dune: Uploading saves for {mainArgs.Games[0].Name}...", cancelable: true));
                    }
                },
                new GameMenuItem
                {
                    MenuSection = "Dune Sync",
                    Description = "Pull Saves from Server",
                    Action = (mainArgs) =>
                    {
                        var game = mainArgs.Games[0];
                        PlayniteApi.Dialogs.ActivateGlobalProgress(async (progressArgs) =>
                        {
                            await PullSavesAsync(game, progressArgs.CancelToken);
                        }, new GlobalProgressOptions($"Dune: Downloading saves for {game.Name}...", cancelable: true));
                    }
                }
            };
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}

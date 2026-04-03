using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace DunePlayniteAddon
{
    public class DunePlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private DuneSettingsViewModel settings;
        private DuneScanner scanner;
        private DuneApiClient apiClient;

        public override Guid Id { get; } = Guid.Parse("d9e27c94-eba8-46f1-b4ea-6c444cc0500e");

        public DunePlugin(IPlayniteAPI api) : base(api)
        {
            settings = new DuneSettingsViewModel(this);
            scanner = new DuneScanner();
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            apiClient = new DuneApiClient(settings.Settings.ServerUrl);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            // Optional: Pull saves before starting game
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
                    "dune-save-not-found",
                    $"Dune: Could not automatically detect save path for {game.Name}. Please map it in the Dune client.",
                    NotificationType.Error
                ));
                return;
            }

            bool success = await apiClient.UploadSaves(game.Name, savePath);
            if (success)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "dune-sync-success",
                    $"Dune: Successfully synced saves for {game.Name}!",
                    NotificationType.Info
                ));
            }
            else
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    "dune-sync-failed",
                    $"Dune: Failed to sync saves for {game.Name}. Check server connection.",
                    NotificationType.Error
                ));
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
                    Action = (mainArgs) => {
                        _ = SyncSavesAsync(mainArgs.Games[0]);
                    }
                },
                new GameMenuItem
                {
                    MenuSection = "Dune Sync",
                    Description = "Pull Saves from Server",
                    Action = async (mainArgs) => {
                        var game = mainArgs.Games[0];
                        string savePath = scanner.FindSavePathForGame(game);
                        if (string.IsNullOrEmpty(savePath))
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage($"Could not find save path for {game.Name}.");
                            return;
                        }

                        bool success = await apiClient.DownloadSaves(game.Name, savePath);
                        if (success)
                        {
                            PlayniteApi.Dialogs.ShowInfo($"Successfully restored saves for {game.Name}!");
                        }
                        else
                        {
                            PlayniteApi.Dialogs.ShowErrorMessage($"Failed to restore saves for {game.Name}.");
                        }
                    }
                }
            };
        }
    }
}

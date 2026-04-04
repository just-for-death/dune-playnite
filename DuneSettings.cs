using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;

namespace DunePlayniteAddon
{
    public class DuneSettings : ObservableObject
    {
        // Bug fix: default was 3010 — server runs on 3030.
        private string serverUrl = "http://localhost:3030";
        public string ServerUrl
        {
            get => serverUrl ?? "http://localhost:3030";
            set => SetValue(ref serverUrl, value);
        }

        private bool autoSyncOnClose = true;
        public bool AutoSyncOnClose
        {
            get => autoSyncOnClose;
            set => SetValue(ref autoSyncOnClose, value);
        }

        // New: pull saves from server before launching a game (restore-on-start).
        private bool syncOnGameStart = false;
        public bool SyncOnGameStart
        {
            get => syncOnGameStart;
            set => SetValue(ref syncOnGameStart, value);
        }

        // New: Dual-Boot / Offline Mode. Disables sync in Windows if syncing using local Linux dune-server.
        private bool offlineMode = false;
        public bool OfflineMode
        {
            get => offlineMode;
            set => SetValue(ref offlineMode, value);
        }
    }

    public class DuneSettingsViewModel : ObservableObject, ISettings
    {
        private readonly GenericPlugin plugin;
        private DuneSettings editingClone;

        public DuneSettings Settings { get; set; }

        public DuneSettingsViewModel(GenericPlugin plugin)
        {
            this.plugin = plugin;
            try
            {
                var savedSettings = plugin.LoadPluginSettings<DuneSettings>();
                Settings = savedSettings ?? new DuneSettings();
            }
            catch (Exception ex)
            {
                // Log and fallback to defaults if settings file is corrupted
                LogManager.GetLogger().Error(ex, "Failed to load Dune settings.");
                Settings = new DuneSettings();
            }
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Settings.ServerUrl))
            {
                errors.Add("Server URL cannot be empty.");
                return false;
            }

            if (!Uri.TryCreate(Settings.ServerUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"'{Settings.ServerUrl}' is not a valid HTTP/HTTPS URL.");
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace DunePlayniteAddon
{
    public class DuneSettings : ObservableObject
    {
        private string serverUrl = "http://localhost:3010";
        public string ServerUrl
        {
            get => serverUrl;
            set => SetValue(ref serverUrl, value);
        }

        private bool autoSyncOnClose = true;
        public bool AutoSyncOnClose
        {
            get => autoSyncOnClose;
            set => SetValue(ref autoSyncOnClose, value);
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
            var savedSettings = plugin.LoadPluginSettings<DuneSettings>();

            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
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
            return true;
        }
    }
}

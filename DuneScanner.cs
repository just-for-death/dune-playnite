using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using Playnite.SDK.Models;

namespace DunePlayniteAddon
{
    public class GamePath
    {
        public string Name { get; set; }
        public string WinPath { get; set; }
    }

    public class DuneScanner
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/mtkennerly/ludusavi-manifest/master/data/manifest.yaml";

        public string ExpandPath(string template)
        {
            if (string.IsNullOrEmpty(template)) return "";

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            var replacements = new Dictionary<string, string>
            {
                { "<home>", home },
                { "<userProfile>", home },
                { "<winAppData>", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
                { "<winLocalAppData>", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
                { "<winDocuments>", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
                { "<winPublic>", Path.Combine(Path.GetPathRoot(home), "Users", "Public") },
                { "<winProgramFiles>", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) },
                { "<winProgramFilesX86>", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) },
                { "<osUserName>", Environment.UserName },
                { "<steam>", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") }
            };

            string expanded = template;
            foreach (var r in replacements)
            {
                expanded = expanded.Replace(r.Key, r.Value);
            }

            // Handle %USERPROFILE% style environment variables
            expanded = Environment.ExpandEnvironmentVariables(expanded);

            return Path.GetFullPath(expanded);
        }

        public string FindSavePathForGame(Game game)
        {
            // Simple logic for now: check common folders or installation directory
            // In a full implementation, we would fetch the manifest and parse it
            // For this addon, we'll try to find a folder named after the game in standard locations
            
            string[] searchRoots = {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            };

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                // 1. Check for exact name match
                string candidate = Path.Combine(root, game.Name);
                if (Directory.Exists(candidate)) return candidate;
                
                // 2. Check for common suffixes
                candidate = Path.Combine(root, game.Name + " Saves");
                if (Directory.Exists(candidate)) return candidate;

                // 3. Nested check (e.g. Sega/GameName)
                try {
                    foreach (var sub in Directory.GetDirectories(root)) {
                        string nested = Path.Combine(sub, game.Name);
                        if (Directory.Exists(nested)) return nested;
                    }
                } catch { }
            }

            return null;
        }
    }
}

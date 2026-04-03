using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK.Models;

namespace DunePlayniteAddon
{
    /// <summary>
    /// Heuristically locates game save directories from common Windows save locations.
    /// Discovered paths are cached per Game.Id to avoid redundant disk scans on every
    /// game-start / game-stop event.
    /// </summary>
    public class DuneScanner
    {
        // Keyed by Game.Id — survives multiple start/stop cycles within one session.
        private readonly Dictionary<Guid, string> _pathCache = new Dictionary<Guid, string>();

        /// <summary>
        /// Expands Ludusavi-style path tokens (e.g. &lt;home&gt;, &lt;winAppData&gt;)
        /// and standard %ENVVAR% references into real filesystem paths.
        /// </summary>
        public string ExpandPath(string template)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "<home>",              home },
                { "<userProfile>",       home },
                { "<winAppData>",        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
                { "<winLocalAppData>",   Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) },
                { "<winDocuments>",      Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
                { "<winPublic>",         Path.Combine(Path.GetPathRoot(home), "Users", "Public") },
                { "<winProgramFiles>",   Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) },
                { "<winProgramFilesX86>",Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) },
                { "<osUserName>",        Environment.UserName },
                { "<steam>",             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") }
            };

            string expanded = template;
            foreach (var r in replacements)
                expanded = expanded.Replace(r.Key, r.Value);

            // Handle %USERPROFILE% / %APPDATA% etc.
            expanded = Environment.ExpandEnvironmentVariables(expanded);
            return Path.GetFullPath(expanded);
        }

        /// <summary>
        /// Searches common Windows save locations for a directory matching the game name.
        /// Results are cached so repeated calls are instant.
        /// </summary>
        public string FindSavePathForGame(Game game)
        {
            // Return cached result (re-verify it still exists)
            if (_pathCache.TryGetValue(game.Id, out string cached) && Directory.Exists(cached))
                return cached;

            string[] searchRoots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            };

            foreach (var root in searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                // 1. Exact name match: Documents/GameName
                string candidate = Path.Combine(root, game.Name);
                if (Directory.Exists(candidate))
                    return Cache(game.Id, candidate);

                // 2. Common "Saves" suffix: Documents/GameName Saves
                candidate = Path.Combine(root, game.Name + " Saves");
                if (Directory.Exists(candidate))
                    return Cache(game.Id, candidate);

                // 3. One level of nesting: Documents/Publisher/GameName
                try
                {
                    foreach (var sub in Directory.GetDirectories(root))
                    {
                        string nested = Path.Combine(sub, game.Name);
                        if (Directory.Exists(nested))
                            return Cache(game.Id, nested);
                    }
                }
                catch { /* skip protected directories */ }
            }

            return null;
        }

        /// <summary>Clears the cached path for a specific game (call if path moves).</summary>
        public void InvalidateCache(Guid gameId) => _pathCache.Remove(gameId);

        private string Cache(Guid id, string path)
        {
            _pathCache[id] = path;
            return path;
        }
    }
}

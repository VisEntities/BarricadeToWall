/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Barricade To Wall", "VisEntities", "1.1.0")]
    [Description("Turns barricades into high external walls automatically.")]
    public class BarricadeToWall : RustPlugin
    {
        #region Fields

        private static BarricadeToWall _plugin;
        private static Configuration _config;
        private static StoredData _storedData;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Enable By Default")]
            public bool EnableByDefault { get; set; }

            [JsonProperty("Chat Command")]
            public string ChatCommand { get; set; }

            [JsonProperty("Barricade Replacements")]
            public Dictionary<string, string> BarricadeReplacements { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            if (string.Compare(_config.Version, "1.1.0") < 0)
            {
                _config.ChatCommand = defaultConfig.ChatCommand;
                _config.EnableByDefault = defaultConfig.EnableByDefault;
            }

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                EnableByDefault = false,
                ChatCommand = "barricade",
                BarricadeReplacements = new Dictionary<string, string>
                {
                    ["assets/prefabs/deployable/barricades/barricade.cover.wood_double.prefab"]
                        = "assets/prefabs/building/wall.external.high.wood/wall.external.high.wood.prefab",

                    ["assets/prefabs/deployable/barricades/barricade.stone.prefab"]
                        = "assets/prefabs/building/wall.external.high.stone/wall.external.high.stone.prefab"
                }
            };
        }

        #endregion Configuration

        #region Stored Data

        private class StoredData
        {
            [JsonProperty("Barricade Enabled")]
            public Dictionary<ulong, bool> BarricadeEnabled { get; set; } = new Dictionary<ulong, bool>();
        }

        #endregion Stored Data

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
            _storedData = DataFileUtil.LoadOrCreate<StoredData>(DataFileUtil.GetFilePath());
            cmd.AddChatCommand(_config.ChatCommand, this, nameof(cmdBarricadeToggle));
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (planner == null || gameObject == null)
                return;

            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return;

            if (!HasBarricadeEnabled(player))
                return;

            BaseEntity entity = gameObject.ToBaseEntity();
            if (entity == null)
                return;

            string prefabName = entity.PrefabName;
            if (_config.BarricadeReplacements.TryGetValue(prefabName, out string newWallPrefab))
            {
                Vector3 position = entity.transform.position;
                Quaternion rotation = entity.transform.rotation;

                NextTick(() =>
                {
                    entity.Kill();

                    BaseEntity newEntity = GameManager.server.CreateEntity(newWallPrefab, position, rotation);
                    if (newEntity != null)
                    {
                        newEntity.OwnerID = player.userID;
                        newEntity.Spawn();
                    }
                });
            }
        }

        #endregion Oxide Hooks

        #region Permissions

        private static class PermissionUtil
        {
            public const string USE = "barricadetowall.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Helper Functions

        private bool HasBarricadeEnabled(BasePlayer player)
        {
            ulong userId = player.userID;

            if (!_storedData.BarricadeEnabled.TryGetValue(userId, out bool currentState))
            {
                currentState = _config.EnableByDefault;
            }

            return currentState;
        }

        #endregion Helper Functions

        #region Helper Classes

        public static class DataFileUtil
        {
            private const string FOLDER = "";

            public static string GetFilePath(string filename = null)
            {
                if (filename == null)
                    filename = _plugin.Name;

                return Path.Combine(FOLDER, filename);
            }

            public static string[] GetAllFilePaths()
            {
                string[] filePaths = Interface.Oxide.DataFileSystem.GetFiles(FOLDER);
                for (int i = 0; i < filePaths.Length; i++)
                {
                    filePaths[i] = filePaths[i].Substring(0, filePaths[i].Length - 5);
                }

                return filePaths;
            }

            public static bool Exists(string filePath)
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(filePath);
            }

            public static T Load<T>(string filePath) where T : class, new()
            {
                T data = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static T LoadIfExists<T>(string filePath) where T : class, new()
            {
                if (Exists(filePath))
                    return Load<T>(filePath);
                else
                    return null;
            }

            public static T LoadOrCreate<T>(string filePath) where T : class, new()
            {
                T data = LoadIfExists<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static void Save<T>(string filePath, T data)
            {
                Interface.Oxide.DataFileSystem.WriteObject(filePath, data);
            }

            public static void Delete(string filePath)
            {
                Interface.Oxide.DataFileSystem.DeleteDataFile(filePath);
            }
        }

        #endregion Helper Classes

        #region Commands

        private void cmdBarricadeToggle(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
            {
                MessagePlayer(player, Lang.NoPermission);
                return;
            }

            ulong userId = player.userID;
            bool current = HasBarricadeEnabled(player);
            bool newState = !current;

            _storedData.BarricadeEnabled[userId] = newState;
            DataFileUtil.Save(DataFileUtil.GetFilePath(), _storedData);

            if (newState)
                MessagePlayer(player, Lang.BarricadeEnabled);
            else
                MessagePlayer(player, Lang.BarricadeDisabled);
        }

        #endregion Commands

        #region Localization

        private class Lang
        {
            public const string NoPermission = "NoPermission";
            public const string BarricadeEnabled = "BarricadeEnabled";
            public const string BarricadeDisabled = "BarricadeDisabled";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.NoPermission] = "You do not have permission to use this command.",
                [Lang.BarricadeEnabled] = "You have turned Barricade-to-Wall ON! Any barricades you place will turn into walls.",
                [Lang.BarricadeDisabled] = "You have turned Barricade-to-Wall OFF! Barricades you place will remain normal."
            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);
            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}
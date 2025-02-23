/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Barricade To Wall", "VisEntities", "1.0.0")]
    [Description("Turns barricades into high external walls automatically.")]
    public class BarricadeToWall : RustPlugin
    {
        #region Fields

        private static BarricadeToWall _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

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

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
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

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
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
                        newEntity.Spawn();
                        newEntity.OwnerID = player.userID;
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
    }
}
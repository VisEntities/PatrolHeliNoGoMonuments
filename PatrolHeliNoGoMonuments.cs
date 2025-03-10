/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Patrol Heli No Go Monuments", "VisEntities", "1.0.0")]
    [Description(" ")]
    public class PatrolHeliNoGoMonuments : RustPlugin
    {
        #region Fields

        private static PatrolHeliNoGoMonuments _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("No Go Monuments")]
            public List<string> NoGoMonuments { get; set; }
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
                NoGoMonuments = new List<string>
                {
                    "airfield_1",
                    "powerplant_1"
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        #endregion Oxide Hooks

        #region Monument Filtering

        public bool IsNoGoMonument(MonumentInfo monument)
        {
            if (monument == null || string.IsNullOrEmpty(monument.name))
                return false;

            if (_config.NoGoMonuments == null || _config.NoGoMonuments.Count == 0)
                return false;

            string monumentNameLower = monument.name.ToLower();
            foreach (string badName in _config.NoGoMonuments)
            {
                if (!string.IsNullOrEmpty(badName) && monumentNameLower.Contains(badName.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion Monument Filtering

        #region Harmony

        [AutoPatch]
        [HarmonyPatch(typeof(PatrolHelicopterAI), "GenerateRandomDestination", new[] { typeof(bool) })]
        public static class PatrolHelicopterAI_GenerateRandomDestination_Patch
        {
            public static bool Prefix(PatrolHelicopterAI __instance, bool forceMonument, ref Vector3 __result)
            {
                if (__instance == null || __instance.helicopterBase == null)
                    return true;

                Vector3 vector = Vector3.zero;
                bool flag = Random.Range(0f, 1f) >= 0.6f;
                if (forceMonument)
                {
                    flag = true;
                }
                if (flag)
                {
                    if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null && TerrainMeta.Path.Monuments.Count > 0)
                    {
                        MonumentInfo monumentInfo = null;
                        if (__instance._visitedMonuments.Count > 0)
                        {
                            foreach (MonumentInfo monumentInfo2 in TerrainMeta.Path.Monuments)
                            {
                                if (!monumentInfo2.IsSafeZone && !_plugin.IsNoGoMonument(monumentInfo2))
                                {
                                    bool flag2 = false;
                                    foreach (MonumentInfo y in __instance._visitedMonuments)
                                    {
                                        if (monumentInfo2 == y)
                                        {
                                            flag2 = true;
                                        }
                                    }
                                    if (!flag2)
                                    {
                                        monumentInfo = monumentInfo2;
                                        break;
                                    }
                                }
                            }
                        }
                        if (monumentInfo == null)
                        {
                            __instance._visitedMonuments.Clear();
                            for (int i = 0; i < 5; i++)
                            {
                                int rIdx = Random.Range(0, TerrainMeta.Path.Monuments.Count);
                                monumentInfo = TerrainMeta.Path.Monuments[rIdx];
                                if (!monumentInfo.IsSafeZone && !_plugin.IsNoGoMonument(monumentInfo))
                                {
                                    break;
                                }
                            }
                        }
                        if (monumentInfo)
                        {
                            vector = monumentInfo.transform.position;
                            __instance._visitedMonuments.Add(monumentInfo);
                            vector.y = TerrainMeta.HeightMap.GetHeight(vector) + 200f;
                            RaycastHit raycastHit;
                            if (TransformUtil.GetGroundInfo(vector, out raycastHit, 300f, 1235288065, null))
                            {
                                vector.y = raycastHit.point.y;
                            }
                            vector.y += 30f;
                        }
                    }
                    else
                    {
                        vector = GetRandomMapPosition();
                    }
                }
                else
                {
                    vector = GetRandomMapPosition();
                }

                __result = vector;
                return false;
            }

            private static Vector3 GetRandomMapPosition()
            {
                float x = TerrainMeta.Size.x;
                float y = 30f;
                Vector3 vector = Vector3Ex.Range(-0.7f, 0.7f);
                vector.y = 0f;
                vector.Normalize();
                vector *= x * Random.Range(0f, 0.75f);
                vector.y = y;
                return vector;
            }
        }

        #endregion Harmony
    }
}
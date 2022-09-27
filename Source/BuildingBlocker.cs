using System;
using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Upgradeables;

namespace BuildingBlocker
{
    using MonoBehavior = UnityEngine.MonoBehaviour;

    /// <summary>
    /// Make the builders of KSC less profligate
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class BuildingBlocker : MonoBehavior
    {
        /// <summary>
        /// Hide useless buildings
        /// </summary>
        public void Start()
        {
            if (config == null)
            {
                config = new Config(GameDatabase.Instance);
            }
            var useless = config.UselessBuildings(HighLogic.CurrentGame.Mode);
            foreach (var facility in GameObject.FindObjectsOfType<UpgradeableFacility>())
            {
                var visible = !useless.Contains(facility.name);
                foreach (var level in facility.UpgradeLevels)
                {
                    level.facilityPrefab.SetActive(visible);
                }
            }
        }

        private Config config = null;

        /// <summary>
        /// Object representing a BUILDINGBLOCKERCONFIG node
        /// </summary>
        private class Config
        {
            /// <summary>
            /// Load the config from the game database
            /// Contained inside parent class to avoid namespace collisions with short name
            /// </summary>
            /// <param name="db">GameDatabase to scan</param>
            public Config(GameDatabase db)
            {
                gameModes = db.GetConfigs(configNodeName)
                    // Get all the GAMEMODE nodes from all the configs
                    .SelectMany(cfg => cfg.config.GetNodes(gameModeNodeName)
                                                 .Select(mode => new GameMode(mode)))
                    // Skip nodes with unparseable names
                    .Where(mode => mode.Mode.HasValue)
                    // Might have duplicates in case of multiple configs
                    .GroupBy(mode => mode.Mode.Value)
                    // Map the mode enum val to the full info
                    .ToDictionary(grp => grp.Key,
                                  // Merge duplicates
                                  grp => grp.First().MergeWith(grp.Skip(1)));
            }

            /// <summary>
            /// Check which facilities are configured to be hidden in a given game mode
            /// </summary>
            /// <param name="mode">The game mode to check</param>
            /// <returns>Names of facilities to hide</returns>
            public HashSet<string> UselessBuildings(Game.Modes mode)
                => gameModes.TryGetValue(mode, out GameMode modeConfig)
                    ? modeConfig.HiddenFacilities
                    : new HashSet<string>();

            private readonly Dictionary<Game.Modes, GameMode> gameModes;
            private const string configNodeName   = "BUILDINGBLOCKERCONFIG";
            private const string gameModeNodeName = "GAMEMODE";

            /// <summary>
            /// Info about how one game mode is configured
            /// </summary>
            private class GameMode
            {
                /// <summary>
                /// Initialize the game mode info
                /// </summary>
                /// <param name="node">ConfigNode to load</param>
                public GameMode(ConfigNode node)
                {
                    var name = node.GetValue("name");
                    if (modeAliases.TryGetValue(name, out string aliasValue))
                    {
                        name = aliasValue;
                    }
                    if (Enum.TryParse<Game.Modes>(name, true, out Game.Modes parsedMode))
                    {
                        Mode = parsedMode;
                    }
                    HiddenFacilities = node.GetNodes(hideFacNodeName)
                        .Select(hidFac => hidFac.GetValue("name"))
                        .ToHashSet();
                }

                /// <summary>
                /// Combine other game mode configs into this one
                /// </summary>
                /// <param name="others">Other game mode configs</param>
                /// <returns>Self, for caller's convenience</returns>
                public GameMode MergeWith(IEnumerable<GameMode> others)
                {
                    foreach (var other in others)
                    {
                        if (Mode != other.Mode)
                        {
                            throw new ArgumentException($"Can't merge {other.Mode} into {Mode}", "others");
                        }
                        HiddenFacilities.UnionWith(other.HiddenFacilities);
                    }
                    return this;
                }

                /// <summary>
                /// Machine readable representation of the game mode
                /// Null if missing or invalid
                /// https://www.kerbalspaceprogram.com/ksp/api/class_game.html
                /// </summary>
                public readonly Game.Modes? Mode = null;

                /// <summary>
                /// Which facilities are hidden
                /// https://www.kerbalspaceprogram.com/ksp/api/_scenario_upgradeable_facilities_8cs.html
                /// </summary>
                public readonly HashSet<string> HiddenFacilities;

                // Users probably expect "Science" instead of "Science_Sandbox"
                private static Dictionary<string, string> modeAliases =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "SCIENCE", "SCIENCE_SANDBOX" },
                    };
                private const string hideFacNodeName = "HIDEFACILITY";
            }
        }
    }

}

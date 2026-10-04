using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSimulator.Configuration
{
    /// <summary>
    /// Remappable keyboard shortcuts for everything polled directly via
    /// Keyboard.current (environment/sensor/flip/panel keys). InputSystem
    /// action-map bindings (WASD/QE/ZC/etc.) are handled separately by
    /// <see cref="InputRebindStore"/>. Persisted in PlayerPrefs.
    /// </summary>
    public static class KeyBindings
    {
        private const string PrefsKey = "DroneSim.KeyBindings.v1";

        public struct Default
        {
            public string Id;
            public string Label;
            public Key Key;
        }

        public static readonly Default[] Defaults =
        {
            new Default { Id = "windUp", Label = "Wind −/+", Key = Key.R },
            new Default { Id = "windDown", Label = "Wind −/+", Key = Key.F },
            new Default { Id = "windDirUp", Label = "Wind Dir −/+", Key = Key.T },
            new Default { Id = "windDirDown", Label = "Wind Dir −/+", Key = Key.G },
            new Default { Id = "turbUp", Label = "Turbulence −/+", Key = Key.Y },
            new Default { Id = "turbDown", Label = "Turbulence −/+", Key = Key.H },
            new Default { Id = "visUp", Label = "Visibility −/+", Key = Key.U },
            new Default { Id = "visDown", Label = "Visibility −/+", Key = Key.J },
            new Default { Id = "weather", Label = "Cycle Weather", Key = Key.V },
            new Default { Id = "time", Label = "Cycle Time", Key = Key.B },
            new Default { Id = "sensUp", Label = "Degradation −/+", Key = Key.I },
            new Default { Id = "sensDown", Label = "Degradation −/+", Key = Key.K },
            new Default { Id = "sensReset", Label = "Reset Degradation", Key = Key.O },
            new Default { Id = "flipFwd", Label = "Flip Forward 180°", Key = Key.X },
            new Default { Id = "flipRoll", Label = "Flip Roll 180°", Key = Key.N },
            new Default { Id = "panel", Label = "Controls Panel", Key = Key.F1 },
            new Default { Id = "hud", Label = "Hide / Show HUD", Key = Key.F2 },
        };

        private static readonly Dictionary<string, Key> map = new Dictionary<string, Key>();
        private static bool loaded;

        public static Key Get(string id)
        {
            EnsureLoaded();
            if (map.TryGetValue(id, out var key))
            {
                return key;
            }

            return Key.None;
        }

        public static void Set(string id, Key key)
        {
            EnsureLoaded();
            map[id] = key;
            Save();
        }

        public static void ResetAll()
        {
            map.Clear();
            foreach (var d in Defaults)
            {
                map[d.Id] = d.Key;
            }

            Save();
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            foreach (var d in Defaults)
            {
                map[d.Id] = d.Key;
            }

            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            // Format: id=KeyName;id=KeyName
            foreach (var pair in raw.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string id = pair.Substring(0, eq);
                string name = pair.Substring(eq + 1);
                if (System.Enum.TryParse(name, out Key key) && map.ContainsKey(id))
                {
                    map[id] = key;
                }
            }
        }

        private static void Save()
        {
            var parts = new List<string>();
            foreach (var d in Defaults)
            {
                parts.Add(d.Id + "=" + (map.TryGetValue(d.Id, out var k) ? k : d.Key));
            }

            PlayerPrefs.SetString(PrefsKey, string.Join(";", parts));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Persistence for InputSystem action-map binding overrides (WASD/QE/ZC/
    /// arrows/brackets). Applied to the project-wide asset before any
    /// controller binds, so overrides survive restarts.
    /// </summary>
    public static class InputRebindStore
    {
        private const string PrefsKey = "DroneSim.InputOverrides.v1";

        public static void Apply(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                asset.LoadBindingOverridesFromJson(json);
            }
        }

        public static void Save(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public static void Reset(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        /// <summary>Index of the binding with the exact default path, or -1.</summary>
        public static int FindBinding(InputAction action, string defaultPath)
        {
            if (action == null)
            {
                return -1;
            }

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].path == defaultPath)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Human-readable key name honoring overrides ("W", "Up Arrow").</summary>
        public static string DisplayKey(InputAction action, string defaultPath)
        {
            int index = FindBinding(action, defaultPath);
            if (index < 0)
            {
                return "?";
            }

            return action.GetBindingDisplayString(index);
        }
    }
}

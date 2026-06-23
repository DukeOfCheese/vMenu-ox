using System.Collections.Generic;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;

namespace vMenuClient.data
{
    /// <summary>
    /// Base-game ped drawable variation counts per (model hash, component index), used to cap
    /// clothing sliders for players without the EUP permission. Values are captured from a server
    /// with EUP disabled (see plan Task 7). Unknown models return -1 (caller leaves sliders uncapped).
    /// </summary>
    public static class EupBaseCounts
    {
        private static readonly Dictionary<uint, Dictionary<int, int>> _base = new Dictionary<uint, Dictionary<int, int>>
        {
            {
                (uint)GetHashKey("mp_m_freemode_01"), new Dictionary<int, int>
                {
                    // component index -> base drawable count (CAPTURE in Step 2):
                    // { 1, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }, { 6, 0 },
                    // { 7, 0 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 },
                }
            },
            {
                (uint)GetHashKey("mp_f_freemode_01"), new Dictionary<int, int>
                {
                    // { 1, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 }, { 6, 0 },
                    // { 7, 0 }, { 8, 0 }, { 9, 0 }, { 10, 0 }, { 11, 0 },
                }
            },
        };

        private static readonly HashSet<uint> _warned = new HashSet<uint>();

        public static int For(uint model, int component)
        {
            if (_base.TryGetValue(model, out var comps) && comps.TryGetValue(component, out var count))
            {
                return count;
            }
            if (_warned.Add(model))
            {
                Debug.WriteLine($"[vMenu] [Addons] No EUP base-count entry for ped model {model} / component {component}; leaving its sliders uncapped (fail-open).");
            }
            return -1;
        }
    }
}

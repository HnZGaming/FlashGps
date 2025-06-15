using System;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace FlashGps
{
    public sealed class EntryState
    {
        public readonly IMyGps Gps;
        public FlashGpsApi.Entry Entry;
        public DateTime LastUpdate;
        public IMyEntity Entity;

        public EntryState(IMyGps gps, FlashGpsApi.Entry entry, DateTime lastUpdate)
        {
            Entry = entry;
            Gps = gps;
            LastUpdate = lastUpdate;
        }
    }
}
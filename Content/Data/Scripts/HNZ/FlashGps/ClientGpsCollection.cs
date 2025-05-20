using System.Collections.Generic;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Logging;
using HNZ.Utils.Pools;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using DateTime = System.DateTime;

namespace HNZ.FlashGps
{
    public sealed class ClientGpsCollection
    {
        static readonly Logger Log = LoggerManager.Create(nameof(ClientGpsCollection));

        readonly Dictionary<long, ClientGps> _gpsEntries;

        public ClientGpsCollection()
        {
            _gpsEntries = new Dictionary<long, ClientGps>();
        }

        public void UpsertGps(FlashGpsSource src)
        {
            var localPlayer = MyAPIGateway.Session.LocalHumanPlayer;
            var character = localPlayer?.Character;
            if (character == null) return;

            if (localPlayer.PromoteLevel < (MyPromoteLevel)src.PromoteLevel)
            {
                RemoveGps(src.Id);
                return;
            }

            // insert
            ClientGps gpsEntry;
            if (!_gpsEntries.TryGetValue(src.Id, out gpsEntry))
            {
                var gps = MyAPIGateway.Session.GPS.Create($"{src.Id}", src.Description, src.Position, true, false);

                MyAPIGateway.Session.GPS.AddLocalGps(gps);

                if (!src.SuppressSound)
                {
                    GameUtils.PlaySound("HudGPSNotification3");
                }

                _gpsEntries[src.Id] = gpsEntry = new ClientGps
                {
                    Gps = gps,
                    Follow = new ClientGpsFollow(gps),
                };

                Log.Debug($"added; id: {src.Id}, name: {src.Name}, pos: {src.Position}, radius: {src.Radius}, jingle: {!src.SuppressSound}");
            }

            // update
            gpsEntry.Gps.Name = src.Name ?? "";
            gpsEntry.Gps.Description = src.Description ?? "";
            gpsEntry.Gps.GPSColor = src.Color;
            gpsEntry.Follow.SetTargetPosition(src.Position);
            gpsEntry.Follow.SetTargetEntity(src.EntityId);

            if (src.DecaySeconds > 0)
            {
                gpsEntry.DecayTime = DateTime.UtcNow + src.DecaySeconds.Seconds();
            }
        }

        public void RemoveGps(long gpsId)
        {
            ClientGps gpsEntry;
            if (_gpsEntries.TryGetValue(gpsId, out gpsEntry))
            {
                MyAPIGateway.Session.GPS.RemoveLocalGps(gpsEntry.Gps);
                _gpsEntries.Remove(gpsId);
                Log.Debug($"removed: {gpsId}");
            }
        }

        public void Update()
        {
            var gpsEntries = ListPool<KeyValuePair<long, ClientGps>>.Get();
            gpsEntries.AddRange(_gpsEntries);

            foreach (var p in gpsEntries)
            {
                var gpsEntry = p.Value;

                gpsEntry.Follow.Update();

                //Log.Debug($"{gpsEntry.DecayTime} < {DateTime.UtcNow}");
                if (gpsEntry.DecayTime < DateTime.UtcNow)
                {
                    Log.Debug("removing for decay");
                    RemoveGps(p.Key);
                }
            }

            ListPool<KeyValuePair<long, ClientGps>>.Release(gpsEntries);
        }
    }
}
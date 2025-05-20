using System;
using System.Collections.Generic;
using FlashGps.Utils;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace FlashGps
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once UnusedType.Global
    public sealed class Session : MySessionComponentBase
    {
        static readonly ushort InternalKey = (ushort)"FlashGpsApi.Internal".GetHashCode();
        Dictionary<long, Wrap> _wraps;

        public override void LoadData()
        {
            base.LoadData();

            if (MyAPIGateway.Session.IsServer)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(FlashGpsApi.Key, OnApiMessageReceived);
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _wraps = new Dictionary<long, Wrap>();
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(InternalKey, OnInternalMessageReceived);
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            if (MyAPIGateway.Utilities.IsDedicated) return;

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(InternalKey, OnApiMessageReceived);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(FlashGpsApi.Key, OnInternalMessageReceived);
        }

        void OnApiMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                MyLog.Default.Error("[FlashGPS] invalid API message");
                return;
            }

            var entry = MyAPIGateway.Utilities.SerializeFromBinary<FlashGpsApi.Entry>(bytes);
            MyLog.Default.Info($"[FlashGPS] API message received: {entry.Id}, {entry.Name}, {entry.Position}");

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                if (!CanReach(p, entry.Position, entry.Radius)) continue;

                MyAPIGateway.Multiplayer.SendMessageTo(InternalKey, bytes, p.SteamUserId);
            }
        }

        void OnInternalMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                MyLog.Default.Error("[FlashGPS] invalid API message");
                return;
            }

            var entry = MyAPIGateway.Utilities.SerializeFromBinary<FlashGpsApi.Entry>(bytes);

            Wrap wrap;
            if (!_wraps.TryGetValue(entry.Id, out wrap))
            {
                var gps = MyAPIGateway.Session.GPS.Create(entry.Name, "", entry.Position, true);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);

                if (!entry.Mute)
                {
                    VRageUtils.PlaySound("HudGPSNotification3");
                }

                wrap = new Wrap(gps, entry, DateTime.UtcNow);
                _wraps.Add(entry.Id, wrap);
            }

            wrap.Gps.Name = entry.Name;
            wrap.Gps.Coords = entry.Position;
            wrap.Gps.GPSColor = entry.Color;
            wrap.Entry = entry;
            wrap.LastUpdate = DateTime.UtcNow;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (MyAPIGateway.Utilities.IsDedicated) return;

            foreach (var kvp in _wraps)
            {
                UpdateEntityMapping(kvp.Value);
                UpdatePosition(kvp.Value);
            }

            RemoveExpiredGps();
        }

        static void UpdateEntityMapping(Wrap g)
        {
            var targetId = g.Entry.EntityId;
            if (targetId == 0)
            {
                g.Entity = null;
                return;
            }

            if (g.Entity?.EntityId == targetId) return;

            g.Entity = MyAPIGateway.Entities.GetEntityById(targetId);
            //MyLog.Default.WriteLine($"[HnzCoopSeason] mapping entity to gps; name: '{g.Entry.Name}', entity: '{g.Entity}' ({g.Entry.EntityId})");
        }

        static void UpdatePosition(Wrap g)
        {
            g.Gps.Coords = g.Entity != null
                ? g.Entity.GetPosition()
                : g.Entry.Position;
        }

        void RemoveExpiredGps()
        {
            var expiredIds = new List<long>();
            foreach (var kvp in _wraps)
            {
                var wrap = kvp.Value;
                if (ShouldRemove(wrap))
                {
                    expiredIds.Add(kvp.Key);
                    MyAPIGateway.Session.GPS.RemoveLocalGps(wrap.Gps);
                }
            }

            foreach (var expiredId in expiredIds)
            {
                _wraps.Remove(expiredId);
            }
        }

        static bool ShouldRemove(Wrap wrap)
        {
            if ((DateTime.UtcNow - wrap.LastUpdate).TotalSeconds > wrap.Entry.Duration) return true;
            if (!CanReach(MyAPIGateway.Session.Player, wrap.Entry.Position, wrap.Entry.Radius)) return true;

            return false;
        }

        static bool CanReach(IMyPlayer player, Vector3D origin, double radius)
        {
            if (radius <= 0) return true; // everyone

            var character = player.Character;
            if (character == null) return false;

            var sphere = new BoundingSphereD(origin, radius);
            if (sphere.Contains(character.GetPosition()) == ContainmentType.Disjoint) return false;

            return true;
        }

        sealed class Wrap
        {
            public readonly IMyGps Gps;
            public FlashGpsApi.Entry Entry;
            public DateTime LastUpdate;
            public IMyEntity Entity;

            public Wrap(IMyGps gps, FlashGpsApi.Entry entry, DateTime lastUpdate)
            {
                Entry = entry;
                Gps = gps;
                LastUpdate = lastUpdate;
            }
        }
    }
}
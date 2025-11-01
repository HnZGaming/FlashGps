using System;
using System.Collections.Generic;
using FlashGps.Utils;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace FlashGps
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once UnusedType.Global
    public sealed class Session : MySessionComponentBase
    {
        static readonly ushort InternalKey = (ushort)"FlashGpsApi.Internal".GetHashCode();
        static readonly ushort NexusKey = (ushort)"FlashGpsApi.Nexus".GetHashCode();
        ModMessageBroker _modMessageBroker;
        Dictionary<long, EntryState> _states;

        public override void LoadData()
        {
            base.LoadData();

            if (MyAPIGateway.Session.IsServer)
            {
                MyLog.Default.Info("[FlashGPS] loading as server");

                _modMessageBroker = new ModMessageBroker(NexusKey);
                _modMessageBroker.Load();
                _modMessageBroker.OnReceived += OnModBrokerMessageReceived;

                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(FlashGpsApi.Key, OnApiMessageReceived);
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                MyLog.Default.Info("[FlashGPS] loading as client");

                _states = new Dictionary<long, EntryState>();
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(InternalKey, OnInternalMessageReceived);
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            if (MyAPIGateway.Session.IsServer)
            {
                MyLog.Default.Info("[FlashGPS] unloading as server");

                _modMessageBroker.OnReceived -= OnModBrokerMessageReceived;
                _modMessageBroker.Unload();

                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(FlashGpsApi.Key, OnInternalMessageReceived);
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                MyLog.Default.Info("[FlashGPS] unloading as client");

                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(InternalKey, OnApiMessageReceived);
            }
        }

        // Called in server when `FlashGpsApi.Send()` is called in this server
        void OnApiMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (!MyAPIGateway.Session.IsServer)
            {
                MyLog.Default.Error("[FlashGPS] invalid API message");
                return;
            }

            MyLog.Default.Info($"[FlashGPS] API message received; sender: {senderId}");
            _modMessageBroker.Send(bytes);
        }

        // Called in server when `FlashGpsApi.Send()` is called in any servers in the same Nexus sector
        void OnModBrokerMessageReceived(byte[] bytes)
        {
            var entry = MyAPIGateway.Utilities.SerializeFromBinary<FlashGpsApi.Entry>(bytes);
            MyLog.Default.Info($"[FlashGPS] broker message received: {entry.Id}, {entry.Name}, {entry.Position}");

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                if (!CanReach(p, entry.Position, entry.Radius)) continue;

                MyLog.Default.Info($"[FlashGPS] sending internal message; receiver: {p.SteamUserId}");
                MyAPIGateway.Multiplayer.SendMessageTo(InternalKey, bytes, p.SteamUserId);
            }
        }

        // Called in client
        void OnInternalMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
            {
                MyLog.Default.Error("[FlashGPS] invalid API message");
                return;
            }

            var entry = MyAPIGateway.Utilities.SerializeFromBinary<FlashGpsApi.Entry>(bytes);
            MyLog.Default.Info($"[FlashGPS] internal message received: {entry.Id}, {entry.Name}, {entry.Position}");

            EntryState state;
            if (!_states.TryGetValue(entry.Id, out state))
            {
                var gps = MyAPIGateway.Session.GPS.Create(entry.Name, "", entry.Position, true);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);

                if (!entry.Mute)
                {
                    VRageUtils.PlaySound("HudGPSNotification3");
                }

                state = new EntryState(gps, entry, DateTime.UtcNow);
                _states.Add(entry.Id, state);
                MyLog.Default.Info($"[FlashGPS] Created; id: {entry.Id}");
            }

            state.Gps.Name = entry.Name ?? "";
            state.Gps.Coords = entry.Position;
            state.Gps.GPSColor = entry.Color;
            state.Gps.Description = entry.Description ?? "";
            state.Entry = entry;
            state.LastUpdate = DateTime.UtcNow;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (MyAPIGateway.Utilities.IsDedicated) return;

            foreach (var kvp in _states)
            {
                UpdateEntityMapping(kvp.Value);
                UpdatePosition(kvp.Value);
            }

            RemoveExpiredGps();
        }

        static void UpdateEntityMapping(EntryState g)
        {
            var targetId = g.Entry.EntityId;
            if (targetId == 0)
            {
                g.Entity = null;
                return;
            }

            if (g.Entity?.EntityId == targetId) return;

            g.Entity = MyAPIGateway.Entities.GetEntityById(targetId);
            MyLog.Default.Info($"[FlashGPS] Mapping; name: '{g.Entry.Name}', entity: '{g.Entity}' ({g.Entry.EntityId})");
        }

        static void UpdatePosition(EntryState g)
        {
            g.Gps.Coords = g.Entity != null
                ? g.Entity.GetPosition()
                : g.Entry.Position;
        }

        void RemoveExpiredGps()
        {
            var expiredIds = new List<long>();
            foreach (var kvp in _states)
            {
                var wrap = kvp.Value;
                if (ShouldRemove(wrap))
                {
                    expiredIds.Add(kvp.Key);
                    MyAPIGateway.Session.GPS.RemoveLocalGps(wrap.Gps);
                    MyLog.Default.Info($"[FlashGPS] Expired: {kvp.Key}");
                }
            }

            foreach (var expiredId in expiredIds)
            {
                _states.Remove(expiredId);
            }
        }

        static bool ShouldRemove(EntryState state)
        {
            if ((DateTime.UtcNow - state.LastUpdate).TotalSeconds > state.Entry.Duration) return true;
            if (!CanReach(MyAPIGateway.Session.Player, state.Entry.Position, state.Entry.Radius)) return true;

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
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Communications;
using HNZ.Utils.Logging;
using HNZ.Utils.Pools;
using Sandbox.ModAPI;
using VRage;

namespace HNZ.FlashGps
{
    public sealed class Network : IProtobufListener
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Network));

        readonly ProtobufModule _protobufModule;
        readonly byte _loadId;
        readonly Dictionary<long, ClientGpsCollection> _clientGps; // mod id to gps collection
        readonly ServerGpsFilter _serverGpsFilter;

        //todo buffer sending multiple data

        public Network(ProtobufModule protobufModule, byte loadId)
        {
            _protobufModule = protobufModule;
            _loadId = loadId;
            _clientGps = new Dictionary<long, ClientGpsCollection>();
            _serverGpsFilter = new ServerGpsFilter();
        }

        public void Initialize()
        {
            _protobufModule.AddListener(this);
        }

        public void Close()
        {
            _protobufModule.RemoveListener(this);
            _clientGps.Clear();
            _serverGpsFilter.Clear();
        }

        public void Update()
        {
            if (MyAPIGateway.Session.IsServer && MyAPIGateway.Session.GameplayFrameCounter % 60 == 0)
            {
                _serverGpsFilter.Update();
            }

            if (_clientGps.Count > 0)
            {
                foreach (var c in _clientGps)
                {
                    c.Value.Update();
                }
            }
        }

        public void SendUpsertGps(long moduleId, FlashGpsSource src, bool reliable = true)
        {
            Log.Debug($"Sending upsert: {moduleId}, {src.Id}, \"{src.Name}\"");

            if (src.Id == 0)
            {
                throw new InvalidOperationException($"gps id not set; module id: {moduleId}");
            }

            var playerIds = SetPool<ulong>.Create();

            // single player mode
            if (MyAPIGateway.Session.IsServer && !MyAPIGateway.Utilities.IsDedicated)
            {
                Log.Debug("single player mode");
                playerIds.Add(MyAPIGateway.Session.Player.SteamUserId);
            }
            else
            {
                _serverGpsFilter.GetReceivingPlayerIds(src, playerIds);
            }

            Log.Debug($"receiving: {playerIds.SeqToString()}");

            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteUpsertFlashGps(moduleId, src);
                _protobufModule.SendDataToClients(_loadId, stream.Data, reliable, playerIds);
            }

            SetPool<ulong>.Release(playerIds);
        }

        public void SendRemoveGps(long moduleId, long gpsId, bool reliable = true)
        {
            Log.Debug($"Sending remove: {moduleId}, {gpsId}");

            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteRemoveFlashGps(moduleId, gpsId);
                _protobufModule.SendDataToClients(_loadId, stream.Data, reliable);
            }
        }

        // receive on client
        bool IProtobufListener.TryProcessProtobuf(byte loadId, BinaryReader reader)
        {
            if (loadId != _loadId) return false;

            bool isUpsert;
            long moduleId;
            FlashGpsSource src;
            long gpsId;
            reader.ReadFlashGps(out isUpsert, out moduleId, out src, out gpsId);
            if (isUpsert)
            {
                UpsertGps(moduleId, src);
            }
            else
            {
                RemoveGps(moduleId, gpsId);
            }

            return true;
        }

        void UpsertGps(long moduleId, FlashGpsSource src)
        {
            Log.Debug($"Received add or update: {moduleId}, {src.Id}, \"{src.Name}\"");

            GetFlashGpsCollection(moduleId).UpsertGps(src);
        }

        void RemoveGps(long moduleId, long gpsId)
        {
            Log.Debug($"Received remove: {moduleId}, {gpsId}");

            GetFlashGpsCollection(moduleId).RemoveGps(gpsId);
        }

        ClientGpsCollection GetFlashGpsCollection(long moduleId)
        {
            ClientGpsCollection c;
            if (!_clientGps.TryGetValue(moduleId, out c))
            {
                c = _clientGps[moduleId] = new ClientGpsCollection();
            }

            return c;
        }
    }
}
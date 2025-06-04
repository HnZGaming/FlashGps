using System;
using System.Collections.Generic;
using FlashGps.Nexus;
using Sandbox.ModAPI;
using VRage.Utils;

namespace FlashGps
{
    public class ModMessageBroker
    {
        readonly NexusAPI _nexusApi;

        public ModMessageBroker(ushort key)
        {
            _nexusApi = new NexusAPI(key);
        }

        public event Action<byte[]> OnReceived;

        public void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_nexusApi.CrossServerModID, OnMessageReceived);

            MyLog.Default.Info($"[FlashGPS] nexus running: {NexusAPI.IsRunningNexus()}");
        }

        public void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_nexusApi.CrossServerModID, OnMessageReceived);
        }

        public void Send(byte[] bytes)
        {
            OnReceived?.Invoke(bytes);

            foreach (var server in GetOtherNexusServers())
            {
                MyLog.Default.Info($"[FlashGPS] sending Nexus message to server: {server}");
                _nexusApi.SendMessageToServer(server, bytes);
            }
        }

        void OnMessageReceived(ushort key, byte[] bytes, ulong senderId, bool fromServer)
        {
            MyLog.Default.Info("[FlashGPS] Nexus message received");
            OnReceived?.Invoke(bytes);
        }

        static IEnumerable<int> GetOtherNexusServers()
        {
            var servers = new List<int>();

            if (!NexusAPI.IsRunningNexus()) return servers;

            var myServer = NexusAPI.GetThisServer();
            if (myServer.ServerType != 0) return servers;

            var allServers = NexusAPI.GetAllServers();
            foreach (var server in allServers)
            {
                if (server.ServerType != 0) continue;
                if (server.ServerID == myServer.ServerID) continue;

                servers.Add(server.ServerID);
            }

            return servers;
        }
    }
}
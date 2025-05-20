using System.Collections.Generic;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Logging;
using HNZ.Utils.Pools;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.FlashGps
{
    public sealed class ServerGpsFilter
    {
        static readonly Logger Log = LoggerManager.Create(nameof(ServerGpsFilter));

        readonly Dictionary<ulong, IMyCharacter> _serverPlayers; // character entity id to player steam id

        public ServerGpsFilter()
        {
            _serverPlayers = new Dictionary<ulong, IMyCharacter>();
        }

        public void Clear()
        {
            _serverPlayers.Clear();
        }

        public void Update()
        {
            _serverPlayers.Clear();

            var players = ListPool<IMyPlayer>.Get();
            MyAPIGateway.Players.GetPlayers(players);
            foreach (var player in players)
            {
                if (player.Character != null)
                {
                    _serverPlayers[player.SteamUserId] = player.Character;
                }
            }

            ListPool<IMyPlayer>.Release(players);
        }

        public void GetReceivingPlayerIds(FlashGpsSource src, ISet<ulong> playerIds)
        {
            playerIds.AddRange(_serverPlayers.Keys);
            Log.Debug($"all players: {playerIds.SeqToString()}");

            if (src.TargetPlayers != null)
            {
                playerIds.IntersectWith(src.TargetPlayers);
            }

            if (src.ExcludedPlayers != null)
            {
                playerIds.ExceptWith(src.ExcludedPlayers);
            }

            if (src.Radius > 0)
            {
                var farPlayerIds = SetPool<ulong>.Create();
                foreach (var playerId in playerIds)
                {
                    IMyCharacter character;
                    if (!_serverPlayers.TryGetValue(playerId, out character)) continue;

                    var playerPosition = character.GetPosition();
                    if (Vector3D.Distance(src.Position, playerPosition) > src.Radius)
                    {
                        farPlayerIds.Add(playerId);
                        Log.Debug($"far: {playerId}");
                    }
                }

                playerIds.ExceptWith(farPlayerIds);
                SetPool<ulong>.Release(farPlayerIds);
                
                Log.Debug($"receiving players: {playerIds.SeqToString()}");
            }
        }
    }
}
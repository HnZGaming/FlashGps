using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HNZ.FlashGps.Interface;
using HNZ.Utils;
using HNZ.Utils.Communications;
using HNZ.Utils.Logging;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRageMath;

namespace HNZ.FlashGps
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class Session : MySessionComponentBase, ICommandListener
    {
        static readonly Logger Log = LoggerManager.Create(nameof(Session));
        static readonly long ModuleId = nameof(FlashGps).GetHashCode();

        ContentFile<Config> _configFile;
        Dictionary<string, Action<Command>> _commands;
        ProtobufModule _protobufModule;
        CommandModule _commandModule;
        Network _network;

        public override void LoadData()
        {
            LoggerManager.SetPrefix(nameof(FlashGps));

            _commands = new Dictionary<string, Action<Command>>
            {
                { "reload", Command_Reload },
                { "upsert", Command_Upsert },
                { "remove", Command_Remove },
            };

            _protobufModule = new ProtobufModule((ushort)nameof(FlashGps).GetHashCode());
            _protobufModule.Initialize();

            _commandModule = new CommandModule(_protobufModule, 1, "fg", this);
            _commandModule.Initialize();

            _network = new Network(_protobufModule, 2);
            _network.Initialize();

            MyAPIGateway.Utilities.RegisterMessageHandler(FlashGpsApi.ModVersion, OnModMessageReceived);

            ReloadConfig();
        }

        void ReloadConfig()
        {
            _configFile = new ContentFile<Config>("FlashGps.cfg", Config.CreateDefault());
            _configFile.ReadOrCreateFile();
            Config.Instance = _configFile.Content;
            LoggerManager.SetConfigs(Config.Instance.LogConfigs);
        }

        protected override void UnloadData()
        {
            _protobufModule?.Close();
            _commandModule?.Close();
            _network?.Close();
            MyAPIGateway.Utilities.UnregisterMessageHandler(FlashGpsApi.ModVersion, OnModMessageReceived);
        }

        public override void UpdateBeforeSimulation()
        {
            _protobufModule.Update();
            _commandModule.Update();
            _network.Update();
        }

        void OnModMessageReceived(object message)
        {
            var load = message as byte[];
            if (load == null) return; // shouldn't happen

            using (var stream = new ByteStream(load, load.Length))
            using (var binaryReader = new BinaryReader(stream))
            {
                bool isUpsert;
                long moduleId;
                FlashGpsSource src;
                long gpsId;
                binaryReader.ReadFlashGps(out isUpsert, out moduleId, out src, out gpsId);
                if (isUpsert)
                {
                    _network.SendUpsertGps(moduleId, src);
                }
                else
                {
                    _network.SendRemoveGps(moduleId, gpsId);
                }
            }
        }

        bool ICommandListener.ProcessCommandOnClient(Command command)
        {
            //todo test admin
            //todo filter
            _commands.GetValueOrDefault(command.Header, null)?.Invoke(command);
            return false;
        }

        void ICommandListener.ProcessCommandOnServer(Command command)
        {
            _commands.GetValueOrDefault(command.Header, null)?.Invoke(command);
        }

        void Command_Reload(Command command)
        {
            ReloadConfig();
            command.Respond("Local GPS", Color.White, "config reloaded");
        }

        void Command_Upsert(Command command)
        {
            try
            {
                var args = ParseCommandArg(command);

                var src = new FlashGpsSource
                {
                    Id = long.Parse(args["id"]),
                    Position = new Vector3D(
                        float.Parse(args.GetValueOrDefault("x", "0")),
                        float.Parse(args.GetValueOrDefault("y", "0")),
                        float.Parse(args.GetValueOrDefault("z", "0"))),
                    Color = Color.Green,
                    Name = args.GetValueOrDefault("name", "debug"),
                    Description = args.GetValueOrDefault("description", "debug"),
                    DecaySeconds = int.Parse(args.GetValueOrDefault("decay", "5")),
                    Radius = float.Parse(args.GetValueOrDefault("radius", "0")),
                    EntityId = long.Parse(args.GetValueOrDefault("entity", "0")),
                    PromoteLevel = int.Parse(args.GetValueOrDefault("level", "0")),
                    ExcludedPlayers = Array.Empty<ulong>(),
                    TargetPlayers = Array.Empty<ulong>(),
                    SuppressSound = !bool.Parse(args.GetValueOrDefault("jingle", "true")),
                };

                var reliable = bool.Parse(args.GetValueOrDefault("reliable", "true"));

                command.Respond("Local GPS", Color.White, $"sending upsert gps; id: {src.Id}");
                _network.SendUpsertGps(ModuleId, src, reliable);
            }
            catch (Exception e)
            {
                command.Respond("Local GPS", Color.Red, $"error: {e}");
            }
        }

        void Command_Remove(Command command)
        {
            var args = ParseCommandArg(command);
            var id = long.Parse(args["id"]);
            var reliable = bool.Parse(args.GetValueOrDefault("reliable", "true"));

            command.Respond("Local GPS", Color.White, $"sending remove gps; id: {id}");
            _network.SendRemoveGps(ModuleId, id, reliable);
        }

        static Dictionary<string, string> ParseCommandArg(Command command)
        {
            return command.Arguments
                .Select(a => a.Split('='))
                .ToDictionary(p => p[0], p => p[1]);
        }
    }
}
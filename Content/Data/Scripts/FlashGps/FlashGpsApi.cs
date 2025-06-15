using ProtoBuf;
using Sandbox.ModAPI;
using VRageMath;

namespace FlashGps
{
    public static class FlashGpsApi
    {
        public static readonly ushort Key = (ushort)"FlashGpsApi:2.1.*".GetHashCode();

        public static void Send(Entry entry)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(entry);
            MyAPIGateway.Multiplayer.SendMessageToServer(Key, bytes);
        }

        [ProtoContract]
        public sealed class Entry
        {
            [ProtoMember(1)]
            public long Id { get; set; }

            [ProtoMember(2)]
            public string Name { get; set; }

            [ProtoMember(3)]
            public Vector3D Position { get; set; }

            [ProtoMember(4)]
            public Color Color { get; set; }

            [ProtoMember(5)]
            public double Duration { get; set; }

            [ProtoMember(6)]
            public double Radius { get; set; }

            [ProtoMember(7)]
            public long EntityId { get; set; }

            [ProtoMember(8)]
            public bool Mute { get; set; }

            [ProtoMember(9)]
            public string Description { get; set; }
        }
    }
}
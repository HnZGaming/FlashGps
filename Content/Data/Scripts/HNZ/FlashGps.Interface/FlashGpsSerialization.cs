using System.IO;
using Sandbox.ModAPI;

namespace HNZ.FlashGps.Interface
{
    public static class FlashGpsSerialization
    {
        public static void WriteUpsertFlashGps(this BinaryWriter writer, long moduleId, FlashGpsSource src)
        {
            // optimize network load
            src.ExcludedPlayers = null;
            src.TargetPlayers = null;

            writer.Write(true);
            writer.Write(moduleId);
            writer.WriteProtobuf(src);
        }

        public static void WriteRemoveFlashGps(this BinaryWriter writer, long moduleId, long gpsId)
        {
            writer.Write(false);
            writer.Write(moduleId);
            writer.Write(gpsId);
        }

        public static void ReadFlashGps(this BinaryReader reader, out bool isUpsert, out long moduleId, out FlashGpsSource source, out long gpsId)
        {
            if (reader.ReadBoolean())
            {
                isUpsert = true;
                moduleId = reader.ReadInt64();
                source = reader.ReadProtobuf<FlashGpsSource>();
                gpsId = source.Id;
            }
            else
            {
                isUpsert = false;
                moduleId = reader.ReadInt64();
                gpsId = reader.ReadInt64();
                source = null;
            }
        }

        static T ReadProtobuf<T>(this BinaryReader self)
        {
            var length = self.ReadInt32();
            var load = self.ReadBytes(length);
            var content = MyAPIGateway.Utilities.SerializeFromBinary<T>(load);
            return content;
        }

        static void WriteProtobuf<T>(this BinaryWriter self, T content)
        {
            var load = MyAPIGateway.Utilities.SerializeToBinary(content);
            self.Write(load.Length);
            self.Write(load);
        }
    }
}
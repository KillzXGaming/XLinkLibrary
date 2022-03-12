using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core.IO;

namespace XLinkLibrary
{
    public class ParamDefineTable
    {
        public List<ParamDefineEntry> UserParams = new List<ParamDefineEntry>();
        public List<ParamDefineEntry> AssetParams = new List<ParamDefineEntry>();
        public List<ParamDefineEntry> TriggerParams = new List<ParamDefineEntry>();

        public void Read(FileReader reader)
        {
            long pos = reader.Position;

            uint SectionSize = reader.ReadUInt32();
            uint numUserParams = reader.ReadUInt32();
            uint numAssetParams = reader.ReadUInt32();
            uint unknown = reader.ReadUInt32();
            uint numTriggerParams = reader.ReadUInt32();

            uint nameTblPos = (uint)reader.Position + ((numUserParams + numAssetParams + numTriggerParams) * 12);

            for (int i = 0; i < numUserParams; i++)
            {
                var entry = new ParamDefineEntry();
                entry.Read(reader, nameTblPos);
                UserParams.Add(entry);
            }
            for (int i = 0; i < numAssetParams; i++)
            {
                var entry = new ParamDefineEntry();
                entry.Read(reader, nameTblPos);
                AssetParams.Add(entry);
            }
            for (int i = 0; i < numTriggerParams; i++)
            {
                var entry = new ParamDefineEntry();
                entry.Read(reader, nameTblPos);
                TriggerParams.Add(entry);
            }

            reader.SeekBegin(pos + SectionSize);
        }

        public class ParamDefineEntry
        {
            public string Name { get; set; }

            public PropertyType Type { get; set; }

            public object DefaultValue { get; set; }

            public void Read(FileReader reader, uint nameTblPos)
            {
                uint NamePos = reader.ReadUInt32(); //Offset from string table
                Type = (PropertyType)reader.ReadUInt32();

                switch (Type)
                {
                    case PropertyType.Uint:
                    case PropertyType.Unknown:
                    case PropertyType.Enum:
                        DefaultValue = reader.ReadInt32();
                        break;
                    case PropertyType.Bool:
                        DefaultValue = reader.ReadInt32() != 0;
                        break;
                    case PropertyType.Float:
                        DefaultValue = reader.ReadSingle();
                        break;
                    case PropertyType.String:
                        uint defaultPos = reader.ReadUInt32();
                        using (reader.TemporarySeek(nameTblPos + defaultPos, System.IO.SeekOrigin.Begin))
                        {
                            DefaultValue = reader.ReadZeroTerminatedString();
                        }
                        break;
                    default:
                        throw new Exception($"Unknown param define type! {Type}");
                }

                using (reader.TemporarySeek(nameTblPos + NamePos, System.IO.SeekOrigin.Begin))
                {
                    Name = reader.ReadZeroTerminatedString();
                }
            }

            public enum PropertyType
            {
                Uint,
                Float,
                Bool,
                Enum,
                String,
                Unknown,
            }
        }
    }
}

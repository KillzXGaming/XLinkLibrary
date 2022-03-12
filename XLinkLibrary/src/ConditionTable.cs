using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core.IO;

namespace XLinkLibrary
{
    public class ConditionTable
    {
        public uint ParentContainerType { get; set; }
        public float Weight { get; set; }

        public uint PropertyType { get; set; }
        public uint CompareType { get; set; }
        public object Value { get; set; }
        public short LocalEnumIndex { get; set; }
        public byte IsSolved { get; set; }
        public byte IsGlobal { get; set; }

        public void Read(FileReader reader, XLink xlink)
        {
            ParentContainerType = reader.ReadUInt32();
            if (ParentContainerType == 1 || ParentContainerType == 2) {
                Weight = reader.ReadSingle();
            }
            else
            {
                PropertyType = reader.ReadUInt32();
                CompareType = reader.ReadUInt32();
                uint offset = reader.ReadUInt32(); 
                LocalEnumIndex = reader.ReadInt16();
                IsSolved = reader.ReadByte();
                IsGlobal = reader.ReadByte();

                if (LocalEnumIndex != -1)
                    Value = xlink.ReadNameTable(reader, offset);
                else
                    Value = offset;
            }
        }
    }
}

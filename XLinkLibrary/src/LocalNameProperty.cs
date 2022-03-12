using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Core.IO;

namespace XLinkLibrary
{
    public class LocalNameProperty
    {
        public string Name;

        public void Read(FileReader reader, uint nameTableOffset)
        {
            uint offset = reader.ReadUInt32();
            long pos = reader.Position;

            reader.SeekBegin(offset + nameTableOffset);

            Name = reader.ReadZeroTerminatedString(Encoding.UTF8);
            reader.SeekBegin(pos);
        }

        public override string ToString() => Name;
    }
}

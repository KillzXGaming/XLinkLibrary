using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Toolbox.Core.IO;
using Toolbox.Core.Hashes.Cryptography;

namespace XLinkLibrary
{
    /// <summary>
    /// Represents a user data table present in an XLink file binary.
    /// </summary>
    public class UserDataTable
    {
        /// <summary>
        /// CRC32 hashes representing the names of the user data headers.
        /// </summary>
        public uint[] CRC32Hashes;

        /// <summary>
        /// An array of user data headers for storing effect parameters, properties, actions and triggers.
        /// </summary>
        public UserDataHeader[] UserData;

        private Dictionary<uint, string> hashes = new Dictionary<uint, string>();

        public void Read(FileReader reader, XLink xlink, int EntryCount)
        {
            CRC32Hashes = reader.ReadUInt32s(EntryCount);
            UserData = new UserDataHeader[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                uint offset = reader.ReadUInt32();
                using (reader.TemporarySeek(offset, System.IO.SeekOrigin.Begin)) {
                    UserData[i] = new UserDataHeader(reader, xlink);
                    UserData[i].Name = FindName(CRC32Hashes[i]);
                }
            }
        }

        private void UpdateHashList()
        {
            using (var stream = new System.IO.StreamReader(new System.IO.MemoryStream((Properties.Resources.Hashes))))
            {
                while (!stream.EndOfStream)
                {
                    string str = stream.ReadLine();
                    var hash = Crc32.Compute(str);
                    if (!hashes.ContainsKey(hash))
                        hashes.Add(hash, str);
                }
            }
        }

        private string FindName(uint hash)
        {
            if (hashes.Count == 0) UpdateHashList();

            if (hashes.ContainsKey(hash))
                return hashes[hash];

            return hash.ToString();
        }

        /// <summary>
        /// User data header storing effect parameters, properties, actions and triggers.
        /// </summary>
        public class UserDataHeader
        {
            public string Name { get; set; }

            public uint IsSetup { get; set; }

            public ushort[] SortedAssetIDTable { get; set; }
            public string[] LocalProperyRefNameTable { get; set; }

            public AssetCallTable[] AssetCallTable { get; set; }
            public ContainerTable[] ContainerTable { get; set; }
            public ResActionSlotTable[] ActionSlots { get; set; }
            public ResActionTable[] Actions { get; set; }
            public ResActionTriggerTable[] ActionTriggers { get; set; }
            public ResPropertyTable[] Properties { get; set; }
            public ResPropertyTriggerTable[] PropertyTriggers { get; set; }
            public ResPropertyAlwaysTriggerTable[] PropertyAlwaysTriggers { get; set; }

            public UserDataHeader(FileReader reader, XLink xlink)
            {
                long pos = reader.Position;

                IsSetup = reader.ReadUInt32();
                uint numLocalProperty = reader.ReadUInt32();
                uint numCallTable = reader.ReadUInt32();
                uint numAsset = reader.ReadUInt32();
                uint numRandomContainer = reader.ReadUInt32(); //Todo find where this is used
                uint numResActionSlot = reader.ReadUInt32();
                uint numResAction = reader.ReadUInt32();
                uint numResActionTrigger = reader.ReadUInt32();
                uint numResPropery = reader.ReadUInt32();
                uint numResProperyTrigger = reader.ReadUInt32();
                uint numResAlwaysProperyTrigger = reader.ReadUInt32();
                uint triggerTablePos = reader.ReadUInt32();
                uint numContainerEntries = numCallTable - numAsset;

                LocalProperyRefNameTable = new string[numLocalProperty];
                for (int i = 0; i  < numLocalProperty; i++) {
                    LocalProperyRefNameTable[i] = xlink.ReadNameTable(reader);
                }

                reader.ReadBytes(4); // BOTW ELINK 
                // BOTW SLINK reader.ReadBytes(32);

                // reader.ReadBytes(40);// MK8 SLINK

                SortedAssetIDTable = reader.ReadUInt16s((int)numCallTable);
                reader.Align(4);

                AssetCallTable = ReadSectionList<AssetCallTable>(reader, xlink, numCallTable);
                ContainerTable = ReadSectionList<ContainerTable>(reader, xlink, numContainerEntries);

                //This offset points to the current reader position but use it anyways.
                reader.SeekBegin(pos + triggerTablePos);

                ActionSlots = ReadSectionList<ResActionSlotTable>(reader, xlink, numResActionSlot);
                Actions = ReadSectionList<ResActionTable>(reader, xlink, numResAction);
                ActionTriggers = ReadSectionList<ResActionTriggerTable>(reader, xlink, numResActionTrigger);
                Properties = ReadSectionList<ResPropertyTable>(reader, xlink, numResPropery);
                PropertyTriggers = ReadSectionList<ResPropertyTriggerTable>(reader, xlink, numResProperyTrigger);
                PropertyAlwaysTriggers = ReadSectionList<ResPropertyAlwaysTriggerTable>(reader, xlink, numResAlwaysProperyTrigger);
            }

            public ContainerTable GetContainer(int assetIndex)
            {
                for (int i = 0; i < ContainerTable.Length; i++)
                {
                    if (assetIndex >= ContainerTable[i].ChildrenStartIndex &&
                        assetIndex <= ContainerTable[i].ChildrenEndIndex)
                        return ContainerTable[i];
                }
                return null;
            }
        }

        public class AssetCallTable : ISection
        {
            public string Name { get; set; }

            public short AssetID { get; set; }
            public ushort Flag { get; set; }
            public int Field8 { get; set; }
            public int ParentIndex { get; set; }

            public uint NameHash { get; set; }

            public uint paramStartPos;
            public uint conditionPos;

            public void Read(FileReader reader, XLink xlink)
            {
                Name = xlink.ReadNameTable(reader);
                AssetID = reader.ReadInt16();
                Flag = reader.ReadUInt16();
                Field8 = reader.ReadInt32();
                ParentIndex = reader.ReadInt32();
                reader.ReadUInt32();
                NameHash = reader.ReadUInt32();
                paramStartPos = reader.ReadUInt32();
                conditionPos = reader.ReadUInt32();
            }
        }

        public class ContainerTable : ISection
        {
            public uint Type { get; set; }

            //The start/end index of the asset call table entry
            public int ChildrenStartIndex { get; set; }
            public int ChildrenEndIndex { get; set; }

            public string WatchPropertyName { get; set; }
            public int WatchPropertyID { get; set; }
            public int ID { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                Type = reader.ReadUInt32();
                if (Type != 0)
                {
                    ChildrenStartIndex = reader.ReadInt32();
                    ChildrenEndIndex = reader.ReadInt32();
                }
                else
                {
                    ChildrenStartIndex = reader.ReadInt32();
                    ChildrenEndIndex = reader.ReadInt32();
                    WatchPropertyName = xlink.ReadNameTable(reader);
                    WatchPropertyID = reader.ReadInt32();
                    ID = reader.ReadInt32();
                }
            }
        }

        public class ResActionSlotTable : ISection
        {
            public string Name { get; set; }
            public ushort StartIdx { get; set; }
            public ushort EndIdx { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                Name = xlink.ReadNameTable(reader);
                StartIdx = reader.ReadUInt16();
                EndIdx = reader.ReadUInt16();
            }
        }

        public class ResActionTable : ISection
        {
            public string Name { get; set; }
            public uint TriggerStartIdx { get; set; }
            public uint TriggerEndIdx { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                Name = xlink.ReadNameTable(reader);
                TriggerStartIdx = reader.ReadUInt32();
                TriggerEndIdx = reader.ReadUInt32();
            }
        }

        public class ResTriggerBase
        {
            public uint GuId { get; set; }
            public uint AssetCtbPos { get; set; }
            public uint Flag { get; set; }
            public uint overrideParamPos { get; set; }
        }

        public class ResActionTriggerTable : ResTriggerBase, ISection
        {
            public uint StartFrame { get; set; }
            public uint EndFrame { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                GuId = reader.ReadUInt32();
                AssetCtbPos = reader.ReadUInt32();
                StartFrame = reader.ReadUInt32();
                EndFrame = reader.ReadUInt32();
                Flag = reader.ReadUInt32();
                overrideParamPos = reader.ReadUInt32();
            }
        }

        public class ResPropertyTable : ISection
        {
            public string Name { get; set; }
            public uint IsGlobal { get; set; }
            public uint TriggerStartIdx { get; set; }
            public uint TriggerEndIdx { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                Name = xlink.ReadNameTable(reader);
                IsGlobal = reader.ReadUInt32();
                TriggerStartIdx = reader.ReadUInt32();
                TriggerEndIdx = reader.ReadUInt32();
            }
        }

        public class ResPropertyTriggerTable : ResTriggerBase, ISection
        {
            public uint Condition { get; set; }

            public void Read(FileReader reader, XLink xlink)
            {
                GuId = reader.ReadUInt32();
                AssetCtbPos = reader.ReadUInt32();
                Condition = reader.ReadUInt32();
                Flag = reader.ReadUInt32();
                overrideParamPos = reader.ReadUInt32();
            }
        }

        public class ResPropertyAlwaysTriggerTable : ResTriggerBase, ISection
        {
            public void Read(FileReader reader, XLink xlink)
            {
                GuId = reader.ReadUInt32();
                AssetCtbPos = reader.ReadUInt32();
                Flag = reader.ReadUInt32();
                overrideParamPos = reader.ReadUInt32();
            }
        }

        static T[] ReadSectionList<T>(FileReader reader, XLink xlink, uint count) where T : ISection
        {
            T[] array = new T[(int)count];
            for (int i = 0; i < count; i++)
            {
                T instance = (T)Activator.CreateInstance(typeof(T));
                instance.Read(reader, xlink);
                array[i] = instance;
            }
            return array;
        }

        interface ISection
        {
            void Read(FileReader reader, XLink xlink);
        }
    }
}
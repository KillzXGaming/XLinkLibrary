using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Toolbox.Core.IO;
using Newtonsoft.Json;

namespace XLinkLibrary
{
    public class XLink
    {
        public ushort ByteOrderMark;

        public List<UserEntry> Entries = new List<UserEntry>();

        /// <summary>
        /// The resource used to store xlink data.
        /// </summary>
        public class UserEntry
        {
            public string Name { get; set; }

            public List<AssetEntry> Assets = new List<AssetEntry>();
            public Dictionary<string, ActionSlot> ActionSlots = new Dictionary<string, ActionSlot>();
            public Dictionary<string, Property> Properties = new Dictionary<string, Property>();

            public List<TriggerEntry> AlwaysTriggers = new List<TriggerEntry>();

            public static UserEntry Import(string fileName)
            {
                return JsonConvert.DeserializeObject<UserEntry>(File.ReadAllText(fileName));
            }

            public void Export(string fileName)
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(fileName, json);
            }
        }

        /// <summary>
        /// The asset of the resource used to link effects and sounds.
        /// </summary>
        public class AssetEntry
        {
            public string Name { get; set; }

            public int ParentIndex { get; set; }

            [JsonProperty(ItemConverterType = typeof(NoFormattingConverter))]
            public Dictionary<string, object> Parameters = new Dictionary<string, object>();

            public ConditionEntry Condition;

            public List<AssetEntry> Children = new List<AssetEntry>();
        }

        /// <summary>
        /// Represents a trigger with a list of properties.
        /// </summary>
        public class TriggerEntry
        {
            public string Name { get; set; }

            [JsonProperty(ItemConverterType = typeof(NoFormattingConverter))]
            public Dictionary<string, object> Parameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Represents a trigger with a start and frame.
        /// </summary>
        public class TriggerActionEntry : TriggerEntry
        {
            public float StartFrame { get; set; }
            public float EndFrame { get; set; }
        }

        /// <summary>
        /// Represents a condition generally used for enumerative values.
        /// </summary>
        public class ConditionEntry
        {
            //Container properties
            public string Name { get; set; }
            public object Value { get; set; }

            public string WatchPropertyID { get; set; }
            public int ID { get; set; }
            public int Type { get; set; }

            //Condition properties
            public float Weight { get; set; }

            public bool IsSolved { get; set; }
            public bool IsGlobal { get; set; }

            public ConditionEntry()
            {

            }
        }


        /// <summary>
        /// Represents a list of actions used by a resource.
        /// </summary>
        public class ActionSlot
        {
            public Dictionary<string, Action> Actions = new Dictionary<string, Action>();
        }

        /// <summary>
        /// Represents an action containing triggers.
        /// </summary>
        public class Action
        {
            public List<TriggerEntry> Triggers = new List<TriggerEntry>();
        }

        /// <summary>
        /// Represents a property containing triggers.
        /// </summary>
        public class Property
        {
            public List<TriggerEntry> Triggers = new List<TriggerEntry>();
        }

        /// <summary>
        /// The raw user data table.
        /// </summary>
        public UserDataTable UserDataTable;

        /// <summary>
        /// The raw param table.
        /// </summary>
        public ParamDefineTable ParamDefineTable;

        /// <summary>
        /// A list of strings used for resources and assets.
        /// </summary>
        public List<LocalNameProperty> LocalNameProperties = new List<LocalNameProperty>();

        /// <summary>
        /// A list of enum strings used for conditions.
        /// </summary>
        public List<LocalNameProperty> LocalNameEnumProperties = new List<LocalNameProperty>();

        internal uint nameTablePos;

        public UserStructure VersionStruct = UserStructure.ELinkNormal;

        public enum UserStructure
        {
            ELinkNormal,
            SLinkNormal,
            SLinkBOTW,
            ELinkBOTW,
        }

        public XLink(string fileName, UserStructure readMethod, bool bigEndian) {
            VersionStruct = readMethod;
            using (var reader = new FileReader(fileName)) {
                Read(reader, bigEndian);
            }
        }

        public XLink(Stream stream, UserStructure readMethod, bool bigEndian) {
            VersionStruct = readMethod;
            using (var reader = new FileReader(stream)) {
                Read(reader, bigEndian);
            }
        }

        void Read(FileReader reader, bool bigEndian)
        {
            if (bigEndian)
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
            reader.ReadSignature(4, "XLNK");
            uint FileSize = reader.ReadUInt32();
            uint Version = reader.ReadUInt32();
            uint numResParam = reader.ReadUInt32();
            uint numResAssetParam = reader.ReadUInt32();
            uint numResTriggerOverwriteParam = reader.ReadUInt32();
            uint triggerOverwriteParamTablePos = reader.ReadUInt32();
            uint localPropertyNameRefTablePos = reader.ReadUInt32();
            uint numLocalPropertyNameRefTable = reader.ReadUInt32();
            uint numLocalPropertyEnumNameRefTable = reader.ReadUInt32();
            uint numDirectValueTable = reader.ReadUInt32();
            uint numRandomTable = reader.ReadUInt32();
            uint numCurveTable = reader.ReadUInt32();
            uint numCurvePointTable = reader.ReadUInt32();
            uint exRegionPos = reader.ReadUInt32();
            uint numUser = reader.ReadUInt32();
            uint conditionTablePos = reader.ReadUInt32();
            nameTablePos = reader.ReadUInt32();

            UserDataTable = new UserDataTable();
            UserDataTable.Read(reader, this, (int)numUser);

            ParamDefineTable = new ParamDefineTable();
            ParamDefineTable.Read(reader);

            //Asset param table in this spot after param define table.
            uint assetParamTable = (uint)reader.Position;

            //Read name tables
            //It is atleast important to read these sections as the direct value table is afterwards

            reader.SeekBegin(localPropertyNameRefTablePos);
            for (int i = 0; i < numLocalPropertyNameRefTable; i++)
            {
                var localNameProp = new LocalNameProperty();
                localNameProp.Read(reader, nameTablePos);
                LocalNameProperties.Add(localNameProp);
            }

            for (int i = 0; i < numLocalPropertyEnumNameRefTable; i++)
            {
                var localNameProp = new LocalNameProperty();
                localNameProp.Read(reader, nameTablePos);
                LocalNameEnumProperties.Add(localNameProp);
            }

            //Direct value table in this spot
            uint directTablePos = (uint)reader.Position;

            //Add the actual xlink data into the user entries for accessing
            foreach (var usd in UserDataTable.UserData)
            {
                UserEntry entry = new UserEntry();
                entry.Name = usd.Name;
                Entries.Add(entry);

                AssetEntry[] assets = new AssetEntry[usd.SortedAssetIDTable.Length];

                //Order by the original sort order. XLink orders by hashes on compile.
                foreach (var assetIdx in usd.SortedAssetIDTable)
                {
                    var asset = usd.AssetCallTable[assetIdx];
                
                    var data = SolveAssetParameter(reader, assetParamTable, directTablePos, nameTablePos, asset.paramStartPos);
                    var assetEntry = new AssetEntry();
                    assetEntry.Name = asset.Name;
                    assetEntry.ParentIndex = asset.ParentIndex;
                    for (int i = 0; i < ParamDefineTable.AssetParams.Count; i++)
                        assetEntry.Parameters.Add(ParamDefineTable.AssetParams[i].Name, data[i]);

                    if (asset.conditionPos != uint.MaxValue)
                    {
                        using (reader.TemporarySeek(conditionTablePos + asset.conditionPos, SeekOrigin.Begin))
                        {
                            var condTable = new ConditionTable();
                            condTable.Read(reader, this);

                            var container = usd.GetContainer(assetIdx);
                            if (container != null)
                            {
                                var cond = new ConditionEntry();
                                cond.Type = (int)container.Type;
                                cond.Weight = condTable.Weight;
                                cond.Name = container.WatchPropertyName;
                                cond.Value = condTable.Value;
                                cond.ID = container.ID;
                                cond.IsGlobal = condTable.IsGlobal == 1;
                                cond.IsSolved = condTable.IsSolved == 1;
                                assetEntry.Condition = cond;
                            }
                        }
                    }

                    assets[assetIdx] = assetEntry;
                }
                foreach (var assetIdx in usd.SortedAssetIDTable)
                {
                    var asset = usd.AssetCallTable[assetIdx];
                    if (asset.ParentIndex == -1)
                        entry.Assets.Add(assets[assetIdx]);
                    else
                        assets[asset.ParentIndex].Children.Add(assets[assetIdx]);
                }

                var actionTriggers = GetSolvedTriggers(usd.ActionTriggers, reader, triggerOverwriteParamTablePos, directTablePos, nameTablePos);
                var propTriggers = GetSolvedTriggers(usd.PropertyTriggers, reader, triggerOverwriteParamTablePos, directTablePos, nameTablePos);
                entry.AlwaysTriggers.AddRange(GetSolvedTriggers(usd.PropertyAlwaysTriggers, reader, triggerOverwriteParamTablePos, directTablePos, nameTablePos));

                foreach (var property in usd.Properties)
                {
                    Property prop = new Property();
                    for (uint i = property.TriggerStartIdx; i <= property.TriggerEndIdx; i++) {
                        prop.Triggers.Add(propTriggers[(int)i]);
                    }
                    entry.Properties.Add(property.Name, prop);
                }

                foreach (var actionSlot in usd.ActionSlots)
                {
                    ActionSlot slot = new ActionSlot();
                    entry.ActionSlots.Add(actionSlot.Name, slot);

                    for (uint i = actionSlot.StartIdx; i <= actionSlot.EndIdx; i++)
                    {
                        var actionEntry = new Action();
                        slot.Actions.Add(usd.Actions[i].Name, actionEntry);

                        for (uint j = usd.Actions[i].TriggerStartIdx; j <= usd.Actions[i].TriggerEndIdx; j++)
                        {
                            actionEntry.Triggers.Add(actionTriggers[(int)j]);
                        }
                    }
                }
            }
        }

        private List<TriggerEntry> GetSolvedTriggers(IEnumerable<UserDataTable.ResTriggerBase> triggers, FileReader reader, uint paramOfs, uint directTablePos, uint nameTablePosn)
        {
            List<TriggerEntry> list = new List<TriggerEntry>();
            foreach (var asset in triggers)
            {
                var data = SolveTriggerParameter(reader, paramOfs, directTablePos, nameTablePos, asset.overrideParamPos);
                var assetEntry = new TriggerEntry();
                if (asset is UserDataTable.ResActionTriggerTable) {
                    var actionTrigger = asset as UserDataTable.ResActionTriggerTable;
                    assetEntry = new TriggerActionEntry()
                    {
                        StartFrame = actionTrigger.StartFrame,
                        EndFrame = actionTrigger.EndFrame,
                    };
                }
                assetEntry.Name = asset.GuId.ToString();
                for (int i = 0; i < ParamDefineTable.TriggerParams.Count; i++)
                    assetEntry.Parameters.Add(ParamDefineTable.TriggerParams[i].Name, data[i]);

                list.Add(assetEntry);
            }
            return list;
        }

        public object[] SolveAssetParameter(FileReader reader, uint paramTablePos, uint directTablePos, uint nameTablePos, uint tablePos)
        {
            object[] values = new object[ParamDefineTable.AssetParams.Count];
            for (int i = 0; i < ParamDefineTable.AssetParams.Count; i++)
                values[i] = ParamDefineTable.AssetParams[i].DefaultValue;

            if (tablePos == uint.MaxValue)
                return values;

            reader.SeekBegin(paramTablePos + tablePos);
            ulong Mask = reader.ReadUInt64();

            long pos = reader.Position;
            for (int i = 0; i < ParamDefineTable.AssetParams.Count; i++)
            {
                var paramInfo = SolveParamValue(reader, directTablePos, nameTablePos, pos, Mask, ParamDefineTable.AssetParams[i], i);
                values[i] = paramInfo.Item2;
            }

            return values;
        }

        public object[] SolveTriggerParameter(FileReader reader, uint paramTablePos, uint directTablePos, uint nameTablePos, uint tablePos)
        {
            object[] values = new object[ParamDefineTable.TriggerParams.Count];
            for (int i = 0; i < ParamDefineTable.TriggerParams.Count; i++)
                values[i] = ParamDefineTable.TriggerParams[i].DefaultValue;

            if (tablePos == uint.MaxValue)
                return values;

            reader.SeekBegin(paramTablePos + tablePos);
            uint Mask = reader.ReadUInt32();

            long pos = reader.Position;
            for (int i = 0; i < ParamDefineTable.TriggerParams.Count; i++)
            {
                var paramInfo = SolveParamValue(reader, directTablePos, nameTablePos, pos, Mask, ParamDefineTable.TriggerParams[i], i, false);
                values[i] = paramInfo.Item2;
            }

            return values;
        }

        private Tuple<uint, object> SolveParamValue(FileReader reader, uint directTablePos, uint nameTablePos, 
            long pos, ulong mask, ParamDefineTable.ParamDefineEntry param, int index, bool isUint64 = true)
        {
            var bitFlag = (long)mask & (1L << index);
            if (!isUint64)
                 bitFlag = (uint)mask & (1 << index);

            //If the bit flag is 0, use default value
            if (bitFlag == 0)
                return Tuple.Create(0U, param.DefaultValue);

            int bitPos = 0;
            if (!isUint64)
                bitPos = BitFlag.CountRightOnBit((uint)mask, index);
            else
                bitPos = BitFlag.CountRightOnBit64(mask, index);

            //Read the reference value
            reader.SeekBegin(pos + (4 * bitPos) - 4);
            var reference = reader.ReadUInt32();

            int offset = (int)(reference & 0xffffff);
            var type = (reference >> 24);

            if (type > 5 || type == 4)
                return Tuple.Create(0U, param.DefaultValue);

            object value = null;

            if (directTablePos + (offset * 4) > reader.BaseStream.Length)
                return Tuple.Create(0U, param.DefaultValue);

            //Seek to the direct table if type is not string/enum
            if ((int)param.Type <= 3)
                reader.SeekBegin(directTablePos + (offset * 4));

            switch (param.Type)
            {
                case ParamDefineTable.ParamDefineEntry.PropertyType.Float:
                    value = reader.ReadSingle();
                    break;
                case ParamDefineTable.ParamDefineEntry.PropertyType.Uint:
                    value = reader.ReadUInt32();
                    break;
                case ParamDefineTable.ParamDefineEntry.PropertyType.Bool:
                    value = reader.ReadUInt32() != 0;
                    break;
                case ParamDefineTable.ParamDefineEntry.PropertyType.String:
                    reader.SeekBegin(nameTablePos + offset);
                    value = reader.ReadZeroTerminatedString(Encoding.UTF8);
                    break;
                case ParamDefineTable.ParamDefineEntry.PropertyType.Enum:
                    if (type == 0)
                        value = reader.ReadUInt32();
                    if (type == 1)
                    {
                        reader.SeekBegin(nameTablePos + offset);
                        value = reader.ReadZeroTerminatedString(Encoding.UTF8);
                    }
                    break;

            }

            if (!isUint64)
                Console.WriteLine($"{param.Name} {value}");

            return Tuple.Create(type, value);
        }

        public string ReadNameTable(FileReader reader)
        {
            uint offset = reader.ReadUInt32();
            using (reader.TemporarySeek(nameTablePos + offset, SeekOrigin.Begin)) {
                return reader.ReadZeroTerminatedString(Encoding.UTF8);
            }
        }

        public string ReadNameTable(FileReader reader, uint offset)
        {
            using (reader.TemporarySeek(nameTablePos + offset, SeekOrigin.Begin))
            {
                return reader.ReadZeroTerminatedString(Encoding.UTF8);
            }
        }
    }
}

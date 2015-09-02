using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using GreyMagic;

namespace HighVoltz.HBRelog.WoW.Lua
{
    public class LuaTable
    {
        private readonly LuaTableStuct _luaTable;

        private readonly ExternalProcessReader _memory;

        public LuaTable(ExternalProcessReader memory, IntPtr address)
        {
            Address = address;
            _memory = memory;
            _luaTable = _memory.Read<LuaTableStuct>(address);
        }

        public byte Flags { get { return _luaTable.Flags; } }

        public readonly IntPtr Address;

        public uint NodeCount { get { return _luaTable.NodesCount; } }

        public uint ValueCount { get { return _luaTable.ValueCount; } }

        private bool _triedGetMetaTable;
        private LuaTable _metaTable;
        public LuaTable MetaTable
        {
            get
            {
                if (!_triedGetMetaTable)
                {
                    _metaTable = (_luaTable.MetaTablePtr != IntPtr.Zero ? new LuaTable(_memory, _luaTable.MetaTablePtr) : null);
                    _triedGetMetaTable = true;
                }
                return _metaTable;
            }
        }

        private static uint H(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            uint length = (uint)str.Length;
            uint num2 = (length >> 5) + 1;
            for (uint i = length; i >= num2; i -= num2)
            {
                length ^= ((length << 5) + (length >> 2)) + bytes[i - 1];
            }
            return length;
        }


        private LuaNode GetNodeAtIndex(uint idx)
        {
            return new LuaNode(_memory, _luaTable.NodePtr + (int)(LuaNode.Size * idx));
        }



        public LuaTValue GetValue(string key)
        {
            var num = H(key);
            LuaNode next = GetNodeAtIndex(num & (NodeCount - 1));
            while ((next.Key.Type != LuaType.String) || !string.Equals(key, next.Key.Value.String.Value))
            {
                next = next.Key.Next;
                if (next == null)
                {
                    return null;
                }
            }
            return next.Value;
        }

        public IEnumerable<LuaNode> Nodes
        {
            get
            {
                if (_luaTable.NodePtr == IntPtr.Zero)
                    yield break;

                for (uint i = 0; i < NodeCount; i++)
                {
                    var node = GetNodeAtIndex(i);
                    if (!node.IsValid)
                        continue;
                    yield return node;
                }

            }
        }

        #region Embedded Type: LuaTableStruct

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct LuaTableStuct
        {
            public readonly LuaCommonHeader Header;
            public readonly byte Flags; /* 1<<p means tagmethod(p) is not present */
            private readonly byte Log2Sizenode; /* log2 of size of `node' array */
            public readonly IntPtr MetaTablePtr;
            public readonly IntPtr ValuesPtr;
            public readonly IntPtr NodePtr;
            private readonly IntPtr lastFree; /* any free position is before this position */
            private readonly IntPtr gclist;
            public readonly uint ValueCount;

            public uint NodesCount
            {
                get { return 1u << Log2Sizenode; }
            }
        }

        #endregion


    }
}

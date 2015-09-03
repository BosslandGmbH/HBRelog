using System;
using System.Runtime.InteropServices;

namespace WowClient.Lua
{
    public class LuaNode
    {
        private LuaNodeStruct _luaNode;

        private readonly IReadOnlyMemory _memory;
        public LuaNode(IReadOnlyMemory memory, IAbsoluteAddress address)
        {
            Address = address;
            _memory = memory;
            _luaNode = memory.Read<LuaNodeStruct>(address);
        }

        public bool IsValid { get { return Address.Value != IntPtr.Zero; } }

        private LuaTKey _key;
        public LuaTKey Key { get { return _key ?? (_key = new LuaTKey(_memory, _luaNode.Key)); } }

        public LuaNode Next { get { return Key.Next; } }

        private LuaTValue _value;
        public LuaTValue Value { get { return _value ?? (_value = new LuaTValue(_memory, _luaNode.Value)); } }

        public readonly IAbsoluteAddress Address;
        public const uint Size = 40;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct LuaNodeStruct
        {
            public LuaTValueStruct Value;
            public LuaTKeyStruct Key;
        }
    }
}

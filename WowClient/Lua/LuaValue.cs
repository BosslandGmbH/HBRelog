using System;

namespace WowClient.Lua
{
    public class LuaValue
    {
        private LuaValueStruct _luaValue;
        private readonly IReadOnlyMemory _memory;

        internal LuaValue(IReadOnlyMemory memory, LuaValueStruct luaValue)
        {
            _luaValue = luaValue;
            _memory = memory;
        }

        public double Number
        {
            get { return _luaValue.Number; }
        }

        public IntPtr Pointer
        {
            get { return _luaValue.Pointer; }
        }

        public bool Boolean
        {
            get { return _luaValue.Boolean != 0; }
        }

        private LuaTable _table;
        public LuaTable Table
        {
            get { return _table ??
                (_table = new LuaTable(_memory, _memory.GetAbsoluteAddress(_luaValue.Pointer))); }
        }

        private LuaTString _string;
        public LuaTString String
        {
            get { return _string ??
                (_string = new LuaTString(_memory, _memory.GetAbsoluteAddress(_luaValue.Pointer))); }
        }
    }
}

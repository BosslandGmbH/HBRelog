using System;
using GreyMagic;

namespace HighVoltz.HBRelog.WoW.Lua
{
    public class LuaValue
    {
        private LuaValueStruct _luaValue;
        private readonly ExternalProcessReader _memory;

        internal LuaValue(ExternalProcessReader memory, LuaValueStruct luaValue)
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
            get { return _table ?? (_table = new LuaTable(_memory, _luaValue.Pointer)); }
        }

        private LuaTString _string;
        public LuaTString String
        {
            get { return _string ?? (_string = new LuaTString(_memory, _luaValue.Pointer)); }
        }
    }
}

using System;

namespace WowClient.Lua
{
    public class LuaTValue
    {
        private LuaTValueStruct _luaTValue;

        private readonly IReadOnlyMemory _memory;

        public LuaTValue(IReadOnlyMemory memory, LuaTValueStruct luaTValue)
        {
            _luaTValue = luaTValue;
            _memory = memory;
        }

        public LuaType Type
        {
            get { return _luaTValue.Type; }
        }

        private LuaValue _value;
        public LuaValue Value
        {
            get { return _value ?? (_value = new LuaValue(_memory, _luaTValue.Value)); }
        }

        public double Number
        {
            get { return _luaTValue.Value.Number; }
        }

        public IAbsoluteAddress Pointer
        {
            get { return _luaTValue.Value.Pointer != IntPtr.Zero ?
                _memory.GetAbsoluteAddress(_luaTValue.Value.Pointer) :
                null; }
        }

        public bool Boolean
        {
            get { return _luaTValue.Value.Boolean != 0; }
        }

        private LuaTable _table;
        public LuaTable Table
        {
            get { return _table ?? (_table = new LuaTable(_memory, Pointer)); }
        }

        private LuaTString _string;
        public LuaTString String
        {
            get { return _string ?? (_string = new LuaTString(_memory, Pointer)); }
        }

    }
}

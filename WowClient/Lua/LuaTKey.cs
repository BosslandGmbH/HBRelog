using System;

namespace WowClient.Lua
{
    public class LuaTKey
    {
        private LuaTKeyStruct _luaTKeyStruct;

        private readonly IReadOnlyMemory _memory;

        public LuaTKey(IReadOnlyMemory memory, LuaTKeyStruct luaTKeyStruct)
        {
            _luaTKeyStruct = luaTKeyStruct;
            _memory = memory;
        }

        public LuaNode Next
        {
            get
            {
                return _luaTKeyStruct.NextNodePtr != IntPtr.Zero ?
                    new LuaNode(_memory, _memory.GetAbsoluteAddress(_luaTKeyStruct.NextNodePtr)) :
                    null;
            }
        }

        private LuaValue _value;
        public LuaValue Value
        {
            get { return _value ?? (_value = new LuaValue(_memory, _luaTKeyStruct.Value)); }
        }

        public LuaType Type { get { return _luaTKeyStruct.Type;}}

    }
}

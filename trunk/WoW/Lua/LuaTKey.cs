using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GreyMagic;

namespace Test.Lua
{
    public class LuaTKey
    {
        private LuaTKeyStruct _luaTKeyStruct;

        private readonly ExternalProcessReader _memory;

        public LuaTKey(ExternalProcessReader memory, LuaTKeyStruct luaTKeyStruct)
        {
            _luaTKeyStruct = luaTKeyStruct;
            _memory = memory;
        }

        public LuaNode Next { get { return _luaTKeyStruct.NextNodePtr != IntPtr.Zero ? new LuaNode(_memory,_luaTKeyStruct.NextNodePtr) : null; } }

        private LuaValue _value;
        public LuaValue Value
        {
            get { return _value ?? (_value = new LuaValue(_memory, _luaTKeyStruct.Value)); }
        }

        public LuaType Type { get { return _luaTKeyStruct.Type;}}

    }
}

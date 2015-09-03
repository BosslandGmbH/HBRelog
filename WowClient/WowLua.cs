using System;
using WowClient.Lua;
using WowClient.Lua.UI;

namespace WowClient
{
    public class WowLua
    {
        internal const int LuaStateGlobalsOffset = 0x50;
        public IReadOnlyMemory Memory { get; set; }
        public string ActiveCharacterName { get; set; }
        public IRelativeAddress FocusedWidgetOffset { get; set; }
        public IRelativeAddress LuaStateOffset { get; set; }

        private IAbsoluteAddress _focusedWidgetPtr;
        public IAbsoluteAddress FocusedWidgetPtr
        {
            get
            {
                return _focusedWidgetPtr ??
                    (_focusedWidgetPtr = Memory == null ?
                        null :
                        FocusedWidgetOffset.Deref());
            }
        }

        public UIObject FocusedWidget
        {
            get
            {
                return FocusedWidgetPtr.Value != IntPtr.Zero ?
                    UIObject.Get(this, FocusedWidgetPtr) :
                    null;
            }
        }

        private LuaTable _globals;
        public LuaTable Globals
        {
            get
            {
                if (Memory == null)
                    return null;
                var luaStatePtr = LuaStateOffset.Deref();
                if (luaStatePtr.Value == IntPtr.Zero)
                    return null;
                var globalsAddress = luaStatePtr.Deref(LuaStateGlobalsOffset);
                if (globalsAddress.Value == IntPtr.Zero)
                    return null;
                if (_globals == null || _globals.Address != globalsAddress)
                    _globals = new LuaTable(Memory, globalsAddress);
                return _globals;
            }
            set { _globals = value; }
        }

    }

}

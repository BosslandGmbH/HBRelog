using System;
using System.Collections.Generic;
using System.Diagnostics;
using WowClient.Lua;
using WowClient.Lua.UI;

namespace WowClient
{
    public interface IWowProcess : IDisposable
    {
        UIObject FocusedWidget { get; }
        UIObject GetWidget(IAbsoluteAddress address);
        T GetWidget<T>(IAbsoluteAddress address) where T : UIObject;
        T GetWidget<T>(string name) where T : UIObject;
        IEnumerable<T> GetWidgets<T>() where T : UIObject;
        IEnumerable<UIObject> GetWidgets();
    }

    public class WowLua : IWowProcess
    {
        private IAbsoluteAddress GameStateAddress { get; set; }
        private IAbsoluteAddress LuaStateAddress { get; set; }
        private IAbsoluteAddress FocusedWidgetAddress { get; set; }
        private IAbsoluteAddress LoadingScreenEnableCountAddress { get; set; }
        private IAbsoluteAddress GlueStateAddress { get; set; }
        public WowLua(Process process)
        {
            try
            {
                Memory = new ReadOnlyMemory(process);
                GameStateAddress = Memory.FindPattern(WowPatterns.GameStatePattern).Deref(2);
                LuaStateAddress = Memory.FindPattern(WowPatterns.LuaStatePattern).Deref(2);
                FocusedWidgetAddress = Memory.FindPattern(WowPatterns.FocusedWidgetPattern).Deref(2);
                LoadingScreenEnableCountAddress = Memory.FindPattern(WowPatterns.LoadingScreenEnableCountPattern).Deref(2);
                GlueStateAddress = Memory.FindPattern(WowPatterns.GlueStatePattern).Deref(2);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
                throw new Exception("Could not initialize WowLua.");
            }
        }



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

        internal static class WowPatterns
        {
            public const string GlueStatePattern =
                "83 3d ?? ?? ?? ?? ?? 75 ?? e8 ?? ?? ?? ?? 8b 10 8b c8 ff 62 5c c3";
            public const string GameStatePattern =
                "80 3d ?? ?? ?? ?? ?? 74 ?? 50 b9 ?? ?? ?? ?? e8 ?? ?? ?? ?? 85 c0 74 ?? 8b 40 08 83 f8 02 74 ?? 83 f8 01 75 ?? b0 01 c3 32 c0 c3";
            // ref - FrameXML:EditBox:HasFocus
            public const string FocusedWidgetPattern =
                "3b 05 ?? ?? ?? ?? 0f 94 c1 51 ff 75 08 e8 ?? ?? ?? ?? 33 c0 83 c4 10 40 5d c3";
            // ref - Framescript_ExecuteBuffer.
            public const string LuaStatePattern =
                "8b 35 ?? ?? ?? ?? 33 db 57 3b c3 74 ?? 88 18 ff 75 08 8d 85 dc fe ff ff 68 ?? ?? ?? ?? 68 ?? ?? ?? ?? 50";
            // first offset used in 'LoadingScreenEnable' function. This function also fires the 'LOADING_SCREEN_ENABLED' lua event.
            public const string LoadingScreenEnableCountPattern =
                "ff 05 ?? ?? ?? ?? 83 3d ?? ?? ?? ?? ?? 53 56 57 0f 8f ?? ?? ?? ?? 6a 00 e8 ?? ?? ?? ?? 59 e8 ?? ?? ?? ?? 84 c0 74 ?? 6a 00 68";
        }

        public void Dispose()
        {
            Memory.Dispose();
        }

        public UIObject GetWidget(IAbsoluteAddress address)
        {
            return UIObject.Get(this, address);
        }

        public T GetWidget<T>(IAbsoluteAddress address) where T : UIObject
        {
            return UIObject.Get<T>(this, address);
        }

        public T GetWidget<T>(string name) where T : UIObject
        {
            return UIObject.Get<T>(this, name);
        }

        public IEnumerable<T> GetWidgets<T>() where T : UIObject
        {
            return UIObject.GetAll<T>(this);
        }

        public IEnumerable<UIObject> GetWidgets()
        {
            return UIObject.GetAll(this);
        }
    }
}

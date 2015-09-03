using System;
using System.Diagnostics;
using WowClient.Lua;
using WowClient.Lua.UI;

namespace WowClient
{
    public class WowLua
    {

        private IAbsoluteAddress GameStateOffset { get; set; }
        public WowLua(Process process)
        {
            var versionString = process.MainModule.FileVersionInfo.FileVersion;

            Memory = new ReadOnlyMemory(process);

            //GameStateOffset = (uint)WowPatterns.GameStatePattern.Find(_wowManager.LuaManager.Memory);
            //Memory.
            //Log.Debug("GameState Offset found at 0x{0:X}", HbRelogManager.Settings.GameStateOffset);

            //HbRelogManager.Settings.LuaStateOffset = (uint)WowPatterns.LuaStatePattern.Find(_wowManager.LuaManager.Memory);
            //Log.Debug("LuaState Offset found at 0x{0:X}", HbRelogManager.Settings.LuaStateOffset);

            //HbRelogManager.Settings.FocusedWidgetOffset = (uint)WowPatterns.FocusedWidgetPattern.Find(_wowManager.LuaManager.Memory);
            //Log.Debug("FocusedWidget Offset found at 0x{0:X}", HbRelogManager.Settings.FocusedWidgetOffset);

            //HbRelogManager.Settings.LoadingScreenEnableCountOffset = (uint)WowPatterns.LoadingScreenEnableCountPattern.Find(_wowManager.LuaManager.Memory);
            //Log.Debug("LoadingScreenEnableCountOffset Offset found at 0x{0:X}", HbRelogManager.Settings.LoadingScreenEnableCountOffset);

            //HbRelogManager.Settings.GlueStateOffset = (uint)WowPatterns.GlueStatePattern.Find(_wowManager.LuaManager.Memory);
            //Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HbRelogManager.Settings.GlueStateOffset);

            //HbRelogManager.Settings.WowVersion = versionString;
            //HbRelogManager.Settings.Save();
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

        //public static class WowPatterns
        //{
        //    public static readonly string GlueStatePattern =
        //        "83 3d ?? ?? ?? ?? ?? 75 ?? e8 ?? ?? ?? ?? 8b 10 8b c8 ff 62 5c c3";

        //    public static readonly Pattern GameStatePattern = Pattern.FromTextstyle(
        //        "GameState",
        //        "80 3d ?? ?? ?? ?? ?? 74 ?? 50 b9 ?? ?? ?? ?? e8 ?? ?? ?? ?? 85 c0 74 ?? 8b 40 08 83 f8 02 74 ?? 83 f8 01 75 ?? b0 01 c3 32 c0 c3",
        //        new AddModifier(2),
        //        new LeaModifier());

        //    // ref - FrameXML:EditBox:HasFocus
        //    public static readonly Pattern FocusedWidgetPattern = Pattern.FromTextstyle(
        //        "FocusedWidget", "3b 05 ?? ?? ?? ?? 0f 94 c1 51 ff 75 08 e8 ?? ?? ?? ?? 33 c0 83 c4 10 40 5d c3", new AddModifier(2), new LeaModifier());

        //    // ref - Framescript_ExecuteBuffer.
        //    public static readonly Pattern LuaStatePattern = Pattern.FromTextstyle(
        //        "LuaState",
        //        "8b 35 ?? ?? ?? ?? 33 db 57 3b c3 74 ?? 88 18 ff 75 08 8d 85 dc fe ff ff 68 ?? ?? ?? ?? 68 ?? ?? ?? ?? 50",
        //        new AddModifier(2),
        //        new LeaModifier());

        //    // first offset used in 'LoadingScreenEnable' function. This function also fires the 'LOADING_SCREEN_ENABLED' lua event.
        //    public static readonly Pattern LoadingScreenEnableCountPattern = Pattern.FromTextstyle(
        //        "LoadingScreenEnableCount",
        //        "ff 05 ?? ?? ?? ?? 83 3d ?? ?? ?? ?? ?? 53 56 57 0f 8f ?? ?? ?? ?? 6a 00 e8 ?? ?? ?? ?? 59 e8 ?? ?? ?? ?? 84 c0 74 ?? 6a 00 68",
        //        new AddModifier(2),
        //        new LeaModifier());

        //    // 
        //}

    }

}

using System;
using System.Runtime.InteropServices;

namespace HighVoltz.HBRelog.WoW.Lua
{
    [StructLayout(LayoutKind.Sequential, Size = 10, Pack = 1)]
    public struct LuaCommonHeader
    {
        public readonly IntPtr GCObjectPtr;
        private readonly uint _unk8;
        private readonly byte type;
        public readonly byte Marked;
        public LuaType Type { get { return (LuaType)type; } }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 1)]
    public struct LuaValueStruct
    {
        [FieldOffset(0)] public readonly int Boolean;
        [FieldOffset(0)] public readonly double Number;
        [FieldOffset(0)] public IntPtr Pointer;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct LuaTValueStruct
    {
        public readonly LuaValueStruct Value;
        public readonly LuaType Type;
        private readonly uint _unkC;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LuaTKeyStruct
    {
        public readonly LuaValueStruct Value;
        public readonly LuaType Type;
        private readonly uint _unkC;
        public readonly IntPtr NextNodePtr;
        private readonly uint unk;
    }
}
using System;
using System.Collections.Generic;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class Frame : VisibleRegion
    {
        public Frame(WowLuaManager wowManager, IntPtr address) : base(wowManager, address) { }

        public int Level
        {
            get { return LuaManager.Memory.Read<int>(Address + Offsets.Frame.LevelOffset); }
        }

        public FrameStrata Strata
        {
            get { return (FrameStrata)(LuaManager.Memory.Read<int>(Address + Offsets.Frame.StrataOffset) & 0xF); }
        }

        public IEnumerable<UIObject> Children
        {
            get
            {
                var ptr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.Frame.ChildrenOffset);
                while (ptr != IntPtr.Zero && ((uint)ptr & 1) == 0)
                {
                    yield return GetUIObjectFromPointer(LuaManager, LuaManager.Memory.Read<IntPtr>(ptr + 8));
                    ptr = LuaManager.Memory.Read<IntPtr>(ptr + 4);
                }
            }
        }

        /// <summary>
        /// Returns a list of non-Frame child regions belonging to the frame
        /// </summary>
        public IEnumerable<UIObject> Regions
        {
            get
            {
                var ptr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.Frame.RegionsOffset);
                var size = LuaManager.Memory.Read<int>(Address + Offsets.Frame.RegionsSizeOffset);
                while (ptr != IntPtr.Zero && ((uint)ptr & 1) == 0)
                {
                    yield return GetUIObjectFromPointer(LuaManager, ptr);
                    ptr = LuaManager.Memory.Read<IntPtr>(ptr + 4 + size);
                }
            }
        }

        public int Id
        {
            get { return LuaManager.Memory.Read<int>(Address + Offsets.Frame.IdOffset); }
        }

    }

    public enum FrameStrata
    {
        Unknown = -1,
        World,
        Background,
        Low,
        Medium,
        High,
        Dialog,
        FullScreen,
        FullScreenDialog,
        Tooltip
    }
}
using System;
using System.Collections.Generic;

namespace WowClient.Lua.UI
{
    public class Frame : VisibleRegion
    {
        public Frame(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        public int Level
        {
            get { return Address.Deref<int>(Offsets.Frame.LevelOffset); }
        }

        public FrameStrata Strata
        {
            get { return (FrameStrata)(Address.Deref<int>(Offsets.Frame.StrataOffset) & 0xF); }
        }

        public IEnumerable<UIObject> Children
        {
            get
            {
                var ptr = Address.Deref(Offsets.Frame.ChildrenOffset);
                while (ptr.Value != IntPtr.Zero && ((uint)ptr.Value & 1) == 0)
                {
                    yield return Get(Wrapper, ptr.Deref(8));
                    ptr = ptr.Deref(4);
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
                var ptr = Address.Deref(Offsets.Frame.RegionsOffset);
                var size = Address.Deref<int>(Offsets.Frame.RegionsSizeOffset);
                while (ptr.Value != IntPtr.Zero && ((uint)ptr.Value & 1) == 0)
                {
                    yield return Get(Wrapper, ptr);
                    ptr = ptr.Deref(4 + size);
                }
            }
        }

        public int Id
        {
            get { return Address.Deref<int>(Offsets.Frame.IdOffset); }
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
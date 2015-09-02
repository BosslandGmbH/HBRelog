using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class ScrollFrame : Frame
    {
        public ScrollFrame(WowLuaManager wowManager, IntPtr address) : base(wowManager, address) { }

        public float HorizontalScroll
        {
            get
            {
                return ToActualSize(LuaManager.Memory.Read<float>(Address + Offsets.ScrollFrame.HorizontalScrollOffset));
            }
        }

        public float HorizontalScrollRange
        {
            get
            {
                return ToActualSize(LuaManager.Memory.Read<float>(Address + Offsets.ScrollFrame.HorizontalScrollRangeOffset));
            }
        }

        public float VerticalScroll
        {
            get
            {
                return ToActualSize(LuaManager.Memory.Read<float>(Address + Offsets.ScrollFrame.VerticalScrollOffset));
            }
        }

        public float VerticalScrollRange
        {
            get
            {
                return ToActualSize(LuaManager.Memory.Read<float>(Address + Offsets.ScrollFrame.VerticalScrollRangeOffset));
            }
        }

        public Frame ScrollChild
        {
            get
            {
                var ptr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.ScrollFrame.ScrollChildOffset);
                return ptr != IntPtr.Zero ? GetUIObjectFromPointer<Frame>(LuaManager, ptr) : null;
            }
        }
    }
}

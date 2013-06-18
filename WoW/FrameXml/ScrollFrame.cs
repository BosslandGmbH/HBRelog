using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class ScrollFrame : Frame
    {
        public ScrollFrame(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        public float HorizontalScroll
        {
            get
            {
                return ToActualSize(WowManager.Memory.Read<float>(Address + Offsets.ScrollFrame.HorizontalScrollOffset));
            }
        }

        public float HorizontalScrollRange
        {
            get
            {
                return ToActualSize(WowManager.Memory.Read<float>(Address + Offsets.ScrollFrame.HorizontalScrollRangeOffset));
            }
        }

        public float VerticalScroll
        {
            get
            {
                return ToActualSize(WowManager.Memory.Read<float>(Address + Offsets.ScrollFrame.VerticalScrollOffset));
            }
        }

        public float VerticalScrollRange
        {
            get
            {
                return ToActualSize(WowManager.Memory.Read<float>(Address + Offsets.ScrollFrame.VerticalScrollRangeOffset));
            }
        }

        public Frame ScrollChild
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.ScrollFrame.ScrollChildOffset);
                return ptr != IntPtr.Zero ? GetUIObjectFromPointer<Frame>(WowManager, ptr) : null;
            }
        }
    }
}

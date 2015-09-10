using System;

namespace WowClient.Lua.UI
{
    public class ScrollFrame : Frame
    {
        public ScrollFrame(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        public float HorizontalScroll
        {
            get
            {
                return ToActualSize(Address.Deref<float>(Offsets.ScrollFrame.HorizontalScrollOffset));
            }
        }

        public float HorizontalScrollRange
        {
            get
            {
                return ToActualSize(Address.Deref<float>(Offsets.ScrollFrame.HorizontalScrollRangeOffset));
            }
        }

        public float VerticalScroll
        {
            get
            {
                return ToActualSize(Address.Deref<float>(Offsets.ScrollFrame.VerticalScrollOffset));
            }
        }

        public float VerticalScrollRange
        {
            get
            {
                return ToActualSize(Address.Deref<float>(Offsets.ScrollFrame.VerticalScrollRangeOffset));
            }
        }

        public Frame ScrollChild
        {
            get
            {
                var ptr = Address.Deref(Offsets.ScrollFrame.ScrollChildOffset);
                return ptr.Value != IntPtr.Zero ? Get<Frame>(Wrapper, ptr) : null;
            }
        }
    }
}

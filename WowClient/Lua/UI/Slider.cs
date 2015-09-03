using System;
using System.Windows.Forms;

namespace WowClient.Lua.UI
{
    public class Slider : Frame
    {
        public Slider(WowLua wow, IAbsoluteAddress address) : base(wow, address) { }

        public bool IsEnabled
        {
            get { return (Address.Deref<int>(Offsets.Slider.IsEnabledFlagOffset) & Offsets.Slider.IsEnabledBit) == 0; }
        }

        public Orientation Orientation
        {
            get
            {
                var ori = Address.Deref<int>(Offsets.Slider.OrientationOffset);
                return ori == 1 ? Orientation.Vertical : Orientation.Horizontal;
            }
        }

        public float MinValue
        {
            get { return Address.Deref<float>(Offsets.Slider.MinValueOffset); }
        }

        public float MaxValue
        {
            get { return Address.Deref<float>(Offsets.Slider.MaxValueOffset) + MinValue; }
        }

        public float Value
        {
            get { return Address.Deref<float>(Offsets.Slider.ValueOffset) + MinValue; }
        }

        public float ValueStep
        {
            get { return Address.Deref<float>(Offsets.Slider.ValueStepOffset) + MinValue; }
        }


        public Texture ThumbTexture
        {
            get
            {
                var ptr = Address.Deref(Offsets.Slider.ThumbTextureOffset);
                return ptr.Value != IntPtr.Zero ? GetUIObjectFromPointer<Texture>(Lua, ptr) : null;
            }
        }
    }
}

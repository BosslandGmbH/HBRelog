using System;
using System.Windows.Forms;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class Slider : Frame
    {
        public Slider(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        public bool IsEnabled
        {
            get { return (WowManager.Memory.Read<int>(Address + Offsets.Slider.IsEnabledFlagOffset) & Offsets.Slider.IsEnabledBit) == 0; }
        }

        public Orientation Orientation
        {
            get
            {
                var ori = WowManager.Memory.Read<int>(Address + Offsets.Slider.OrientationOffset);
                return ori == 1 ? Orientation.Vertical : Orientation.Horizontal;
            }
        }

        public float MinValue
        {
            get { return WowManager.Memory.Read<float>(Address + Offsets.Slider.MinValueOffset); }
        }

        public float MaxValue
        {
            get { return WowManager.Memory.Read<float>(Address + Offsets.Slider.MaxValueOffset) + MinValue; }
        }

        public float Value
        {
            get { return WowManager.Memory.Read<float>(Address + Offsets.Slider.ValueOffset) + MinValue; }
        }

        public float ValueStep
        {
            get { return WowManager.Memory.Read<float>(Address + Offsets.Slider.ValueStepOffset) + MinValue; }
        }


        public Texture ThumbTexture
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.Slider.ThumbTextureOffset);
                return ptr != IntPtr.Zero ? GetUIObjectFromPointer<Texture>(WowManager, ptr) : null;
            }
        }
    }
}

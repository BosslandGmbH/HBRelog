using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public enum ButtonState
    {
        Unknown,
        Disabled,
        Normal,
        Pushed
    }

    public class Button : Frame
    {
        public Button(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        public string Text
        {
            get
            {
                var fontString = FontString;
                return fontString != null ? fontString.Text : string.Empty;
            }
        }

        public bool IsEnabled
        {
            get { return State != ButtonState.Disabled; }
        }

        public ButtonState State
        {
            get
            {
                var state = ButtonState.Disabled;
                var stateInt = WowManager.Memory.Read<int>(Address + Offsets.Button.FlagsOffset) << 28 >> 28;
                if (stateInt != 0)
                {
                    var pushed = stateInt - 1;
                    if (pushed > 0)
                    {
                        state = pushed == 1 ? ButtonState.Pushed : ButtonState.Unknown;
                    }
                    else
                        state = ButtonState.Normal;
                }
                return state;
            }
        }

        public FontString FontString
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.Button.FontStringOffset);
                return ptr == IntPtr.Zero ? null : GetUIObjectFromPointer<FontString>(WowManager, ptr);
            }
        }

        public Texture HighlightTexture
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.Button.HighlightTextureOffset);
                return ptr == IntPtr.Zero ? null : GetUIObjectFromPointer<Texture>(WowManager, ptr);
            }
        }
    }
}
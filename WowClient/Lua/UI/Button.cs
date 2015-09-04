using System;

namespace WowClient.Lua.UI
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
        public Button(WowLua wow, IAbsoluteAddress address) : base(wow, address) { }

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
                var stateInt = Address.Deref<int>(Offsets.Button.FlagsOffset) << 28 >> 28;
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
                var ptr = Address.Deref(Offsets.Button.FontStringOffset);
                return ptr.Value == IntPtr.Zero ? null : Get<FontString>(Lua, ptr);
            }
        }

        public Texture HighlightTexture
        {
            get
            {
                var ptr = Address.Deref(Offsets.Button.HighlightTextureOffset);
                return ptr.Value == IntPtr.Zero ? null : Get<Texture>(Lua, ptr);
            }
        }
    }
}
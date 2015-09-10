using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Shared;

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
        public Button(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        public string Text
        {
            get
            {
                var fontString = FontString;
                return fontString != null ? fontString.Text : string.Empty;
            }
        }

        public override string ToString()
        {
            return string.Format("{0} value: \"{1}\"", base.ToString(), Text);
        }

        public async Task<bool> ClickAsync()
        {
            return await Wrapper.ClickAtAsync(ToWindowCoord());
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
                return ptr.Value == IntPtr.Zero ? null : Get<FontString>(Wrapper, ptr);
            }
        }

        public Texture HighlightTexture
        {
            get
            {
                var ptr = Address.Deref(Offsets.Button.HighlightTextureOffset);
                return ptr.Value == IntPtr.Zero ? null : Get<Texture>(Wrapper, ptr);
            }
        }
    }
}
using System;
using System.Text;

namespace WowClient.Lua.UI
{
    public class FontString : VisibleRegion, IFontInstance
    {
        public FontString(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        public string Text
        {
            get
            {
                var ptr = Address.Deref(Offsets.FontString.TextOffset);
                if (ptr.Value == IntPtr.Zero)
                    return string.Empty;
                return Wrapper.Memory.ReadString(ptr, 512, Encoding.UTF8);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} value: \"{1}\"", base.ToString(), Text);
        }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

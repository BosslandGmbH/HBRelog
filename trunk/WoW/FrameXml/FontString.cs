using System;
using System.Text;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class FontString : VisibleRegion, IFontInstance
    {
        public FontString(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        public string Text
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.FontString.TextOffset);
                if (ptr == IntPtr.Zero)
                    return string.Empty;
                return WowManager.Memory.ReadString(ptr, Encoding.UTF8);
            }
        }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

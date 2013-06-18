using System;
using System.Text;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class FontString : VisibleRegion, IFontInstance
    {
        public FontString(WowManager wowManager,IntPtr address) : base(wowManager, address) { }

        public string Text
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.FontString.TextOffset);
                return ptr != IntPtr.Zero ? WowManager.Memory.ReadString(ptr, Encoding.UTF8, 128) : string.Empty;
            }
        }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

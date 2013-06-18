using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.HBRelog.WoW;
using HighVoltz.HBRelog.WoW.FrameXml;

namespace Test.FrameXml
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

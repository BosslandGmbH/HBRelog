using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class Font: UIObject, IFontInstance
    {
        public Font(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

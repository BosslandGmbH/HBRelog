using System;

namespace WowClient.Lua.UI
{
    public class Font: UIObject, IFontInstance
    {
        public Font(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

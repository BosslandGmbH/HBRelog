using System;
using System.Text;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class FontString : VisibleRegion, IFontInstance
    {
        public FontString(WowLuaManager wowManager, IntPtr address) : base(wowManager, address) { }

        public string Text
        {
            get
            {
                var ptr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.FontString.TextOffset);
                if (ptr == IntPtr.Zero)
                    return string.Empty;
                return LuaManager.Memory.ReadString(ptr, Encoding.UTF8);
            }
        }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }
    }
}

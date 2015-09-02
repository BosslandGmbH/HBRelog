using System;
using System.Text;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class Texture : VisibleRegion
    {
        public Texture(WowLuaManager wowManager, IntPtr address) : base(wowManager, address) { }

        private bool triedGetPath;
        private string _texturePath = string.Empty;

        public string TexturePath
        {
            get
            {
                if (!triedGetPath)
                {
                    var ptr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.Texture.TexturePathObjectOffset);
                    if (ptr != IntPtr.Zero)
                    {
                        ptr = LuaManager.Memory.Read<IntPtr>(ptr + Offsets.Texture.TexturePathOffset);
                        if (ptr != IntPtr.Zero)
                            _texturePath = LuaManager.Memory.ReadString(ptr, Encoding.UTF8, 260);
                    }
	                triedGetPath = true;
                }
                return _texturePath;
            }
        }
    }
}

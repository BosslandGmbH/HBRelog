using System;
using System.Text;

namespace WowClient.Lua.UI
{
    public class Texture : VisibleRegion
    {
        public Texture(WowLua wow, IAbsoluteAddress address) : base(wow, address) { }

        private bool _triedGetPath;
        private string _texturePath = string.Empty;

        public string TexturePath
        {
            get
            {
                if (!_triedGetPath)
                {
                    var ptr = Address.Deref(Offsets.Texture.TexturePathObjectOffset);
                    if (ptr.Value != IntPtr.Zero)
                    {
                        ptr = ptr.Deref(Offsets.Texture.TexturePathOffset);
                        if (ptr.Value != IntPtr.Zero)
                            _texturePath = Lua.Memory.ReadString(ptr, 260, Encoding.UTF8);
                    }
	                _triedGetPath = true;
                }
                return _texturePath;
            }
        }
    }
}

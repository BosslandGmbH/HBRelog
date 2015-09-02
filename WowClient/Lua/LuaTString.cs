using System;
using System.Runtime.InteropServices;
using System.Text;
using GreyMagic;

namespace HighVoltz.HBRelog.WoW.Lua
{
    public class LuaTString
    {
        private LuaTStringHeader _luaTString;
        
        private readonly ExternalProcessReader _memory;

        public LuaTString(ExternalProcessReader memory, IntPtr address)
        {
            Address = address;
            _memory = memory;
            _luaTString = memory.Read<LuaTStringHeader>(address);
        }

        public readonly IntPtr Address;

        public uint Hash { get { return _luaTString.Hash; } }

        private string _string;
        public string Value
        {
            get
            {
                return (_string ?? (_string = _memory.ReadString(Address + Size, Encoding.UTF8, _luaTString.Length)));
            }
        }

        public override string ToString()
        {
            return Value;
        }

        private const int Size = 20;

        [StructLayout(LayoutKind.Sequential, Size = 20, Pack = 1)]
        struct LuaTStringHeader
        {
            readonly public LuaCommonHeader Header;
            private readonly byte reserved1;
            private readonly byte reserved2;
            public readonly uint Hash;
            public readonly int Length;
        }
    }
}

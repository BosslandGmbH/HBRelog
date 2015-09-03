using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WowClient.Lua
{
    public class LuaTString
    {
        private LuaTStringHeader _luaTString;
        
        private readonly IReadOnlyMemory _memory;

        public LuaTString(IReadOnlyMemory memory, IAbsoluteAddress address)
        {
            Address = address;
            _memory = memory;
            _luaTString = memory.Read<LuaTStringHeader>(address);
        }

        public IAbsoluteAddress Address { get; private set; }

        public uint Hash { get { return _luaTString.Hash; } }

        private string _string;
        public string Value
        {
            get
            {
                var offs = _memory.GetRelativeAddress(DataOffset);
                return _string ??
                    (_string = _memory.ReadString(Address.Add(offs), (uint)_luaTString.Length, Encoding.UTF8));
            }
        }

        public override string ToString()
        {
            return Value;
        }

        private const int HeaderSize = 20;
        private const int DataOffset = HeaderSize;

        [StructLayout(LayoutKind.Sequential, Size = HeaderSize, Pack = 1)]
        struct LuaTStringHeader
        {
            private readonly LuaCommonHeader Header;
            private readonly byte reserved1;
            private readonly byte reserved2;
            public readonly uint Hash;
            public readonly int Length;
        }
    }
}

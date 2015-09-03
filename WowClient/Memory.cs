using System;
using System.Diagnostics;
using System.Text;
using GreyMagic;

namespace WowClient
{

    /// <summary>
    /// We can dereference absolute address (And also with non-zero offset too).
    /// We can add only absolute to relative. Otherwise result has no meaning.
    /// </summary>
    public interface IAbsoluteAddress
    {
        IntPtr Value { get; }
        IAbsoluteAddress Deref();
        IAbsoluteAddress Deref(int offset);
        T Deref<T>() where T : struct;
        T Deref<T>(int offset) where T : struct;
        IAbsoluteAddress Add(IRelativeAddress address);
    }

    /// <summary>
    /// We can dereference relative address (And also with non-zero offset too), in this case dereference produces absolute address.
    /// We can add relative addresses.
    /// </summary>
    public interface IRelativeAddress
    {
        int Value { get; }
        IAbsoluteAddress Deref();
        IAbsoluteAddress Deref(int offset);
        IRelativeAddress Add(IRelativeAddress address);
    }

    /// <summary>
    /// Defines read* operations on immutable memory region.
    /// </summary>
    public interface IReadOnlyMemory
    {
        T Read<T>(IAbsoluteAddress address) where T : struct;
        T Read<T>(IRelativeAddress address) where T : struct;
        byte[] ReadBytes(IAbsoluteAddress address, uint size);
        byte[] ReadBytes(IRelativeAddress address, uint size);
        string ReadString(IAbsoluteAddress address, uint size, Encoding encoding);
        string ReadString(IRelativeAddress address, uint size, Encoding encoding);
        IRelativeAddress GetRelativeAddress(int address);
        IAbsoluteAddress GetAbsoluteAddress(IntPtr address);
        IAbsoluteAddress FindPattern(string pattern);
    }

    public class ReadOnlyMemory : IReadOnlyMemory, IDisposable
    {
        private readonly ExternalProcessMemory _mem;
        private readonly PatternFinder _patternFinder;
        public ReadOnlyMemory(Process process)
        {
            _mem = new ExternalProcessMemory(
                new ExternalProcessMemoryInitParams()
                {
                    Process = process,
                    DefaultCacheValue = true,
                });
            _patternFinder = new PatternFinder(_mem);
        }

        internal class AbsoluteAddress : IAbsoluteAddress
        {
            private readonly ExternalProcessMemory _mem;
            public IntPtr Value { get; private set; }
            public AbsoluteAddress(ExternalProcessMemory memory, IntPtr address)
            {
                _mem = memory;
                Value = address;
            }
            public IAbsoluteAddress Deref()
            {
                return new AbsoluteAddress(_mem, _mem.Read<IntPtr>(Value));
            }

            public IAbsoluteAddress Deref(int offset)
            {
                return new AbsoluteAddress(_mem, _mem.Read<IntPtr>(Value + offset));
            }

            public T Deref<T>() where T : struct
            {
                return _mem.Read<T>(Value);
            }

            public T Deref<T>(int offset) where T : struct
            {
                return _mem.Read<T>(Value + offset);
            }

            public IAbsoluteAddress Add(IRelativeAddress address)
            {
                return new AbsoluteAddress(_mem, Value + address.Value);
            }
        }

        internal class RelativeAddress : IRelativeAddress
        {
            private readonly ExternalProcessMemory _mem;
            public int Value { get; private set; }
            public RelativeAddress(ExternalProcessMemory memory, int address)
            {
                _mem = memory;
                Value = address;
            }
            public IAbsoluteAddress Deref()
            {
                return new AbsoluteAddress(_mem, _mem.Read<IntPtr>((IntPtr)Value, true));
            }

            public IAbsoluteAddress Deref(int offset)
            {
                return new AbsoluteAddress(_mem, _mem.Read<IntPtr>((IntPtr)(Value + offset), true));
            }

            public IRelativeAddress Add(IRelativeAddress address)
            {
                return new RelativeAddress(_mem, Value + address.Value);
            }
        }

        public T Read<T>(IAbsoluteAddress address) where T : struct
        {
            return _mem.Read<T>(address.Value);
        }

        public T Read<T>(IRelativeAddress address) where T : struct
        {
            return _mem.Read<T>((IntPtr)address.Value, true);
        }

        public byte[] ReadBytes(IAbsoluteAddress address, uint size)
        {
            return _mem.ReadBytes(address.Value, (int)size);
        }

        public byte[] ReadBytes(IRelativeAddress address, uint size)
        {
            return _mem.ReadBytes((IntPtr)address.Value, (int)size, true);
        }

        public string ReadString(IAbsoluteAddress address, uint size, Encoding encoding)
        {
            return _mem.ReadString(address.Value, encoding, (int)size);
        }

        public string ReadString(IRelativeAddress address, uint size, Encoding encoding)
        {
            return _mem.ReadString((IntPtr)address.Value, encoding, (int)size, true);
        }

        public IRelativeAddress GetRelativeAddress(int address)
        {
            return new RelativeAddress(_mem, address);
        }

        public IAbsoluteAddress GetAbsoluteAddress(IntPtr address)
        {
            return new AbsoluteAddress(_mem, address);
        }

        public IAbsoluteAddress FindPattern(string pattern)
        {
            return GetAbsoluteAddress(_patternFinder.Find(pattern));
        }

        public void Dispose()
        {
            _mem.Dispose();
        }
    }
}

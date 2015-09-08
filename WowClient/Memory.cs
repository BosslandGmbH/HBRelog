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
#if DEBUG
        ExternalProcessMemory Memory { get; }
#endif
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
#if DEBUG
        ExternalProcessMemory Memory { get; }
#endif
    }

    /// <summary>
    /// Defines read* operations on immutable memory region.
    /// </summary>
    public interface IReadOnlyMemory : IDisposable
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
#if DEBUG
        ExternalProcessMemory Memory { get; }
#endif
    }

    public class ReadOnlyMemory : IReadOnlyMemory
    {
        private ExternalProcessMemory _mem;
        private readonly PatternFinder _patternFinder;
        public ReadOnlyMemory(Process process)
        {
            _mem = new ExternalProcessMemory(process, false, true, false);
            _patternFinder = new PatternFinder(_mem);
            _mem.DisableCache();
        }
#if DEBUG
        public ExternalProcessMemory Memory
        {
            get { return _mem; }
        }
#endif

        internal class AbsoluteAddress : IAbsoluteAddress
        {
            private ExternalProcessMemory _mem;
            public IntPtr Value { get; private set; }
#if DEBUG
            public ExternalProcessMemory Memory
            {
                get { return _mem; }
            }
#endif

            public override int GetHashCode()
            {
                return (int)Value;
            }

            public override string ToString()
            {
                return "0x" + Value.ToString("x");
            }

            public override bool Equals(object obj)
            {
                var otherAbs = obj as AbsoluteAddress;
                if (otherAbs != null)
                    return otherAbs.Value == Value;
                var otherRel = obj as RelativeAddress;
                if (otherRel != null)
                    return _mem.ImageBase + otherRel.Value == Value;
                return Value == IntPtr.Zero;
            }

            public AbsoluteAddress(ref ExternalProcessMemory memory, IntPtr address)
            {
                _mem = memory;
                Value = address;
            }
            public IAbsoluteAddress Deref()
            {
                IntPtr ret;
                using (_mem.SaveCacheState())
                {
                    _mem.DisableCache();
                    ret = _mem.Read<IntPtr>(Value);
                }
                return new AbsoluteAddress(ref _mem, ret);
            }

            public IAbsoluteAddress Deref(int offset)
            {
                IntPtr ret;
                using (_mem.SaveCacheState())
                {
                    _mem.DisableCache();
                    ret = _mem.Read<IntPtr>(Value + offset);
                }
                return new AbsoluteAddress(ref _mem, ret);
            }

            public T Deref<T>() where T : struct
            {
                T ret;
                using (_mem.SaveCacheState())
                {
                    _mem.DisableCache();
                    ret = _mem.Read<T>(Value);
                }
                return ret;
            }

            public T Deref<T>(int offset) where T : struct
            {
                T ret;
                using (_mem.SaveCacheState())
                {
                    _mem.DisableCache();
                    ret = _mem.Read<T>(Value + offset);
                }
                return ret;
            }

            public IAbsoluteAddress Add(IRelativeAddress address)
            {
                return new AbsoluteAddress(ref _mem, Value + address.Value);
            }
        }

        internal class RelativeAddress : IRelativeAddress
        {
            private ExternalProcessMemory _mem;
            public int Value { get; private set; }
#if DEBUG
            public ExternalProcessMemory Memory
            {
                get { return _mem; }
            }
#endif
            public RelativeAddress(ref ExternalProcessMemory memory, int address)
            {
                _mem = memory;
                _mem.DisableCache();
                _mem.ClearCache();
                _mem.SaveCacheState();
                Value = address;
                Base = _mem.ImageBase;
            }

            public IntPtr Base { get; private set; }
            
            public override int GetHashCode()
            {
                return (int)(Base + Value);
            }

            public override string ToString()
            {
                return "0x" + (Base + Value).ToString("x");
            }

            public override bool Equals(object obj)
            {
                var otherAbs = obj as AbsoluteAddress;
                if (otherAbs != null)
                    return otherAbs.Value == Base + Value;
                var otherRel = obj as RelativeAddress;
                if (otherRel != null)
                    return otherRel.Base == Base && otherRel.Value == Value;
                return Value == 0;
            }
            public IAbsoluteAddress Deref()
            {
                _mem.DisableCache();
                var v = _mem.Read<IntPtr>((IntPtr)Value, true);
                return new AbsoluteAddress(ref _mem, v);
            }

            public IAbsoluteAddress Deref(int offset)
            {
                _mem.DisableCache();
                var v = _mem.Read<IntPtr>((IntPtr)(Value + offset), true);
                return new AbsoluteAddress(ref _mem, v);
            }

            public IRelativeAddress Add(IRelativeAddress address)
            {
                return new RelativeAddress(ref _mem, Value + address.Value);
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
            return new RelativeAddress(ref _mem, address);
        }

        public IAbsoluteAddress GetAbsoluteAddress(IntPtr address)
        {
            return new AbsoluteAddress(ref _mem, address);
        }

        public IAbsoluteAddress FindPattern(string pattern)
        {
            return GetAbsoluteAddress(_patternFinder.Find(pattern));
        }

        public void Dispose()
        {
            _patternFinder.Dispose();
            _mem.Dispose();
        }
    }
}

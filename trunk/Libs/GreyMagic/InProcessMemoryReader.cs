﻿using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using GreyMagic.Internals;

namespace GreyMagic
{
    public unsafe class InProcessMemoryReader : MemoryBase
    {
        private DetourManager _detourManager;

        public InProcessMemoryReader(Process proc) : base(proc)
        {
        }

        /// <summary>
        /// Provides access to the DetourManager class, that allows you to create and remove
        /// detours and hooks for functions. (Or any other use you may find...)
        /// </summary>
        public virtual DetourManager Detours { get { return _detourManager ?? (_detourManager = new DetourManager(this)); } }

        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void MoveMemory(void* dest, void* src, int size);

        [HandleProcessCorruptedStateExceptions]
        private T InternalRead<T>(IntPtr address) where T : struct
        {
            try
            {
                // TODO: Optimize this more. The boxing/unboxing required tends to slow this down.
                // It may be worth it to simply use memcpy to avoid it, but I doubt thats going to give any noticeable increase in speed.
                if (address == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Cannot retrieve a value at address 0");
                }

                object ret;
                switch (MarshalCache<T>.TypeCode)
                {
                    case TypeCode.Object:

                        if (MarshalCache<T>.IsIntPtr)
                        {
                            return (T) (object) *(IntPtr*) address;
                        }

                        // If the type doesn't require an explicit Marshal call, then ignore it and memcpy the fuckin thing.
                        if (!MarshalCache<T>.TypeRequiresMarshal)
                        {
                            T o = default(T);
                            void* ptr = MarshalCache<T>.GetUnsafePtr(ref o);

                            MoveMemory(ptr, (void*) address, MarshalCache<T>.Size);

                            return o;
                        }

                        // All System.Object's require marshaling!
                        ret = Marshal.PtrToStructure(address, typeof (T));
                        break;
                    case TypeCode.Boolean:
                        ret = *(byte*) address != 0;
                        break;
                    case TypeCode.Char:
                        ret = *(char*) address;
                        break;
                    case TypeCode.SByte:
                        ret = *(sbyte*) address;
                        break;
                    case TypeCode.Byte:
                        ret = *(byte*) address;
                        break;
                    case TypeCode.Int16:
                        ret = *(short*) address;
                        break;
                    case TypeCode.UInt16:
                        ret = *(ushort*) address;
                        break;
                    case TypeCode.Int32:
                        ret = *(int*) address;
                        break;
                    case TypeCode.UInt32:
                        ret = *(uint*) address;
                        break;
                    case TypeCode.Int64:
                        ret = *(long*) address;
                        break;
                    case TypeCode.UInt64:
                        ret = *(ulong*) address;
                        break;
                    case TypeCode.Single:
                        ret = *(float*) address;
                        break;
                    case TypeCode.Double:
                        ret = *(double*) address;
                        break;
                    case TypeCode.Decimal:
                        // Probably safe to remove this. I'm unaware of anything that actually uses "decimal" that would require memory reading...
                        ret = *(decimal*) address;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return (T) ret;
            }
            catch (AccessViolationException ex)
            {
                Trace.WriteLine("Access Violation on " + address + " with type " + typeof (T).Name);
                return default(T);
            }
        }

        /// <summary>
        /// Reads a specific number of bytes from memory and writes them to an unsafe address.
        /// </summary>
        /// <param name="address"></param>
        /// <param name="buffer"></param>
        /// <param name="count"></param>
        public void ReadUnsafe(IntPtr address, void* buffer, int count)
        {
            if (ReadBytes((uint) address, buffer, count) != count)
                throw new Exception("Exception while reading " + count + " bytes from " + ((uint) address).ToString("X"));
        }

        /// <summary>
        /// Reads a specific number of bytes from memory.
        /// </summary>
        /// <param name="dwAddress">The address.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="count">The count.</param>
        /// <returns></returns>
        /// <remarks>Created 2012-04-23</remarks>
        public int ReadBytes(uint dwAddress, void* buffer, int count)
        {
            int lpBytesRead;
            if (!ReadProcessMemory(ProcessHandle, dwAddress, new IntPtr(buffer), count, out lpBytesRead))
                throw new AccessViolationException(string.Format("Could not read bytes from {0} [{1}]!",
                    dwAddress.ToString("X8"), Marshal.GetLastWin32Error()));

            return lpBytesRead;
        }

        [DllImport("kernel32", EntryPoint = "ReadProcessMemory")]
        private static extern bool ReadProcessMemory(SafeMemoryHandle hProcess, uint dwAddress, IntPtr lpBuffer,
            int nSize, out int lpBytesRead);

        #region Overrides of MemoryBase

        /// <summary>
        /// Writes a set of bytes to memory.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns>
        /// Number of bytes written.
        /// </returns>
        public override int WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false)
        {
            if (isRelative)
                address = GetAbsolute(address);

            using (new MemoryProtectionOperation(ProcessHandle, address, bytes.Length, 0x40))
            {
                var ptr = (byte*) address;
                for (int i = 0; i < bytes.Length; i++)
                {
                    ptr[i] = bytes[i];
                }
            }

            return bytes.Length;
        }

        /// <summary>
        /// Reads a specific number of bytes from memory.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public override byte[] ReadBytes(IntPtr address, int count, bool isRelative = false)
        {
            if (isRelative)
                address = GetAbsolute(address);

            var ret = new byte[count];
            var ptr = (byte*) address;
            for (int i = 0; i < count; i++)
            {
                ret[i] = ptr[i];
            }
            return ret;
        }

        /// <summary> Reads a value from the specified address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> . </returns>
        public override T Read<T>(IntPtr address, bool isRelative = false)
        {
            if (isRelative)
                address = GetAbsolute(address);

            return InternalRead<T>(address);
        }

        /// <summary> Writes a value specified to the address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="value"> The value. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public override bool Write<T>(IntPtr address, T value, bool isRelative = false)
        {
            if (isRelative)
                address = GetAbsolute(address);

            Marshal.StructureToPtr(value, address, false);
            return true;
        }

        /// <summary> Reads an array of values from the specified address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="count"> Number of. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> . </returns>
        public override T[] Read<T>(IntPtr address, int count, bool isRelative = false)
        {
            int size = MarshalCache<T>.Size;
            var ret = new T[count];
            for (int i = 0; i < count; i++)
            {
                ret[i] = Read<T>(address + (i * size), isRelative);
            }
            return ret;
        }

        /// <summary> Writes an array of values to the address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="value"> The value. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public override bool Write<T>(IntPtr address, T[] value, bool isRelative = false)
        {
            if (isRelative)
                address = GetAbsolute(address);

            int size = MarshalCache<T>.Size;
            for (int i = 0; i < value.Length; i++)
            {
                T val = value[i];
                Write(address + (i * size), val);
            }
            return true;
        }

        /// <summary> Reads a value from the specified address in memory. This method is used for multi-pointer dereferencing.</summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <param name="addresses"> A variable-length parameters list containing addresses. </param>
        /// <returns> . </returns>
        public override T Read<T>(bool isRelative = false, params IntPtr[] addresses)
        {
            if (addresses.Length == 0)
            {
                throw new InvalidOperationException("Cannot read a value from unspecified addresses.");
            }

            if (addresses.Length == 1)
            {
                return Read<T>(addresses[0], isRelative);
            }

            var temp = Read<IntPtr>(addresses[0], isRelative);

            for (int i = 1; i < addresses.Length - 1; i++)
            {
                temp = Read<IntPtr>(temp + (int) addresses[i]);
            }
            return Read<T>(temp + (int) addresses[addresses.Length - 1]);
        }

        /// <summary> Writes a value specified to the address in memory. This method is used for multi-pointer dereferencing.</summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <param name="value"> The value. </param>
        /// <param name="addresses"> A variable-length parameters list containing addresses. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public override bool Write<T>(bool isRelative = false, T value = default(T), params IntPtr[] addresses)
        {
            if (addresses.Length == 0)
            {
                throw new InvalidOperationException("Cannot write a value to unspecified addresses.");
            }
            if (addresses.Length == 1)
            {
                return Write(addresses[0], value, isRelative);
            }

            var temp = Read<IntPtr>(addresses[0], isRelative);
            for (int i = 1; i < addresses.Length - 1; i++)
            {
                temp = Read<IntPtr>(temp + (int) addresses[i]);
            }
            return Write(temp + (int) addresses[addresses.Length - 1], value);
        }

        #endregion
    }
}
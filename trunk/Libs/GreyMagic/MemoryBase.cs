﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GreyMagic.Internals;
using GreyMagic.Native;

namespace GreyMagic
{
    public abstract class MemoryBase : IDisposable
    {
        private PatchManager _patchManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryBase"/> class.
        /// </summary>
        /// <param name="proc">The process.</param>
        /// <remarks>Created 2012-02-15</remarks>
        protected MemoryBase(Process proc)
        {
            // Good to set this too if ure using events.
            proc.EnableRaisingEvents = true;

            // Since people tend to not realize it exists, we make sure to handle it.
            proc.Exited += (s, e) =>
                {
                    if (ProcessExited != null)
                    {
                        ProcessExited(s, e);
                    }
                    HandleProcessExiting();
                };

            Process = proc;
            proc.ErrorDataReceived += OutputDataReceived;
            proc.OutputDataReceived += OutputDataReceived;

            Process.EnterDebugMode();
            ProcessAccessFlags a = ProcessAccessFlags.PROCESS_CREATE_THREAD |
                                   ProcessAccessFlags.PROCESS_QUERY_INFORMATION |
                                   ProcessAccessFlags.PROCESS_SET_INFORMATION | ProcessAccessFlags.PROCESS_TERMINATE |
                                   ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_READ |
                                   ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.SYNCHRONIZE;

            ProcessHandle = Imports.OpenProcess(a, false, proc.Id);
            ImageBase = Process.MainModule.BaseAddress;
        }

        /// <summary>
        /// Provides access to the PatchManager class, which allows you to apply and remove patches.
        /// </summary>
        public PatchManager Patches { get { return _patchManager ?? (_patchManager = new PatchManager(this)); } }

        /// <summary>
        /// Gets the process.
        /// </summary>
        /// <remarks>Created 2012-02-15</remarks>
        public Process Process { get; private set; }

        /// <summary>
        /// Gets or sets the process handle.
        /// </summary>
        /// <value>
        /// The process handle.
        /// </value>
        /// <remarks>Created 2012-02-15</remarks>
        public SafeMemoryHandle ProcessHandle;

        /// <summary>Gets the image base.</summary>
        public IntPtr ImageBase;

        #region Read-Write

        /// <summary>
        /// Reads a specific number of bytes from memory.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="count">The count.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public abstract byte[] ReadBytes(IntPtr address, int count, bool isRelative = false);

        /// <summary>
        /// Writes a set of bytes to memory.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="bytes">The bytes.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns>
        /// Number of bytes written.
        /// </returns>
        public abstract int WriteBytes(IntPtr address, byte[] bytes, bool isRelative = false);

        /// <summary>
        /// Reads the struct array.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="elements">The elements.</param>
        /// <param name="isRelative">if set to <c>true</c> [is relative].</param>
        /// <returns></returns>
        public virtual unsafe T[] ReadStructArray<T>(IntPtr address, int elements, bool isRelative = false) where T : struct
        {
            if (isRelative)
                address = GetAbsolute(address);
            
            var ret = new T[elements];

            for (int i = 0; i < elements; i++)
            {
                ret[i] = Read<T>(address + (i * MarshalCache<T>.Size));
            }

            return ret;
        }

        /// <summary> Reads a value from the specified address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> . </returns>
        public abstract T Read<T>(IntPtr address, bool isRelative = false) where T : struct;

        /// <summary> Writes a value specified to the address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="value"> The value. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public abstract bool Write<T>(IntPtr address, T value, bool isRelative = false) where T : struct;

        /// <summary> Reads an array of values from the specified address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="count"> Number of. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> . </returns>
        public abstract T[] Read<T>(IntPtr address, int count, bool isRelative = false) where T : struct;

        /// <summary> Writes an array of values to the address in memory. </summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="address"> The address. </param>
        /// <param name="value"> The value. </param>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public abstract bool Write<T>(IntPtr address, T[] value, bool isRelative = false) where T : struct;

        /// <summary> Reads a value from the specified address in memory. This method is used for multi-pointer dereferencing.</summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <param name="addresses"> A variable-length parameters list containing addresses. </param>
        /// <returns> . </returns>
        public abstract T Read<T>(bool isRelative = false, params IntPtr[] addresses) where T : struct;

        /// <summary> Writes a value specified to the address in memory. This method is used for multi-pointer dereferencing.</summary>
        /// <remarks> Created 3/24/2012. </remarks>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="isRelative"> (optional) the relative. </param>
        /// <param name="value"> The value. </param>
        /// <param name="addresses"> A variable-length parameters list containing addresses. </param>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public abstract bool Write<T>(bool isRelative = false, T value = default(T), params IntPtr[] addresses)
            where T : struct;

        #endregion

        #region Strings

        /// <summary> Reads a string. </summary>
        /// <remarks> Created 3/27/2012. </remarks>
        /// <param name="address"> The address. </param>
        /// <param name="encoding"> The encoding. </param>
        /// <param name="maxLength"> (optional) length of the maximum. </param>
        /// <param name="relative"> (optional) the relative. </param>
        /// <returns> The string. </returns>
        public virtual string ReadString(IntPtr address, Encoding encoding, int maxLength = 512, bool relative = false)
        {
            byte[] buffer = ReadBytes(address, maxLength, relative);
            string ret = encoding.GetString(buffer);
            if (ret.IndexOf('\0') != -1)
            {
                ret = ret.Remove(ret.IndexOf('\0'));
            }
            return ret;
        }

        /// <summary> Writes a string. </summary>
        /// <remarks> Created 3/27/2012. </remarks>
        /// <param name="address"> The address. </param>
        /// <param name="value"> The value. </param>
        /// <param name="encoding"> The encoding. </param>
        /// <param name="relative"> (optional) the relative. </param>
        public virtual bool WriteString(IntPtr address, string value, Encoding encoding, bool relative = false)
        {
            if (value[value.Length - 1] != '\0')
            {
                value += '\0';
            }

            byte[] b = encoding.GetBytes(value);
            int written = WriteBytes(address, b, relative);
            return written == b.Length;
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <remarks>Created 2012-02-15</remarks>
        public virtual void Dispose()
        {
            Process.LeaveDebugMode();
        }

        #endregion

        /// <summary>
        /// Handles the process exiting.
        /// </summary>
        /// <remarks>Created 2012-02-15</remarks>
        protected virtual void HandleProcessExiting()
        {
        }

        /// <summary> Event queue for all listeners interested in ProcessExited events. </summary>
        public event EventHandler ProcessExited;

        private void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Trace.Write(e.Data);
        }

        /// <summary>
        /// Gets the absolute.
        /// </summary>
        /// <param name="relative">The relative.</param>
        /// <returns></returns>
        /// <remarks>Created 2012-01-16 19:41</remarks>
        public IntPtr GetAbsolute(IntPtr relative)
        {
            return ImageBase + (int) relative;
        }

        /// <summary>
        /// Gets the relative.
        /// </summary>
        /// <param name="absolute">The absolute.</param>
        /// <returns></returns>
        /// <remarks>Created 2012-01-16 19:41</remarks>
        public IntPtr GetRelative(IntPtr absolute)
        {
            return ImageBase - (int) absolute;
        }

        /// <summary>
        /// Creates a function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address">The address.</param>
        /// <param name="isRelative">if set to <c>true</c> [address is relative].</param>
        /// <returns></returns>
        /// <remarks>Created 2012-01-16 20:40 by Nesox.</remarks>
        public T CreateFunction<T>(IntPtr address, bool isRelative = false) where T : class
        {
            return Marshal.GetDelegateForFunctionPointer(isRelative ? GetAbsolute(address) : address, typeof (T)) as T;
        }

        /// <summary>
        /// Gets the funtion pointer from a delegate.
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns></returns>
        /// <remarks>Created 2012-01-16 20:40 by Nesox.</remarks>
        public IntPtr GetFunction(Delegate d)
        {
            return Marshal.GetFunctionPointerForDelegate(d);
        }

        /// <summary>
        /// Gets the VF table entry.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        /// <remarks>Created 2012-01-16 20:40 by Nesox.</remarks>
        public IntPtr GetVFTableEntry(IntPtr address, int index)
        {
            var vftable = Read<IntPtr>(address);
            return Read<IntPtr>(vftable + (index * 4));
        }
    }
}
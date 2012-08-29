using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;

namespace HighVoltz.HBRelog.DirectX
{
    class D3DDevice : IDisposable
    {
        private const int D3D9SdkVersion = 32;

        private readonly Form _form;
        private readonly IntPtr _pD3D;
        
        private readonly IntPtr _d3DDevicePtr;

        private readonly VTableFuncDelegate _d3DDeviceRelease;
        private readonly VTableFuncDelegate _d3DRelease;

        public D3DDevice()
        {
            _form = new Form();
            _pD3D = Direct3DCreate9(D3D9SdkVersion);
            if (_pD3D == IntPtr.Zero)
                throw new Exception("Failed to create D3D.");

            var parameters = new D3DPresentParameters
            {
                Windowed = true,
                SwapEffect = 1,
                BackBufferFormat = 0
            };

            var createDevicePtr = GetVTableFuncAddress(_pD3D, 0x10);
            var createDevice = GetDelegate<CreateDeviceDelegate>(createDevicePtr);

            if (createDevice(_pD3D, 0, 1, _form.Handle, 0x20, ref parameters, out _d3DDevicePtr) < 0)
            {
                throw new Exception("Failed to create device.");
            }
            _d3DDeviceRelease = GetDelegate<VTableFuncDelegate>(GetVTableFuncAddress(_d3DDevicePtr, 2));
            _d3DRelease = GetDelegate<VTableFuncDelegate>(GetVTableFuncAddress(_pD3D, 2));
        }

        private unsafe IntPtr GetVTableFuncAddress(IntPtr obj, int funcIndex)
        {
            IntPtr pointer = *(IntPtr*)((void*)obj);
            return *(IntPtr*)((void*)((int)pointer + funcIndex * 4));
        }

        public unsafe IntPtr GetDeviceVTableFuncAddress(int funcIndex)
        {
            IntPtr pointer = *(IntPtr*)((void*)_d3DDevicePtr);
            return *(IntPtr*)((void*)((int)pointer + funcIndex * 4));
        }

        private T GetDelegate<T>(IntPtr address) where T : class
        {
            return Marshal.GetDelegateForFunctionPointer(address,typeof(T)) as T;
        }

        [DllImport("d3d9.dll")]
        internal static extern IntPtr Direct3DCreate9(uint sdkVersion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int CreateDeviceDelegate(
            IntPtr instance,
            uint adapter,
            uint deviceType,
            IntPtr focusWindow,
            uint behaviorFlags,
            [In] ref D3DPresentParameters presentationParameters,
            out IntPtr returnedDeviceInterface);

        [StructLayout(LayoutKind.Sequential)]
        public struct D3DPresentParameters
        {
            public readonly uint BackBufferWidth;
            public readonly uint BackBufferHeight;
            public uint BackBufferFormat;
            public readonly uint BackBufferCount;
            public readonly uint MultiSampleType;
            public readonly uint MultiSampleQuality;
            public uint SwapEffect;
            public readonly IntPtr hDeviceWindow;
            [MarshalAs(UnmanagedType.Bool)]
            public bool Windowed;
            [MarshalAs(UnmanagedType.Bool)]
            public readonly bool EnableAutoDepthStencil;
            public readonly uint AutoDepthStencilFormat;
            public readonly uint Flags;
            public readonly uint FullScreen_RefreshRateInHz;
            public readonly uint PresentationInterval;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void VTableFuncDelegate(IntPtr instance);


        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_d3DDevicePtr != IntPtr.Zero)
                        _d3DDeviceRelease(_d3DDevicePtr);

                    if (_pD3D != IntPtr.Zero)
                        _d3DRelease(_pD3D);

                    if (_form != null)
                        _form.Dispose();
                }
                _disposed = true;
            }
        }

        ~D3DDevice()
        {
            Dispose(false);
        }
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;
using Magic;

namespace HighVoltz.HBRelog.DirectX
{
    class Dirext3D
    {
        public Process Process { get; set; }

        private IntPtr D3D9Dll { get; set; }
        private IntPtr TheirD3D9Dll { get; set; }
        public IntPtr EndScene { get; private set; }
        public IntPtr BeginScene { get; private set; }

        public Dirext3D(Process targetProc)
        {
            Process = targetProc;

            if (Process.Modules.Cast<ProcessModule>().Any(m => m.ModuleName == "d3d11.dll"))
                throw new InvalidOperationException("DirectX11 is no currently supported");
            LoadD3D9Dll();

            using (var d3D = new D3DDevice())
            {
                BeginScene = GetAbsolutePointer(d3D.GetDeviceVTableFuncAddress((int)VTableIndexes.BeginScene));
                EndScene = GetAbsolutePointer(d3D.GetDeviceVTableFuncAddress((int)VTableIndexes.EndScene));
            }
        }

        private void LoadD3D9Dll()
        {
            D3D9Dll = LoadLibrary("d3d9.dll");
            if (D3D9Dll == IntPtr.Zero)
            {
                throw new Exception("Could not load d3d9.dll");
            }

            TheirD3D9Dll =
                Process.Modules.Cast<ProcessModule>().First(
                    m => m.ModuleName == "d3d9.dll").BaseAddress;
        }

        public IntPtr GetAbsolutePointer(IntPtr pointer)
        {
            var value = new IntPtr((int)pointer - (int)D3D9Dll);
            return new IntPtr((int)TheirD3D9Dll + (int)value);
        }


        [SuppressUnmanagedCodeSecurity, DllImport("kernel32")]
        internal static extern IntPtr LoadLibrary(string libraryName);

        public enum VTableIndexes 
        {
            Reset = 16,
            ResetEx = 132,
            BeginScene = 39,
            EndScene = 42,
        }

    }
}


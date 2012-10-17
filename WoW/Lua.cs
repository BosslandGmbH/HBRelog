using System;
using System.Text;

namespace HighVoltz.HBRelog.WoW
{
    public class Lua
    {
        private readonly Hook _wowHook;
        public Lua(Hook wowHook)
        {
            _wowHook = wowHook;
        }

        public void DoString(string command)
        {
            if (_wowHook.Installed)
            {
                // Allocate memory
                IntPtr doStringArgCodecave = _wowHook.Memory.AllocateMemory(Encoding.UTF8.GetBytes(command).Length + 1);
                // Write value:
                _wowHook.Memory.WriteBytes(doStringArgCodecave, Encoding.UTF8.GetBytes(command));

                // Write the asm stuff for Lua_DoString
                var asm = new[] 
                {
                    "mov eax, " + doStringArgCodecave,
                    "push 0",
                    "push eax",
                    "push eax",
                    "mov eax, " + ( HbRelogManager.Settings.FrameScriptExecuteOffset + _wowHook.Process.BaseOffset()) , // Lua_DoString
                    "call eax",
                    "add esp, 0xC",
                    "retn"
                };
                // Inject
                _wowHook.InjectAndExecute(asm);
                // Free memory allocated 
                _wowHook.Memory.FreeMemory(doStringArgCodecave);
            }
        }
    }
}

// credits to Rival from www.mmowned.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HighVoltz.Settings;
using System.Linq;
using Magic;

namespace HighVoltz.WoW
{
    public class Hook
    {
        // Addresse Inection code:
        private readonly object _executeLockObject = new object();
        private readonly Process _wowProcess;
        private uint _addresseInjection;
        private uint _injectedCode;
        private uint _retnInjectionAsm;

        public Hook(Process wowProc)
        {
            ProcessID = wowProc.Id;
            Memory = new BlackMagic(ProcessID);
            _wowProcess = wowProc;
            Installed = false;
        }

        public BlackMagic Memory { get; set; }

        public bool Installed { get; private set; }
        public int ProcessID { get; private set; }
        public uint BaseOffset
        {
            get { return (uint)_wowProcess.MainModule.BaseAddress.ToInt32(); }
        }

        public bool InstallHook()
        {
            try
            {
                // check if we need to scan for offsets
                if (string.IsNullOrEmpty(HBRelog.Settings.WowVersion) || !HBRelog.Settings.WowVersion.Equals(WoWVersion))
                    ScanForOffset();
                // Get address of EndScene
                uint pDevice = Memory.ReadUInt(HBRelog.Settings.DxDeviceOffset + BaseOffset);
                uint pEnd = Memory.ReadUInt(pDevice + HBRelog.Settings.DxDeviceIndex);
                uint pScene = Memory.ReadUInt(pEnd);
                uint pEndScene = Memory.ReadUInt(pScene + 0xA8);
                if (Memory.IsProcessOpen)
                {
                    // check if game is already hooked and dispose Hook
                    if (Memory.ReadByte(pEndScene) == 0xE9 &&
                        (_injectedCode == 0 || _addresseInjection == 0))
                    {
                        DisposeHooking();
                    }
                    // skip check since bots sometimes won't clean up after themselves
                    //if (_memory.ReadByte(pEndScene) != 0xE9) // check if game is already hooked
                    //{
                    Installed = false;
                    // allocate memory to store injected code:
                    _injectedCode = Memory.AllocateMemory(2048);
                    // allocate memory the new injection code pointer:
                    _addresseInjection = Memory.AllocateMemory(0x4);
                    Memory.WriteInt(_addresseInjection, 0);
                    // allocate memory the pointer return value:
                    _retnInjectionAsm = Memory.AllocateMemory(0x4);
                    Memory.WriteInt(_retnInjectionAsm, 0);

                    // Generate the STUB to be injected
                    Memory.Asm.Clear(); // $Asm

                    // save regs
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("pushad");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("pushfd");
                    InsertRandomOpCodes(Memory);
                    // Test if you need launch injected code:
                    Memory.Asm.AddLine("mov eax, [" + _addresseInjection + "]");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("test eax, eax");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("je @out");
                    InsertRandomOpCodes(Memory);
                    // Launch Fonction:
                    Memory.Asm.AddLine("mov eax, [" + _addresseInjection + "]");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("call eax");
                    InsertRandomOpCodes(Memory);
                    // Copy pointer return value:
                    Memory.Asm.AddLine("mov [" + _retnInjectionAsm + "], eax");
                    InsertRandomOpCodes(Memory);
                    // Enter value 0 of addresse func inject
                    Memory.Asm.AddLine("mov edx, " + _addresseInjection);
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("mov ecx, 0");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("mov [edx], ecx");
                    InsertRandomOpCodes(Memory);

                    // Close func
                    Memory.Asm.AddLine("@out:");
                    InsertRandomOpCodes(Memory);

                    // load reg
                    Memory.Asm.AddLine("popfd");
                    InsertRandomOpCodes(Memory);
                    Memory.Asm.AddLine("popad");
                    InsertRandomOpCodes(Memory);

                    // injected code
                    var sizeAsm = (uint)(Memory.Asm.Assemble().Length);
                    Memory.Asm.Inject(_injectedCode);

                    // Size asm jumpback
                    const int sizeJumpBack = 5;

                    // copy and save original instructions
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("mov edi, edi");
                    Memory.Asm.AddLine("push ebp");
                    Memory.Asm.AddLine("mov ebp, esp");
                    Memory.Asm.Inject(_injectedCode + sizeAsm);

                    // create jump back stub
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("jmp " + (pEndScene + sizeJumpBack));
                    Memory.Asm.Inject(_injectedCode + sizeAsm + sizeJumpBack);

                    // create hook jump
                    Memory.Asm.Clear(); // $jmpto
                    Memory.Asm.AddLine("jmp " + (_injectedCode));
                    Memory.Asm.Inject(pEndScene);
                    //}
                    Installed = true;
                }
            }
            catch
            {
                return false;
            }
            return Installed;
        }

        public void DisposeHooking()
        {
            try
            {
                // Get address of EndScene
                uint pDevice = Memory.ReadUInt(HBRelog.Settings.DxDeviceOffset + BaseOffset);
                uint pEnd = Memory.ReadUInt(pDevice + HBRelog.Settings.DxDeviceIndex);
                uint pScene = Memory.ReadUInt(pEnd);
                uint pEndScene = Memory.ReadUInt(pScene + 0xA8);

                if (Memory.ReadByte(pEndScene) == 0xE9) // check if wow is already hooked and dispose Hook
                {
                    // Restore origine endscene:
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("mov edi, edi");
                    Memory.Asm.AddLine("push ebp");
                    Memory.Asm.AddLine("mov ebp, esp");
                    Memory.Asm.Inject(pEndScene);
                }

                // free memory:
                Memory.FreeMemory(_injectedCode);
                Memory.FreeMemory(_addresseInjection);
                Memory.FreeMemory(_retnInjectionAsm);
                Installed = false;
            }
            catch
            {
            }
        }

        public byte[] InjectAndExecute(IEnumerable<string> asm, int returnLength = 0)
        {
            lock (_executeLockObject)
            {
                var tempsByte = new byte[0];
                // reset return value pointer
                Memory.WriteInt(_retnInjectionAsm, 0);

                if (Memory.IsProcessOpen && Installed)
                {
                    // Write the asm stuff
                    Memory.Asm.Clear();
                    foreach (string tempLineAsm in asm)
                    {
                        Memory.Asm.AddLine(tempLineAsm);
                    }

                    // Allocation Memory
                    uint injectionAsmCodecave = Memory.AllocateMemory(Memory.Asm.Assemble().Length);

                    try
                    {
                        // Inject
                        Memory.Asm.Inject(injectionAsmCodecave);
                        Memory.WriteInt(_addresseInjection, (int)injectionAsmCodecave);
                        while (Memory.ReadInt(_addresseInjection) > 0)
                        {
                            Thread.Sleep(5);
                        } // Wait to launch code

                        // We don't care about return values. besides this only works if a pointer is returned

                        //if (returnLength > 0)
                        //{
                        //    tempsByte = Memory.ReadBytes(Memory.ReadUInt(_retnInjectionAsm), returnLength);
                        //}
                        //else
                        //{
                        //    var retnByte = new List<byte>();
                        //    uint dwAddress = Memory.ReadUInt(_retnInjectionAsm);
                        //    if (dwAddress != 0)
                        //    {
                        //        Log.Write("dwAddress {0}", dwAddress);
                        //        byte buf = Memory.ReadByte(dwAddress);
                        //        while (buf != 0)
                        //        {
                        //            retnByte.Add(buf);
                        //            dwAddress = dwAddress + 1;
                        //            buf = Memory.ReadByte(dwAddress);
                        //            Log.Write("buf: {0}", buf);
                        //        }
                        //    }
                        //    tempsByte = retnByte.ToArray();
                        //}
                    }
                    catch (Exception ex)
                    {
                        Log.Write(ex.ToString());
                    }
                    finally
                    {
                        // Free memory allocated 
                        //Memory.FreeMemory(injectionAsmCodecave);
                        // schedule resources to be freed at a later date cause freeing it immediately was causing wow crashes
                        new Timer((state) => { Memory.FreeMemory((uint)state); }, injectionAsmCodecave, 100, 0);
                    }
                }
                // return
                return tempsByte;
            }
        }

        static string[] _registerNames = new string[] { "AH", "AL", "BH", "BL", "CH", "CL", "DH", "DL", "EAX", "EBX", "ECX", "EDX" };
        // asm isnt my forte.. but this should mess up any hash scans...
        void InsertRandomOpCodes(BlackMagic mem)
        {
            if (Utility.Rand.Next(10) < 3)
                return;
            int ranNum = Utility.Rand.Next(0, _registerNames.Length + 1);
            // insert a NOP or 2
            if (ranNum == _registerNames.Length)
            {
                mem.Asm.AddLine("nop");
                if (Utility.Rand.Next(2) == 0)
                    mem.Asm.AddLine("nop");
            }
            else
            {
                mem.Asm.AddLine("mov " + _registerNames[ranNum] + "," + _registerNames[ranNum]);
            }
        }

        /// <summary>
        /// Scans for new memory offsets and saves them in WoWSettings. 
        /// </summary>
        void ScanForOffset()
        {
            if (Memory != null)
            {
                HBRelog.Settings.DxDeviceOffset = WoWPatterns.Dx9DevicePattern.Find(Memory);
                Log.Debug("DxDevice9 Offset found at 0x{0:X}", HBRelog.Settings.DxDeviceOffset);
                HBRelog.Settings.DxDeviceIndex = Memory.ReadUInt(WoWPatterns.Dx9DeviceInxPattern.Find(Memory) + BaseOffset);
                Log.Debug("DxDevice9 Index is 0x{0:X}", HBRelog.Settings.DxDeviceIndex);
                HBRelog.Settings.GameStateOffset = WoWPatterns.GameStatePattern.Find(Memory);
                Log.Debug("GameState Offset found at 0x{0:X}", HBRelog.Settings.GameStateOffset);
                HBRelog.Settings.FrameScriptExecuteOffset = WoWPatterns.FrameScriptExecutePattern.Find(Memory);
                Log.Debug("FrameScriptExecute Offset found at 0x{0:X}", HBRelog.Settings.FrameScriptExecuteOffset);
                HBRelog.Settings.LastHardwareEventOffset = WoWPatterns.LastHardwareEventPattern.Find(Memory);
                Log.Debug("LastHardwareEvent Offset found at 0x{0:X}", HBRelog.Settings.LastHardwareEventOffset);
                HBRelog.Settings.GlueStateOffset = WoWPatterns.GlueStatePattern.Find(Memory);
                Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HBRelog.Settings.GlueStateOffset);
                HBRelog.Settings.WowVersion = WoWVersion;
                HBRelog.Settings.Save();
            }
            else
                throw new InvalidOperationException("Can not scan for offsets before attaching to process");
        }


        /// <summary>
        /// Returns the time that WoW.exe was modified last in a DateTime
        /// </summary>
        private string WoWVersion
        {
            get { return _wowProcess.MainModule.FileVersionInfo.FileVersion; }
        }
    }
}
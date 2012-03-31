// credits to Rival from www.mmowned.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using HighVoltz.HBRelog;
using System.Linq;
using Magic;
using System.Windows;

namespace HighVoltz.HBRelog.WoW
{
    public class Hook
    {
        // Addresse Inection code:
        private readonly object _executeLockObject = new object();
        private readonly Process _wowProcess;
        private uint _addresseInjection;
        private uint _injectedCode;
        private uint _retnInjectionAsm;

        private byte[] _endSceneOriginalBytes;

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
                // check if target is 64 bit
                if (Utility.Is64BitProcess(_wowProcess))
                {
                    throw new InvalidOperationException("Only 32bit Wow is supported");
                }
                // check if we need to scan for offsets
                if (string.IsNullOrEmpty(HBRelogManager.Settings.WowVersion) || !HBRelogManager.Settings.WowVersion.Equals(WoWVersion))
                    ScanForOffset();
                // Get address of EndScene
                uint pDevice = Memory.ReadUInt(HBRelogManager.Settings.DxDeviceOffset + BaseOffset);
                uint pEnd = Memory.ReadUInt(pDevice + HBRelogManager.Settings.DxDeviceIndex);
                if (pEnd == 0)
                {
                    throw new InvalidOperationException("Wow needs to be using DirectX 9");
                }
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
                    AddAsmAndRandomOPs("pushad");
                    AddAsmAndRandomOPs("pushfd");
                    // Test if you need launch injected code:
                    AddAsmAndRandomOPs("mov eax, [" + _addresseInjection + "]");
                    AddAsmAndRandomOPs("test eax, eax");
                    AddAsmAndRandomOPs("je @out");
                    // Launch Fonction:
                    AddAsmAndRandomOPs("mov eax, [" + _addresseInjection + "]");
                    AddAsmAndRandomOPs("call eax");
                    // Copy pointer return value:
                    AddAsmAndRandomOPs("mov [" + _retnInjectionAsm + "], eax");
                    // Enter value 0 of addresse func inject
                    AddAsmAndRandomOPs("mov edx, " + _addresseInjection);
                    AddAsmAndRandomOPs("mov ecx, 0");
                    AddAsmAndRandomOPs("mov [edx], ecx");

                    // Close func
                    AddAsmAndRandomOPs("@out:");

                    // load reg
                    AddAsmAndRandomOPs("popfd");
                    AddAsmAndRandomOPs("popad");

                    // injected code
                    var sizeAsm = (uint)(Memory.Asm.Assemble().Length);
                    Memory.Asm.Inject(_injectedCode);

                    // Size asm jumpback
                    const int sizeJumpBack = 2;

                    // store original bytes
                    _endSceneOriginalBytes = Memory.ReadBytes(pEndScene - 5, 7);
                    // copy and save original instructions
                    Memory.WriteBytes(_injectedCode + sizeAsm, new byte[] { _endSceneOriginalBytes[5], _endSceneOriginalBytes[6] }, 2);
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("jmp " + (pEndScene + sizeJumpBack)); // short jump takes 2 bytes.
                    // create jump back stub
                    Memory.Asm.Inject(_injectedCode + sizeAsm + sizeJumpBack);

                    // create hook jump
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("@top:");
                    Memory.Asm.AddLine("jmp " + (_injectedCode));
                    Memory.Asm.AddLine("jmp @top");

                    Memory.Asm.Inject(pEndScene - 5);
                    //}
                    Installed = true;
                }
            }
            catch (Exception ex)
            {
                Log.WriteToLog(ex.ToString());
                return false;
            }
            return Installed;
        }

        public void DisposeHooking()
        {
            try
            {
                // Get address of EndScene
                uint pDevice = Memory.ReadUInt(HBRelogManager.Settings.DxDeviceOffset + BaseOffset);
                uint pEnd = Memory.ReadUInt(pDevice + HBRelogManager.Settings.DxDeviceIndex);
                uint pScene = Memory.ReadUInt(pEnd);
                uint pEndScene = Memory.ReadUInt(pScene + 0xA8);
                byte ho = Memory.ReadByte(pEndScene);
                if (Memory.ReadByte(pEndScene) == 0xEB) // check if wow is already hooked and dispose Hook
                {
                    // Restore original endscene:
                    Memory.WriteBytes(pEndScene - 5, _endSceneOriginalBytes);
                }

                // free memory:
                Memory.FreeMemory(_injectedCode);
                Memory.FreeMemory(_addresseInjection);
                Memory.FreeMemory(_retnInjectionAsm);
                Installed = false;
            }
            catch (Exception ex)
            {
                Log.Write(ex.ToString());
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
        // This should mess up any hash scans...
        void InsertRandomOpCodes()
        {
            if (Utility.Rand.Next(10) < 3)
                return;
            int ranNum = Utility.Rand.Next(0, _registerNames.Length + 1);
            // insert a NOP or 2
            if (ranNum == _registerNames.Length)
            {
                Memory.Asm.AddLine("nop");
                if (Utility.Rand.Next(2) == 0)
                    Memory.Asm.AddLine("nop");
            }
            else
            {
                Memory.Asm.AddLine("mov " + _registerNames[ranNum] + "," + _registerNames[ranNum]);
            }
        }
        void AddAsmAndRandomOPs(string asm)
        {
            InsertRandomOpCodes();
            Memory.Asm.AddLine(asm);
            InsertRandomOpCodes();
        }
        /// <summary>
        /// Scans for new memory offsets and saves them in WoWSettings. 
        /// </summary>
        void ScanForOffset()
        {
            if (Memory != null)
            {
                HBRelogManager.Settings.DxDeviceOffset = WoWPatterns.Dx9DevicePattern.Find(Memory);
                Log.Debug("DxDevice9 Offset found at 0x{0:X}", HBRelogManager.Settings.DxDeviceOffset);
                HBRelogManager.Settings.DxDeviceIndex = Memory.ReadUInt(WoWPatterns.Dx9DeviceInxPattern.Find(Memory) + BaseOffset);
                Log.Debug("DxDevice9 Index is 0x{0:X}", HBRelogManager.Settings.DxDeviceIndex);
                HBRelogManager.Settings.GameStateOffset = WoWPatterns.GameStatePattern.Find(Memory);
                Log.Debug("GameState Offset found at 0x{0:X}", HBRelogManager.Settings.GameStateOffset);
                HBRelogManager.Settings.FrameScriptExecuteOffset = WoWPatterns.FrameScriptExecutePattern.Find(Memory);
                Log.Debug("FrameScriptExecute Offset found at 0x{0:X}", HBRelogManager.Settings.FrameScriptExecuteOffset);
                HBRelogManager.Settings.LastHardwareEventOffset = WoWPatterns.LastHardwareEventPattern.Find(Memory);
                Log.Debug("LastHardwareEvent Offset found at 0x{0:X}", HBRelogManager.Settings.LastHardwareEventOffset);
                HBRelogManager.Settings.GlueStateOffset = WoWPatterns.GlueStatePattern.Find(Memory);
                Log.Debug("GlueStateOffset Offset found at 0x{0:X}", HBRelogManager.Settings.GlueStateOffset);
                HBRelogManager.Settings.WowVersion = WoWVersion;
                HBRelogManager.Settings.Save();
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
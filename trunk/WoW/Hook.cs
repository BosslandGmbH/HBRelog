// credits to Rival from www.mmowned.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using GreyMagic;
using HighVoltz.HBRelog.DirectX;

namespace HighVoltz.HBRelog.WoW
{
    public class Hook
    {
        // Addresse Inection code:
        private readonly object _executeLockObject = new object();
        private readonly Process _wowProcess;
        private IntPtr _addresseInjection;
        private IntPtr _injectedCode;
        private IntPtr _retnInjectionAsm;
        private readonly Dirext3D _dx3D;
        private byte[] _endSceneOriginalBytes;

        public Hook(Process wowProc)
        {
            Process = wowProc;
            Memory = new ExternalProcessReader(wowProc);
            _dx3D = new Dirext3D(wowProc);
            _wowProcess = wowProc;
            Installed = false;
        }

        public ExternalProcessReader Memory { get; set; }

        public bool Installed { get; private set; }

        public Process Process { get; private set; }

        public int ProcessId { get { return Process.Id; } }

        bool UsingWin8 { get { return Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 2; } }

        public bool InstallHook()
        {
            try
            {
                if (Memory.IsProcessOpen)
                {
                    // check if game is already hooked and dispose Hook
                    if (Memory.Read<byte>(_dx3D.HookPtr) == 0xE9 &&
                        (_injectedCode == IntPtr.Zero || _addresseInjection == IntPtr.Zero))
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
                    Memory.Write<int>(_addresseInjection, 0);
                    // allocate memory the pointer return value:
                    _retnInjectionAsm = Memory.AllocateMemory(0x4);
                    Memory.Write<int>(_retnInjectionAsm, 0);

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
                    Memory.Asm.Inject((uint)_injectedCode);

                    // Size asm jumpback
                    const int sizeJumpBack = 2;

                    // store original bytes
                    _endSceneOriginalBytes = Memory.ReadBytes(_dx3D.HookPtr - 5, 7);
                    // copy and save original instructions
                    Memory.WriteBytes(IntPtr.Add(_injectedCode, (int) sizeAsm), new[] { _endSceneOriginalBytes[5], _endSceneOriginalBytes[6] });
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("jmp " + ((uint)_dx3D.HookPtr + sizeJumpBack)); // short jump takes 2 bytes.
                    // create jump back stub
                    Memory.Asm.Inject((uint)_injectedCode + sizeAsm + sizeJumpBack);

                    // create hook jump
                    Memory.Asm.Clear();
                    Memory.Asm.AddLine("@top:");
                    Memory.Asm.AddLine("jmp " + (_injectedCode));
                    Memory.Asm.AddLine("jmp @top");

                    Memory.Asm.Inject((uint)_dx3D.HookPtr - 5);
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
                /*
                // Get address of EndScene
                uint pDevice = Memory.ReadUInt(HbRelogManager.Settings.DxDeviceOffset + _wowProcess.BaseOffset());
                uint pEnd = Memory.ReadUInt(pDevice + HbRelogManager.Settings.DxDeviceIndex);
                uint pScene = Memory.ReadUInt(pEnd);
                uint pEndScene = Memory.ReadUInt(pScene + 0xA8);
                 */
                if (Memory.Read<byte>(_dx3D.HookPtr) == 0xEB) // check if wow is already hooked and dispose Hook
                {
                    // Restore original endscene:
                    Memory.WriteBytes(_dx3D.HookPtr - 5, _endSceneOriginalBytes);
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
                Memory.Write<int>(_retnInjectionAsm, 0);

                if (Memory.IsProcessOpen && Installed)
                {
                    // Write the asm stuff
                    Memory.Asm.Clear();
                    foreach (string tempLineAsm in asm)
                    {
                        Memory.Asm.AddLine(tempLineAsm);
                    }

                    // Allocation Memory
                    IntPtr injectionAsmCodecave = Memory.AllocateMemory(Memory.Asm.Assemble().Length);

                    try
                    {
                        // Inject
                        Memory.Asm.Inject((uint)injectionAsmCodecave);
                        Memory.Write<int>(_addresseInjection, (int)injectionAsmCodecave);
                        while (Memory.Read<int>(_addresseInjection) > 0)
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
                        new Timer(state => Memory.FreeMemory((IntPtr)state), injectionAsmCodecave, 100, 0);
                    }
                }
                // return
                return tempsByte;
            }
        }

        static readonly string[] RegisterNames = new[] { "AH", "AL", "BH", "BL", "CH", "CL", "DH", "DL", "EAX", "EBX", "ECX", "EDX" };
        static readonly string[] Register32BitNames = new[] { "EAX", "EBX", "ECX", "EDX" };

        // This should mess up any hash scans...
        void InsertRandomOpCodes()
        {
            for (int n = Utility.Rand.Next(1); n >= 0; n--)
            {
                var ranNum = Utility.Rand.Next(0, 6);
                if (ranNum == 0)
                {
                    Memory.Asm.AddLine("nop");
                    if (Utility.Rand.Next(2) == 1)
                        Memory.Asm.AddLine("nop");
                }
                else if (ranNum <= 5)
                    InsertRandomMov();
                //else
                //    InsertRandomPushPop();
            }
        }

        void AddAsmAndRandomOPs(string asm)
        {
            InsertRandomOpCodes();
            Memory.Asm.AddLine(asm);
            InsertRandomOpCodes();
        }

        void InsertRandomMov()
        {
            var ranNum = Utility.Rand.Next(0, RegisterNames.Length);
            Memory.Asm.AddLine("mov {0}, {1}", RegisterNames[ranNum], RegisterNames[ranNum]);
        }

        private void InsertRandomPushPop()
        {
            int ranNum = Utility.Rand.Next(0, Register32BitNames.Length);
            Memory.Asm.AddLine("push {0}", Register32BitNames[ranNum]);
            Memory.Asm.AddLine("pop {0}", Register32BitNames[ranNum]);
        }

        private IntPtr _fixHBStub;

        private void FixEndSceneForHB(IntPtr pEndScene)
        {
            Memory.Asm.Clear();
            _fixHBStub = Memory.AllocateMemory(0x200);

            AddAsmAndRandomOPs("push ebx");
            AddAsmAndRandomOPs("mov bl, [" + pEndScene + "]");
            AddAsmAndRandomOPs("cmp bl, 0xE9"); // check for the long jmp that hb uses.
            AddAsmAndRandomOPs("jnz @HbIsNotHooked");
            AddAsmAndRandomOPs("pop ebx"); // first pop the ebx register we pushed to the stack
            AddAsmAndRandomOPs("pop ebp"); // then pop the ebp register HB pushed to the stack
            AddAsmAndRandomOPs("jmp @original");
            Memory.Asm.AddLine("@HbIsNotHooked:");
            AddAsmAndRandomOPs("pop ebx");
            Memory.Asm.AddLine("@original:");
            AddAsmAndRandomOPs("Push 0x14");
            AddAsmAndRandomOPs("Mov Eax, " + Memory.Read<uint>(pEndScene + 3));
            IntPtr funcOffset =(pEndScene + 0xC) + Memory.Read<int>(pEndScene + 8);
            AddAsmAndRandomOPs("Call " + ((uint)funcOffset - (uint)_fixHBStub));

            // jump back to endscene
            AddAsmAndRandomOPs("Jmp " + ((uint)pEndScene + 0xC - (uint) _fixHBStub));
            
            Memory.WriteBytes(_fixHBStub, Memory.Asm.Assemble());

            // pad the top of Endscene with some single and 2 byte NOPs
            Memory.Asm.Clear();
            // try to randomize stub some.
            int rand = Utility.Rand.Next(0, 2);
            switch (rand)
            {
                case 0:
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    Memory.Asm.AddLine("Nop");
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    Memory.Asm.AddLine("Jmp " + ((uint)_fixHBStub - (uint)pEndScene));
                    break;
                case 1:
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    Memory.Asm.AddLine("Nop");
                    Memory.Asm.AddLine("Jmp " + ((uint)_fixHBStub - (uint)pEndScene));
                    if (Utility.Rand.Next(2) == 1) InsertRandomMov(); else Memory.Asm.Add("Nop\nNop\n");
                    break;
            }
            Memory.WriteBytes(pEndScene, Memory.Asm.Assemble());
            Memory.Asm.Clear();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HighVoltz.CleanPattern;

namespace HighVoltz.WoW
{
    static public class WoWPatterns
    {
        static public readonly Pattern GameStatePattern = Pattern.FromTextstyle("GameState", "? ? ? ? 0F 84 ? ? ? ? 8B 86 FC 00 00 00 8B 38 8B 40 04 89 45 FC E8 ? ? ? ? 3B F8 0F 85",
            new LeaModifier());
        static public readonly Pattern Dx9DevicePattern = Pattern.FromTextstyle("Dx9Device", "55 8B EC 8B 55 0C 8B 0D ? ? ? ? 8B 01 8B 80 ? ? ? ? 52 8B 55 08 52 FF D0 5D C3",
            new AddModifier(8), new LeaModifier());
        static public readonly Pattern Dx9DeviceInxPattern = Pattern.FromTextstyle("Dx9DeviceInx", "? ? ? ? 8B 08 8B 51 14 50 FF D2 85 C0 7D 2B 3D 27 08 76 88 75 24 68 11 11 11 11 6A 00 6A 01 6A 00 68");
        static public readonly Pattern FrameScriptExecutePattern = Pattern.FromTextstyle("FrameScriptExecute", "55 8B EC 51 83 05 ? ? ? ? 01 A1 ? ? ? ? 89 45 FC 74 12 83 3D ? ? ? ? 00");
        static public readonly Pattern LastHardwareEventPattern = Pattern.FromTextstyle("LastHardwareEvent", "53 8B 1D ? ? ? ? 57 8D BE F8 00 00 00 7E 3F 8B 86 00 01 00 00 8B 80 B0 00 00 00 85 C0 74 06 F6 40 20 80 74 29 8B CE",
            new AddModifier(3), new LeaModifier());
        static public readonly Pattern PerformanceCounterPattern = Pattern.FromTextstyle("PerformanceCounter", "2B 15 ? ? ? ? 83 3D ? ? ? ? 00 A3 ? ? ? ? 74 13 8B 0D ? ? ? ?",
            new AddModifier(2), new LeaModifier());
        static public readonly Pattern GlueStatePattern = Pattern.FromTextstyle("GlueState", "83 3D ? ? ? ? 00 75 11 E8 ? ? ? ? 8B 10 8B C8 8B 82 88 00 00 00 FF E0 C3",
            new AddModifier(2), new LeaModifier());
    }
}

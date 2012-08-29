using HighVoltz.HBRelog.CleanPattern;

namespace HighVoltz.HBRelog.WoW
{
    static public class WoWPatterns
    {
        static public readonly Pattern GlueStatePattern = Pattern.FromTextstyle("GlueState", "83 3D ? ? ? ? 00 75 11 E8 ? ? ? ? 8B 10 8B C8 8B 82 88 00 00 00 FF E0 C3",
            new AddModifier(2), new LeaModifier());

        static public readonly Pattern GameStatePattern = Pattern.FromTextstyle("GameState", "38 1D ? ? ? ? 0F 85 ? ? ? ? 56 57 C6 05 ? ? ? ? 01 E8 ? ? ? ? 68 ? ? ? ? 68 ? ? ? ? 6A 10 52 50 E8 ? ? ? ? 8B F0 8B 06",
            new AddModifier(2), new LeaModifier());

        static public readonly Pattern FrameScriptExecutePattern = Pattern.FromTextstyle("FrameScriptExecute", "55 8B EC 51 FF 05 ? ? ? ? A1 ? ? ? ? 89 45 FC 74 12 83 3D ? ? ? ? 00 75 09 8B 4D 10");

        static public readonly Pattern Dx9DevicePattern = Pattern.FromTextstyle("Dx9Device", "55 8B EC 8B 55 0C 8B 0D ? ? ? ? 8B 01 8B 80 ? ? ? ? 52 8B 55 08 52 FF D0 5D C3",
            new AddModifier(8), new LeaModifier());

        static public readonly Pattern Dx9DeviceInxPattern = Pattern.FromTextstyle("Dx9DeviceInx", "? ? ? ? 8B 08 8B 51 14 50 FF D2 85 C0 ? ? 3D 27 08 76 88 75 24 68 11 11 11 11 6A 00 6A 01");

        static public readonly Pattern LastHardwareEventPattern = Pattern.FromTextstyle("LastHardwareEvent", "A3 ? ? ? ? 8B 87 ? ? ? ? 89 5D E0 89 5D E8 C7 45 DC ? ? ? ? 89 55 F0 89 4D F4 C7 45",
            new AddModifier(1), new LeaModifier());

        static public readonly Pattern PerformanceCounterPattern = Pattern.FromTextstyle("PerformanceCounter", "2B 15 ? ? ? ? 83 3D ? ? ? ? 00 A3 ? ? ? ? 74 13 8B 0D ? ? ? ? 8B 01 52 8B 50 10",
            new AddModifier(2), new LeaModifier());
        
    }
}

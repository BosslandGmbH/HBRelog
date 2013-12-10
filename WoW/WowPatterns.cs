using HighVoltz.HBRelog.CleanPattern;

namespace HighVoltz.HBRelog.WoW
{
    public static class WowPatterns
    {
        public static readonly Pattern GlueStatePattern = Pattern.FromTextstyle(
            "GlueState", "83 3d ?? ?? ?? ?? ?? 75 ?? e8 ?? ?? ?? ?? 8b 10 8b c8 ff a2 88 00 00 00 c3", new AddModifier(2), new LeaModifier());

        public static readonly Pattern GameStatePattern = Pattern.FromTextstyle(
            "GameState",
            "38 1d ?? ?? ?? ?? 0f 85 ?? ?? ?? ?? 56 57 c6 05 ?? ?? ?? ?? ?? e8 ?? ?? ?? ?? 8b f0 8b 06 8b ce ff 90 2c 01 00 00 3b c3 74",
            new AddModifier(2),
            new LeaModifier());

        //   static public readonly Pattern FrameScriptExecutePattern = Pattern.FromTextstyle("FrameScriptExecute", "55 8b ec 51 ff 05 ?? ?? ?? ?? a1 ?? ?? ?? ?? 53 56 57 8b 3d ?? ?? ?? ?? 6a 00 89 45 fc be ?? ?? ?? ?? 5b 74 ?? 39 1d ?? ?? ?? ?? 75 ?? 8b 4d 10 89 0d");

        //public static readonly Pattern LastHardwareEventPattern = Pattern.FromTextstyle(
        //    "LastHardwareEvent",
        //    "89 0d ?? ?? ?? ?? 85 ff 74 ?? 56 8d 4d d8 e8 ?? ?? ?? ?? 8b 07 8d 4d d8 51 8b cf c7 45 e0 66 00 06 40 ff 90 a4 00 00 00 33 c0 40 eb",
        //    new AddModifier(2),
        //    new LeaModifier());


        // ref - FrameXML:EditBox:HasFocus
        public static readonly Pattern FocusedWidgetPattern = Pattern.FromTextstyle(
			"FocusedWidget", "3b 35 ?? ?? ?? ?? 75 ?? 8b b6 50 02 00 00 81 4e 64 00 00 00 02 8d 4e 20 8b 01 6a 00 ff 50 50 eb", new AddModifier(2), new LeaModifier());

        // ref - Framescript_ExecuteBuffer.
        public static readonly Pattern LuaStatePattern = Pattern.FromTextstyle(
            "LuaState",
            "a1 ?? ?? ?? ?? 53 56 57 8b 3d ?? ?? ?? ?? 6a 00 89 45 fc be ?? ?? ?? ?? 5b 74 ?? 39 1d ?? ?? ?? ?? 75 ?? 8b 4d 10",
            new AddModifier(1),
            new LeaModifier());
    }
}
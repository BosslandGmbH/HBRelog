using HighVoltz.HBRelog.CleanPattern;

namespace HighVoltz.HBRelog.WoW
{
    public static class WowPatterns
    {
        public static readonly Pattern GlueStatePattern = Pattern.FromTextstyle(
            "GlueState", "83 3d ?? ?? ?? ?? ?? 75 ?? e8 ?? ?? ?? ?? 8b 10 8b c8 ff 62 5c c3", new AddModifier(2), new LeaModifier());

        public static readonly Pattern GameStatePattern = Pattern.FromTextstyle(
            "GameState",
            "80 3d ?? ?? ?? ?? ?? 74 ?? 50 b9 ?? ?? ?? ?? e8 ?? ?? ?? ?? 85 c0 74 ?? 8b 40 08 83 f8 02 74 ?? 83 f8 01 75 ?? b0 01 c3 32 c0 c3",
            new AddModifier(2),
            new LeaModifier());

        // ref - FrameXML:EditBox:HasFocus
        public static readonly Pattern FocusedWidgetPattern = Pattern.FromTextstyle(
            "FocusedWidget", "3b 05 ?? ?? ?? ?? 0f 94 c1 51 ff 75 08 e8 ?? ?? ?? ?? 33 c0 83 c4 10 40 5d c3", new AddModifier(2), new LeaModifier());

        // ref - Framescript_ExecuteBuffer.
        public static readonly Pattern LuaStatePattern = Pattern.FromTextstyle(
            "LuaState",
            "8b 35 ?? ?? ?? ?? 33 db 57 3b c3 74 ?? 88 18 ff 75 08 8d 85 dc fe ff ff 68 ?? ?? ?? ?? 68 ?? ?? ?? ?? 50",
            new AddModifier(2),
            new LeaModifier());
        // 
    }
}
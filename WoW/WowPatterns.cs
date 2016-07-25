using HighVoltz.HBRelog.CleanPattern;

namespace HighVoltz.HBRelog.WoW
{
    public static class WowPatterns
    {
        public static readonly Pattern GameStatePattern = Pattern.FromTextstyle(
            "GameState",
			"? ? ? ? ? 74 3A 6A 00 E8 ? ? ? ? 59 39 45 10 74 08 6A 00 E8 ? ? ? ? 59 6A 01 E8 ? ? ? ? 59 39 45 0C 74 08",
            new LeaModifier());

        // ref - FrameXML:EditBox:HasFocus
        public static readonly Pattern FocusedWidgetPattern = Pattern.FromTextstyle(
            "FocusedWidget", "55 8b ec 6a 00 e8 ? ? ? ? 50 ff 75 08 e8 ? ? ? ? 33 c9 3b 05 ? ? ? ? 0f 94 c1 51 ff 75 08 e8 ? ? ? ? 33 c0 83 c4 14 40 5d c3", new AddModifier(23), new LeaModifier());

        // ref - Framescript_ExecuteBuffer.
        public static readonly Pattern LuaStatePattern = Pattern.FromTextstyle(
            "LuaState",
			"? ? ? ? 6a 00 ff 75 0c 56 e8 ? ? ? ? ff 75 08 56 e8 ? ? ? ? 6a fe 56 e8 ? ? ? ? 68 ? ? ? ? 56 e8 ? ? ? ? 83 c4 24 5e 5d c3",
            new LeaModifier());

		// first offset used in 'LoadingScreenEnable' function. This function also fires the 'LOADING_SCREEN_ENABLED' lua event.
	    public static readonly Pattern LoadingScreenEnableCountPattern = Pattern.FromTextstyle(
		    "LoadingScreenEnableCount",
			"? ? ? ? 83 EC 18 48 A3 ? ? ? ? 85 C0 0F 8F ? ? ? ? 53 79 18 33 DB 89 1D ? ? ? ? E8 ? ? ? ? 89 1D ? ? ? ? E9",
		    new LeaModifier());

	    // 
    }
}
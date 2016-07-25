namespace HighVoltz.HBRelog.WoW.FrameXml
{
    static class Offsets
    {
        public static class UIObject
        {
            public const int GetTypeVtmOffset = 0x20;
            public const int GetFontTypeVtmOffset = 0x1C;
            public const int NamePtrOffset = 0x14;
			// Needs updating
            public const int FontNamePtrOffset = 0x54;
        }

        public static class ParentedObject
        {
            public const int ParentOffset = 0x84;
        }

        public static class Region
        {
			// This is actually 'EffectiveScale'
			public const int UIScaleOffset = 0x78;
            public const int BottomOffset = 0x4C;
            public const int LeftOffset = 0x50;
            public const int TopOffset = 0x54;
            public const int RightOffset = 0x58;
        }

        public static class VisibleRegion
        {
            public const int FlagsOffset = 0x80;
            public const int IsVisibleRShiftAmount = 19;
            public const int IsShownRShiftAmount = 18;
        }

        public static class Frame // Inherits ScriptObject and VisibleFrame
        {
            public const int ChildrenOffset = 0x148; 
            public const int RegionsSizeOffset = 0x128;  
            public const int RegionsOffset = 0x130;  
            public const int IdOffset = 0x94;
            public const int LevelOffset = 0x158;
            public const int StrataOffset = 0x157;  
        }

        public static class Button // inherits Frame
        {
            public const int FlagsOffset = 0x170;  
            public const int FontStringOffset = 0x178;  
            public const int HighlightTextureOffset = 0x1A0; 
        }

        public static class FontString
        {
            public const int TextOffset = 0xE4;
        }

        public static class EditBox // Inherits FontInstance and Frame
        {
            public const int FlagsOffset = 0x17C;  
            public const int AsciiCursorPositionOffset = 0x1D8; 
            public const int TextOffset = 0x190;  
            public const int MaxBytesOffset = 0x1A0;  
            public const int MaxLettersOffset = 0x1A4;  
            public const int IsEnabledFlagOffset = 0x98;  
            public const int IsEnabledBit = 8;
            public const int IsAutoFocus = 0x1;
            public const int IsMultilineBit = 0x2;
            public const int IsNumericBit = 0x4;
            public const int IsPasswordBit = 0x8;
            public const int IsCountInvisibleLettersBit = 0x20;
        }

        public static class ScrollFrame // Inherits from Frame
        {
            public const int ScrollChildOffset = 0x164;  
            public const int HorizontalScrollRangeOffset = 0x168; 
            public const int VerticalScrollRangeOffset = 0x16C;
			public const int HorizontalScrollOffset = 0x170;  
            public const int VerticalScrollOffset = 0x174;  
        }

        public static class Slider // Inherits from Frame
        {
            public const int IsEnabledFlagOffset = 0x98;
            public const int MinValueOffset = 0x160;  
            public const int MaxValueOffset = 0x164;  
            public const int ValueOffset = 0x168;  
            public const int ValueStepOffset = 0x16C;  
            public const int ThumbTextureOffset = 0x174; 
            public const int OrientationOffset = 0x170;  
            public const int IsEnabledBit = 8;
        }

        public static class Texture // Inherits From Layered Region
        {
			// Needs fixing
            public const int TexturePathObjectOffset = 0xC0;
			// Needs fixing
			public const int TexturePathOffset = 0x18;
        }

    }
}

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    static class Offsets
    {
        public static class UIObject
        {
            public const int GetTypeVtmOffset = 0x20;
            public const int GetFontTypeVtmOffset = 0x1C;
            public const int NamePtrOffset = 0x1C;
            public const int FontNamePtrOffset = 0x5C;
        }

        public static class ParentedObject
        {
            public const int ParentOffset = 0x98;
        }

        public static class Region
        {
            public const int UIScaleOffset = 0x80;
            public const int BottomOffset = 0x68;
            public const int LeftOffset = 0x6C;
            public const int TopOffset = 0x70;
            public const int RightOffset = 0x74;
        }

        public static class VisibleRegion
        {
            public const int FlagsOffset = 0x64;
            public const int IsVisibleRShiftAmount = 26;
            public const int IsShownRShiftAmount = 25;
        }

        public static class Frame // Inherits ScriptObject and VisibleFrame
        {
            public const int ChildrenOffset = 0x1BC; 
            public const int RegionsSizeOffset = 0x160;  
            public const int RegionsOffset = 0x168;  
            public const int IdOffset = 0xC4;
            public const int LevelOffset = 0xD8;
            public const int StrataOffset = 0x1E3;  
        }

        public static class Button // inherits Frame
        {
            public const int FlagsOffset = 0x1F0;  
            public const int FontStringOffset = 0x1F8;  
            public const int HighlightTextureOffset = 0x220; 
        }

        public static class FontString
        {
            public const int TextOffset = 0xF8;
        }

        public static class EditBox // Inherits FontInstance and Frame
        {
            public const int FlagsOffset = 0x1F8;  
            public const int AsciiCursorPositionOffset = 0x24C; 
            public const int TextOffset = 0x20C;  
            public const int MaxBytesOffset = 0x21C;  
            public const int MaxLettersOffset = 0x220;  
            public const int IsEnabledFlagOffset = 0xC8;  
            public const int IsEnabledBit = 0x400;
            public const int IsAutoFocus = 0x1;
            public const int IsMultilineBit = 0x2;
            public const int IsNumericBit = 0x4;
            public const int IsPasswordBit = 0x8;
            public const int IsCountInvisibleLettersBit = 0x20;
        }

        public static class ScrollFrame // Inherits from Frame
        {
            public const int ScrollChildOffset = 0x1E8;  
            public const int HorizontalScrollRangeOffset = 0x1EC; 
            public const int VerticalScrollRangeOffset = 0x1F0;  
            public const int HorizontalScrollOffset = 0x1F4;  
            public const int VerticalScrollOffset = 0x1F8;  
        }

        public static class Slider // Inherits from Frame
        {
            public const int IsEnabledFlagOffset = 0xC8;
            public const int MinValueOffset = 0x1E4;  
            public const int MaxValueOffset = 0x1E8;  
            public const int ValueOffset = 0x1EC;  
            public const int ValueStepOffset = 0x1F0;  
            public const int ThumbTextureOffset = 0x1F8; 
            public const int OrientationOffset = 0x1F4;  
            public const int IsEnabledBit = 0x400;
        }

        public static class Texture // Inherits From Layered Region
        {
            public const int TexturePathObjectOffset = 0xC0;
            public const int TexturePathOffset = 0x18;
        }

    }
}

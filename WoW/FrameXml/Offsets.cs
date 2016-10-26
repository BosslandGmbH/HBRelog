namespace HighVoltz.HBRelog.WoW.FrameXml
{
    internal static class Offsets
    {
        public static class UIObject
        {
            public const int GetTypeNameVfuncOffset = 0x18;
            public const int NamePtrOffset = 0x14;
            // public const int FontNamePtrOffset = 0x5C;
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
            public const int ChildrenOffset = 0x130; 
            public const int RegionsSizeOffset = 0x110;  
            public const int RegionsOffset = 0x118;
            public const int IdOffset = 0x90;
            // In GetFrameLevel function
            public const int LevelOffset = 0x140;
            // In Frame::GetFrameStrata function
            public const int StrataOffset = 0x13F;
        }

        public static class Button // inherits Frame
        {
            // first offset in Button::GetButtonState function
            public const int FlagsOffset = 0x158;
            // first offset in Button::GetFontString function
            public const int FontStringOffset = 0x160;
            // first offset in Button::GetHighlightTexture function
            public const int HighlightTextureOffset = 0x188; 
        }

        public static class FontString
        {
            // Offset in FontString:GetText function
            public const int TextOffset = 0xE4;
        }

        public static class EditBox // Inherits FontInstance and Frame
        {
            // In EditBox:IsMultiLine function
            public const int FlagsOffset = 0x154;
            // In EditBox:GetCursorPosition function
            public const int AsciiCursorPositionOffset = 0x1B0;
            // Last offset in EditBox:GetText function
            public const int TextOffset = 0x168;
            // In EditBox:GetMaxBytes function
            public const int MaxBytesOffset = 0x178;
            // In EditBox:GetMaxLetters function
            public const int MaxLettersOffset = 0x17C;  
            public const int IsEnabledFlagOffset = 0x94;  
            public const int IsEnabledBit = 8;

        }

        public static class ScrollFrame // Inherits from Frame
        {
            // In ScrollFrame:GetScrollChild function
            public const int ScrollChildOffset = 0x14C;
            // In ScrollFrame:GetHorizontalScrollRange function
            public const int HorizontalScrollRangeOffset = 0x150;
            // In ScrollFrame:GetVerticalScrollRange function
            public const int VerticalScrollRangeOffset = 0x154;
            // In ScrollFrame:GetHorizontalScroll function
            public const int HorizontalScrollOffset = 0x158;
            // In ScrollFrame:GetVerticalScroll function
            public const int VerticalScrollOffset = 0x15C;  
        }

        public static class Slider // Inherits from Frame
        {
            public const int Flags = 0x98;
            // Lowest offset in Slider:GetMinMaxValues()
            public const int MinValueOffset = 0x148;
            // Highest offset in Slider:GetMinMaxValues()
            public const int MaxValueOffset = 0x14C;
            // In Slider:GetValue function
            public const int ValueOffset = 0x150;
            // In Slider:GetValueStep function
            public const int ValueStepOffset = 0x154;
            // In Slider:Slider:SetThumbTexture function
            public const int ThumbTextureOffset = 0x15C;
            // In Slider:GetOrientation function
            public const int OrientationOffset = 0x158;  
        }

        public static class Texture // Inherits From Layered Region
        {
            // In Texture:GetTexture go to the 3rd function call (it has one argument). Offset is the first offset used in this function
            public const int TexturePathObjectOffset = 0xA8;
            // In Texture:GetTexture go to the 3rd function call (it has one argument), and then go inside the first and only function call. Offset is the only offset in this function.
            public const int TexturePathOffset = 0x188;
        }

    }
}

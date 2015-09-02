namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public interface IFontInstance
    {
        Font FontObject { get; }

        FontInfo FontInfo { get; }
    }

    public enum FontFlags
    {
        None,
        Outline,
        ThickOutline,
        Monochrome
    }

    public class FontInfo
    {
        public readonly string Name;
        public readonly float Height;
        public readonly FontFlags Flags;
    }
}

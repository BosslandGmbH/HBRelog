namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public enum UIObjectType
    {
        None,
        Unknown,
        Alpha,
        Animation,
        AnimationGroup,
        ArchaeologyDigSiteFrame,
        Browser,
        Button,
        CheckButton,
        ColorSelect,
        ControlPoint,
        Cooldown,
        DressUpModel,
        EditBox,
        Font,
        FontInstance, // abstract
        FontString, 
        Frame,    
        GameTooltip,
        LayeredRegion, // abstract
        MessageFrame,
        Minimap,
        Model,
        MovieFrame,
        ParentedObject,  // abstact.
        Path,
        PlayerModel,
        QuestPOIFrame,
        Region,         // abstract (can't be instanized but some objects might return this type)
        Rotation,
        Scale,
        ScenarioPOIFrame,
        ScriptObject,   // abstract
        ScrollFrame,
        ScrollingMessageFrame,
        SimpleHTML,
        Slider,
        StatusBar,
        TabardModel,
        Texture,
        Translation,
        UIObject,       // abstract
        VisbleRegion,   // abstract
    }
}

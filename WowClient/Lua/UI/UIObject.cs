using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace WowClient.Lua.UI
{

    public class UIObject
    {
        protected readonly WowLua Lua;

        protected UIObject(WowLua lua, IAbsoluteAddress address)
        {
            Address = address;
            Lua = lua;
        }

        public readonly IAbsoluteAddress Address;

        private string _name;
        public string Name
        {
            get
            {
                if (_name != null)
                    return _name;
                var offs = Type != UIObjectType.Font ?
                    Offsets.UIObject.NamePtrOffset :
                    Offsets.UIObject.FontNamePtrOffset;
                var ptr = Address.Deref(offs);
                _name = ptr.Value != IntPtr.Zero ?
                    Lua.Memory.ReadString(ptr, 128, Encoding.UTF8) :
                    "<unnamed>";
                return _name;
            }
        }

        public UIObjectType Type { get; private set; }

        private static void SetObjectType(IReadOnlyMemory memory, IAbsoluteAddress address)
        {
            var vtmAddress = address.Deref(Offsets.UIObject.GetTypeVtmOffset); // memory.Read<IntPtr>(ptr + Offsets.UIObject.GetTypeVtmOffset);
            if (!IsValidTypePtr(memory, vtmAddress))
                vtmAddress = address.Deref(Offsets.UIObject.GetFontTypeVtmOffset); // memory.Read<IntPtr>(ptr + Offsets.UIObject.GetFontTypeVtmOffset);

            if (IsValidTypePtr(memory, vtmAddress))
            {
                var strAddress = vtmAddress // memory.Read<IntPtr>(false, vtmPtr + 1, IntPtr.Zero)
                    .Deref(1)
                    .Deref();

                var str = memory.ReadString(strAddress, 128, Encoding.UTF8);
                TypeCache[address.Value] = GetUIObjectTypeFromString(str);
            }
            else
                TypeCache[address.Value] = UIObjectType.None;
        }

        private static UIObjectType GetUIObjectTypeFromString(string str)
        {
            switch (str)
            {
                case "Alpha":
                    return UIObjectType.Alpha;
                case "Animation":
                    return UIObjectType.Animation;
                case "AnimationGroup":
                    return UIObjectType.AnimationGroup;
                case "ArchaeologyDigSiteFrame":
                    return UIObjectType.ArchaeologyDigSiteFrame;
                case "Browser":
                    return UIObjectType.Browser;
                case "Button":
                    return UIObjectType.Button;
                case "CheckButton":
                    return UIObjectType.CheckButton;
                case "ColorSelect":
                    return UIObjectType.ColorSelect;
                case "ControlPoint":
                    return UIObjectType.ControlPoint;
                case "Cooldown":
                    return UIObjectType.Cooldown;
                case "DressUpModel":
                    return UIObjectType.DressUpModel;
                case "EditBox":
                    return UIObjectType.EditBox;
                case "Font":
                    return UIObjectType.Font;
                case "FontString":
                    return UIObjectType.FontString;
                case "Frame":
                    return UIObjectType.Frame;
                case "GameTooltip":
                    return UIObjectType.GameTooltip;
                case "MessageFrame":
                    return UIObjectType.MessageFrame;
                case "Minimap":
                    return UIObjectType.Minimap;
                case "Model":
                    return UIObjectType.Model;
                case "MovieFrame":
                    return UIObjectType.MovieFrame;
                case "Path":
                    return UIObjectType.Path;
                case "PlayerModel":
                    return UIObjectType.PlayerModel;
                case "QuestPOIFrame":
                    return UIObjectType.QuestPOIFrame;
                case "Rotation":
                    return UIObjectType.Rotation;
                case "Scale":
                    return UIObjectType.Scale;
                case "ScenarioPOIFrame":
                    return UIObjectType.ScenarioPOIFrame;
                case "ScrollFrame":
                    return UIObjectType.ScrollFrame;
                case "ScrollingMessageFrame":
                    return UIObjectType.ScrollingMessageFrame;
                case "SimpleHTML":
                    return UIObjectType.SimpleHTML;
                case "Slider":
                    return UIObjectType.Slider;
                case "StatusBar":
                    return UIObjectType.StatusBar;
                case "TabardModel":
                    return UIObjectType.TabardModel;
                case "Texture":
                    return UIObjectType.Texture;
                case "Translation":
                    return UIObjectType.Translation;
                default:
                    return UIObjectType.Unknown;
            }
        }

        private static bool IsValidTypePtr(IReadOnlyMemory memory, IAbsoluteAddress address)
        {
            if (address.Value == IntPtr.Zero) return false;
            try
            {
                // TODO what it is? 6... should not use unnamed constants
                var bytes = memory.ReadBytes(address, 6);
                return bytes[0] == 0xA1 /* mov */
                    && bytes[5] == 0xC3 /* retn */;
            }
            catch (AccessViolationException)
            {
                return false;
            }
        }

        // dictionary that caches vtm pointers for UIObject types
        private static readonly Dictionary<IntPtr, UIObjectType> TypeCache = new Dictionary<IntPtr, UIObjectType>();

        public static IEnumerable<UIObject> GetAll(WowLua wow)
        {
            return
                from node in wow.Globals.Nodes
                where node.Value.Type == LuaType.Table
                where node.Value.Pointer.Value != IntPtr.Zero
                where node.Value.Table.IsUIObject
                select Get(wow, node.Value.Table.LightUserData.Address);
        }

        public static void ResetTypeCache()
        {
            TypeCache.Clear();
        }

        public static T Get<T>(WowLua lua, string name) where T : UIObject
        {
            if (lua == null) throw new ArgumentException("lua is null", "lua");
            if (lua.Globals == null) throw new ArgumentException("lua.Globals is null", "lua.Globals");
            var value = lua.Globals.GetValue(name);
            if (value == null || value.Type != LuaType.Table)
            {
                return null;
            }
            if (value.Table.IsUIObject)
                return (T)Get(lua, value.Table.LightUserData.Address);
            return null;
        }

        public static IEnumerable<T> GetAll<T>(WowLua wow) where T : UIObject
        {
            return GetAll(wow).OfType<T>();
        }

        public static T Get<T>(WowLua lua, IAbsoluteAddress address) where T : UIObject
        {
            return (T)Get(lua, address);
        }

        public static UIObject Get(WowLua lua, IAbsoluteAddress address)
        {
            var vtmAddress = address.Deref(); // lua.Memory.Read<IntPtr>(address));
            if (!TypeCache.ContainsKey(vtmAddress.Value))
                SetObjectType(lua.Memory, vtmAddress);
            var type = TypeCache[vtmAddress.Value];
            switch (type)
            {
                case UIObjectType.Button:
                    return new Button(lua, address) { Type = type };
                case UIObjectType.EditBox:
                    return new EditBox(lua, address) { Type = type };
                case UIObjectType.Font:
                    return new Font(lua, address) { Type = type };
                case UIObjectType.FontString:
                    return new FontString(lua, address) { Type = type };
                case UIObjectType.Frame:
                    return new Frame(lua, address) { Type = type };
                case UIObjectType.ScrollFrame:
                    return new ScrollFrame(lua, address) { Type = type };
                case UIObjectType.Slider:
                    return new Slider(lua, address) { Type = type };
                case UIObjectType.Texture:
                    return new Texture(lua, address) { Type = type };
                default:
                    return new UIObject(lua, address) { Type = type };
            }
        }

    }
}

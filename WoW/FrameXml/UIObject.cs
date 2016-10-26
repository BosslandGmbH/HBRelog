using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GreyMagic;
using HighVoltz.HBRelog.Settings;
using HighVoltz.HBRelog.WoW.Lua;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class UIObject
    {
        protected readonly WowManager WowManager;

        protected UIObject(WowManager wowManager, IntPtr address)
        {
            Address = address;
            WowManager = wowManager;
        }

        public readonly IntPtr Address;

        private bool _triedGetName;
        private string _name;
        public string Name
        {
            get
            {
                if (!_triedGetName)
                {
                    var idx = Offsets.UIObject.NamePtrOffset;
                    var ptr = WowManager.Memory.Read<IntPtr>(Address + idx);
                    _name = ptr != IntPtr.Zero ? WowManager.Memory.ReadString(ptr, Encoding.UTF8, 128) : "<unnamed>";
                    _triedGetName = true;
                }
                return _name;
            }
        }

        public UIObjectType Type { get; private set; }

        #region Static Members

        public static bool IsUIObject(LuaTable table, out IntPtr lightUserDataPtr)
        {
            lightUserDataPtr = IntPtr.Zero;
            if (table == null)
                return false;
            var firstNode = table.Nodes.FirstOrDefault(n => n.Value.Type == LuaType.LightUserData);
            bool ret = firstNode != null;
            if (ret)
                lightUserDataPtr = firstNode.Value.Pointer;
            return ret;
        }

        private static void SetObjectType(ExternalProcessReader memory, IntPtr ptr)
        {
            var vtmPtr = memory.Read<IntPtr>(ptr + Offsets.UIObject.GetTypeNameVfuncOffset);
            if (IsValidTypePtr(memory, vtmPtr))
            {
                var strPtr = memory.Read<IntPtr>(false, vtmPtr + 1);
                var str = memory.ReadString(strPtr, Encoding.UTF8, 128);
                UIObjectTypeCache[ptr] = GetUIObjectTypeFromString(str);
            }
            else
                UIObjectTypeCache[ptr] = UIObjectType.None;
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

        private static bool IsValidTypePtr(ExternalProcessReader memory, IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return false;
            try
            {
                var bytes = memory.ReadBytes(ptr, 6);
                return bytes[0] == 0xB8 /* mov */&& bytes[5] == 0xC3 /* retn */;
            }
            catch
            {
                return false;
            }
        }

        // dictionary that caches vtm pointers for UIObject types
        private static readonly Dictionary<IntPtr, UIObjectType> UIObjectTypeCache = new Dictionary<IntPtr, UIObjectType>();

        public static IEnumerable<UIObject> GetUIObjects(WowManager wowManager)
        {
			foreach (var node in wowManager.Globals.Nodes)
            {
                if (node.Value.Type != LuaType.Table)
                    continue;
                if (node.Value.Pointer == IntPtr.Zero)
                {
                    continue;
                }
                IntPtr address;
                if (!IsUIObject(node.Value.Table, out address))
                    continue;

                yield return GetUIObjectFromPointer(wowManager, address);
            }
        }

        public static T GetUIObjectByName<T>(WowManager wowManager, string name) where T : UIObject
        {
            if (wowManager == null) throw new ArgumentException("wowManager is null", "wowManager");
            if (wowManager.Globals == null) throw new ArgumentException("wowManager.Globals is null", "wowManager.Globals");
			var value = wowManager.GetLuaObject(name);
			if (value == null || value.Type != LuaType.Table)
            {
                return null;
            }
            IntPtr ptr;
            if (IsUIObject(value.Table, out ptr))
                return (T)GetUIObjectFromPointer(wowManager, ptr);
            return null;
        }

        public static IEnumerable<T> GetUIObjectsOfType<T>(WowManager wowManager) where T : UIObject
        {
            return GetUIObjects(wowManager).OfType<T>();
        }

        public static T GetUIObjectFromPointer<T>(WowManager wowManager, IntPtr address) where T : UIObject
        {
            return (T)GetUIObjectFromPointer(wowManager, address);
        }

        public static UIObject GetUIObjectFromPointer(WowManager wowManager, IntPtr address)
        {
            var vtmPtr = wowManager.Memory.Read<IntPtr>(address);
            if (!UIObjectTypeCache.ContainsKey(vtmPtr))
                SetObjectType(wowManager.Memory, vtmPtr);
            var type = UIObjectTypeCache[vtmPtr];
            switch (type)
            {
                case UIObjectType.Button:
                    return new Button(wowManager, address) { Type = type };
                case UIObjectType.EditBox:
                    return new EditBox(wowManager, address) { Type = type };
                case UIObjectType.Font:
                    return new Font(wowManager, address) { Type = type };
                case UIObjectType.FontString:
                    return new FontString(wowManager, address) { Type = type };
                case UIObjectType.Frame:
                    return new Frame(wowManager, address) { Type = type };
                case UIObjectType.ScrollFrame:
                    return new ScrollFrame(wowManager, address) { Type = type };
				case UIObjectType.SimpleHTML:
					return new Frame(wowManager, address) { Type = type };
				case UIObjectType.Slider:
                    return new Slider(wowManager, address) { Type = type };
                case UIObjectType.Texture:
                    return new Texture(wowManager, address) { Type = type };
                default:
                    return new UIObject(wowManager, address) { Type = type };
            }
        }

        #endregion

    }
}

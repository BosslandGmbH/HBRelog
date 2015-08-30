using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public abstract class ParentedObject : UIObject
    {
        protected ParentedObject(WowLuaManager wowManager, IntPtr address) : base(wowManager, address) { }

        private UIObject _parent;
        private bool _triedToGetParent;
        public UIObject Parent
        {
            get
            {
                if (!_triedToGetParent)
                {
                    var parentPtr = LuaManager.Memory.Read<IntPtr>(Address + Offsets.ParentedObject.ParentOffset);
                    _parent = parentPtr != IntPtr.Zero ? GetUIObjectFromPointer(LuaManager, parentPtr) : null;
                    _triedToGetParent = true;
                }
                return _parent;
            }
        }
    }
}

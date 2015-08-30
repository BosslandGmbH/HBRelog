using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public abstract class ParentedObject : UIObject
    {
        protected ParentedObject(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        private UIObject _parent;
        private bool _triedToGetParent;
        public UIObject Parent
        {
            get
            {
                if (!_triedToGetParent)
                {
                    var parentPtr = WowManager.Memory.Read<IntPtr>(Address + Offsets.ParentedObject.ParentOffset);
                    _parent = parentPtr != IntPtr.Zero ? GetUIObjectFromPointer(WowManager, parentPtr) : null;
                    _triedToGetParent = true;
                }
                return _parent;
            }
        }
    }
}

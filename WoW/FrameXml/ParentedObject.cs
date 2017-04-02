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
                    var vft = WowManager.Memory.Read<IntPtr>(Address);
                    var func = WowManager.Memory.Read<IntPtr>(vft + 32);

                    var parentPtr = WowManager.Memory.Read<IntPtr>(Address + Offsets.ParentedObject.ParentOffset);
                    _parent = parentPtr != IntPtr.Zero ? GetUIObjectFromPointer(WowManager, parentPtr) : null;
                    _triedToGetParent = true;
                }
                return _parent;
            }
        }
    }
}

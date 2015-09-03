using System;

namespace WowClient.Lua.UI
{
    public abstract class ParentedObject : UIObject
    {
        protected ParentedObject(WowLua wow, IAbsoluteAddress address) : base(wow, address) { }

        private UIObject _parent;
        private bool _triedToGetParent;
        public UIObject Parent
        {
            get
            {
                if (!_triedToGetParent)
                {
                    var parentAddress = Address.Deref(Offsets.ParentedObject.ParentOffset); // Lua.Memory.Read<IntPtr>(Address + Offsets.ParentedObject.ParentOffset);
                    _parent = parentAddress.Value != IntPtr.Zero ? Get(Lua, parentAddress) : null;
                    _triedToGetParent = true;
                }
                return _parent;
            }
        }
    }
}

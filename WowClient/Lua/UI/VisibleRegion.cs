using System;

namespace WowClient.Lua.UI
{
    public abstract class VisibleRegion : Region
    {
        protected VisibleRegion(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

        /// <summary>
        /// Gets a value indicating whether this region is visible.
        /// </summary>
        /// <value>
        /// <c>true</c> if this region is visible; otherwise, <c>false</c>.
        /// </value>
        public bool IsVisible
        {
            get
            {
                var flags = Address.Deref<uint>(Offsets.VisibleRegion.FlagsOffset);
                return ((flags >> Offsets.VisibleRegion.IsVisibleRShiftAmount) & 1) != 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this region is shown.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this region is shown; otherwise, <c>false</c>.
        /// </value>
        public bool IsShown
        {
            get
            {
                var flags = Address.Deref<uint>(Offsets.VisibleRegion.FlagsOffset);
                return ((flags >> Offsets.VisibleRegion.IsShownRShiftAmount) & 1) != 0;
            }
        }
    }
}

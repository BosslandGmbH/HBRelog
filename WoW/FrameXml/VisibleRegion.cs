using System;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public abstract class VisibleRegion : Region
    {
        protected VisibleRegion(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

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
                var flags = WowManager.Memory.Read<uint>(Address + Offsets.VisibleRegion.FlagsOffset);
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
                var flags = WowManager.Memory.Read<uint>(Address + Offsets.VisibleRegion.FlagsOffset);
                return ((flags >> Offsets.VisibleRegion.IsShownRShiftAmount) & 1) != 0;
            }
        }
    }
}

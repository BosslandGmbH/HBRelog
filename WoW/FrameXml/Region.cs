using System;
using System.Drawing;
using System.Windows.Forms;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public abstract class Region : ParentedObject
    {
        private static readonly float SizeCo;
        private static readonly float SizeRatio;

        static Region()
        {
            var screen = Screen.PrimaryScreen;
            var bounds = screen.Bounds;
            var aspectRatio = (float)bounds.Width / bounds.Height;
            SizeCo = aspectRatio * 3 * 0.25f * 1024.0f;
            SizeRatio = 1.0f / (float)Math.Sqrt(aspectRatio * aspectRatio + 1f) * aspectRatio;
        }
        
        protected static float ToActualSize(float value)
        {
            return SizeCo*value/SizeRatio;
        }

        protected Region(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

        /// <summary>
        /// Gets the the distance from bottom of WoW window to bottom of region in pixels
        /// </summary>
        /// <value>
        /// The bottom.
        /// </value>
        public float Bottom
        {
            get
            {
                float scale = UIScale;
                var bot = WowManager.Memory.Read<float>(Address + Offsets.Region.BottomOffset);
                return ToActualSize(bot / scale);
            }
        }

        /// <summary>
        /// Gets the the distance from left of WoW window to left of region in pixels
        /// </summary>
        /// <value>
        /// The left.
        /// </value>
        public float Left
        {
            get
            {
                float scale = UIScale;
                var left = WowManager.Memory.Read<float>(Address + Offsets.Region.LeftOffset);
                return ToActualSize(left / scale);
            }
        }

        /// <summary>
        /// Gets the the distance from bottom of WoW window to top of region in pixels
        /// </summary>
        /// <value>
        /// The top.
        /// </value>
        public float Top
        {
            get
            {
                float scale = UIScale;
                var top = WowManager.Memory.Read<float>(Address + Offsets.Region.TopOffset);
                return ToActualSize(top / scale);
            }
        }

        /// <summary>
        /// Gets the the distance from left of WoW window to right of region in pixels
        /// </summary>
        /// <value>
        /// The right.
        /// </value>
        public float Right
        {
            get
            {
                float scale = UIScale;
                var right = WowManager.Memory.Read<float>(Address + Offsets.Region.RightOffset);
                return ToActualSize(right / scale);
            }
        }


        /// <summary>
        /// Gets width of the region in pixels
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public float Width
        {
            get { return Right - Left; }
        }

        /// <summary>
        /// Gets height of the region in pixels
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        public float Height
        {
            get { return Top - Bottom; }
        }

        /// <summary>
        /// Gets the the distance from left (x) and bottom (y) of WoW window to center of region in pixels
        /// </summary>
        /// <value>
        /// The center.
        /// </value>
        public PointF Center
        {
            get
            {
                var left = Left;
                var bottom = Bottom;
                var width = Right - left;
                var height = Top - bottom;
                return new PointF(left + (width / 2), bottom + (height / 2));
            }
        }

        /// <summary>
        /// Gets the the width and height of the region in pixels
        /// </summary>
        /// <value>
        /// The size.
        /// </value>
        public SizeF Size
        {
            get
            {
                return new SizeF(Width, Height);
            }
        }

        /// <summary>
        /// Gets the the distance from left (x) and bottom (y) of WoW window to left and bottom of region in pixels respectively and also return the width and height
        /// </summary>
        /// <value>
        /// The center.
        /// </value>
        public RectangleF Rect
        {
            get
            {
                var left = Left;
                var bottom = Bottom;
                var width = Right - left;
                var height = Top - bottom;
                return new RectangleF(left, bottom, width, height);
            }
        }

        float UIScale
        {
            get { return WowManager.Memory.Read<float>(Address + Offsets.Region.UIScaleOffset); }
        }
    }
}
using System;
using System.Drawing;
using System.Windows.Forms;
using Shared;

namespace WowClient.Lua.UI
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

        protected Region(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

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
                var bot = Address.Deref<float>(Offsets.Region.BottomOffset);
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
                var left = Address.Deref<float>(Offsets.Region.LeftOffset);
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
                var top = Address.Deref<float>(Offsets.Region.TopOffset);
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
                var right = Address.Deref<float>(Offsets.Region.RightOffset);
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
            get
            {
                return Address.Deref<float>(Offsets.Region.UIScaleOffset);
            }
        }

        public PointF ToWindowCoord()
        {
            var ret = new PointF();
            //var gameFullScreenFrame = UIObject.GetUIObjectByName<Frame>(LuaManager, "GlueParent") ?? UIObject.GetUIObjectByName<Frame>(LuaManager, "UIParent");
            var gameFullScreenFrame = this;
            while (gameFullScreenFrame != null
                && gameFullScreenFrame.Parent != null)
            {
                Region r;
                try
                {
                    r = (Region)gameFullScreenFrame.Parent;
                }
                catch (Exception)
                {
                    break;
                }
                gameFullScreenFrame = r;
            }
            if (gameFullScreenFrame == null)
                return ret;
            var gameFullScreenFrameRect = gameFullScreenFrame.Rect;
            var widget = this;
            var widgetCenter = widget.Center;
            var windowInfo = Utility.GetWindowInfo(Wrapper.WowProcess.MainWindowHandle);
            var leftBorderWidth = windowInfo.rcClient.Left - windowInfo.rcWindow.Left;
            var bottomBorderWidth = windowInfo.rcWindow.Bottom - windowInfo.rcClient.Bottom;
            var winClientWidth = windowInfo.rcClient.Right - windowInfo.rcClient.Left;
            var winClientHeight = windowInfo.rcClient.Bottom - windowInfo.rcClient.Top;
            var xCo = winClientWidth / gameFullScreenFrameRect.Width;
            var yCo = winClientHeight / gameFullScreenFrameRect.Height;

            ret.X = widgetCenter.X * xCo + leftBorderWidth;
            ret.Y = widgetCenter.Y * yCo + bottomBorderWidth;

            // flip the Y coord around because in WoW's UI coord space the Y goes up where as in windows it goes down.
            ret.Y = windowInfo.rcWindow.Bottom - windowInfo.rcWindow.Top - ret.Y;
            return ret;
        }

    }
}
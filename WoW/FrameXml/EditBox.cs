using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighVoltz.HBRelog.WoW.FrameXml
{
    public class EditBox : Frame, IFontInstance
    {
        public EditBox(WowManager wowManager, IntPtr address) : base(wowManager, address) { }

		/// <summary>
		/// Gets the cursor position. Works correctly with utf8 text.
		/// </summary>
		/// <value>
		/// The cursor position.
		/// </value>
		public int CursorPosition
        {
            get
            {
                var text = Text;
                if (string.IsNullOrEmpty(text)) return 0;
                var bytePos = WowManager.Memory.Read<int>(Address + Offsets.EditBox.AsciiCursorPositionOffset);
                // calculate position in a utf8 string.
                return text.Take(bytePos).Count();
            }
        }

        public EditBoxFlags EditBoxFlags => (EditBoxFlags)WowManager.Memory.Read<int>(Address + Offsets.EditBox.FlagsOffset);

	    public bool HasFocus => Address == WowManager.FocusedWidgetPtr;

	    /// <summary>
        /// Gets the max bytes. NOTE: this can return 0.
        /// </summary>
        /// <value>
        /// The max bytes.
        /// </value>
        public int MaxBytes => WowManager.Memory.Read<int>(Address + Offsets.EditBox.MaxBytesOffset) + 1;

	    /// <summary>
        /// Gets the max letters.
        /// </summary>
        /// <value>
        /// The max letters.
        /// </value>
        public int MaxLetters => WowManager.Memory.Read<int>(Address + Offsets.EditBox.MaxLettersOffset);

	    public int NumLetters
        {
            get
            {
                var text = Text;
                if (!IsNumeric)
                {
                    return text?.Length ?? 0;
                }
                return text?.Count(char.IsDigit) ?? 0;
            }
        }

        public string Text
        {
            get
            {
                var ptr = WowManager.Memory.Read<IntPtr>(Address + Offsets.EditBox.TextOffset);
                if (ptr == IntPtr.Zero) return string.Empty;
                var maxBytes = MaxBytes;
                var maxLetters = MaxLetters;
                return WowManager.Memory.ReadString(ptr, Encoding.UTF8, maxBytes > 0 ? maxBytes : maxLetters * 4);
            }
        }

        public float Number
        {
            get
            {
                if (!IsNumeric) return 0f;
                var text = Text;
                if (string.IsNullOrEmpty(text)) return 0f;
                float val;
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val);
                return val;
            }
        }

        public bool IsAutoFocus => (EditBoxFlags & EditBoxFlags.IsAutoFocus) != 0;

	    public bool IsEnabled => (WowManager.Memory.Read<int>(Address + Offsets.EditBox.IsEnabledFlagOffset) & Offsets.EditBox.IsEnabledBit) == 0;

	    public bool IsNumeric => (EditBoxFlags & EditBoxFlags.IsNumeric) != 0;

	    public bool IsPassword => (EditBoxFlags & EditBoxFlags.IsPassword) != 0;

	    public bool IsMultiline => (EditBoxFlags & EditBoxFlags.IsMultiline) != 0;

	    public bool IsCountInvisibleLetters => (EditBoxFlags & EditBoxFlags.IsCountInvisibleLetters) != 0;

	    public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }

    }

	[Flags]
	public enum EditBoxFlags
	{
		IsAutoFocus = 1,
		IsMultiline = 1 << 1,
		IsNumeric = 1 << 2,
		IsPassword = 1 << 3,
		IsCountInvisibleLetters = 1 << 5,
	}

}

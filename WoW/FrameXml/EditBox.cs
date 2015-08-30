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

        public int Flags
        {
            get { return WowManager.Memory.Read<int>(Address + Offsets.EditBox.FlagsOffset); }
        }

        public bool HasFocus
        {
            get { return Address == WowManager.FocusedWidgetPtr; }
        }

        /// <summary>
        /// Gets the max bytes. NOTE: this can return 0.
        /// </summary>
        /// <value>
        /// The max bytes.
        /// </value>
        public int MaxBytes
        {
            get { return WowManager.Memory.Read<int>(Address + Offsets.EditBox.MaxBytesOffset) + 1; }
        }

        /// <summary>
        /// Gets the max letters.
        /// </summary>
        /// <value>
        /// The max letters.
        /// </value>
        public int MaxLetters
        {
            get { return WowManager.Memory.Read<int>(Address + Offsets.EditBox.MaxLettersOffset); }
        }

        public int NumLetters
        {
            get
            {
                var text = Text;
                if (!IsNumeric)
                {
                    return text != null ? text.Length : 0;
                }
                return text != null ? text.Count(char.IsDigit) : 0;
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

        public bool IsAutoFocus
        {
            get { return (Flags & Offsets.EditBox.IsAutoFocus) != 0; }
        }

        public bool IsEnabled
        {
            get { return (WowManager.Memory.Read<int>(Address + Offsets.EditBox.IsEnabledFlagOffset) & Offsets.EditBox.IsEnabledBit) == 0; }
        }

        public bool IsNumeric
        {
            get { return (Flags & Offsets.EditBox.IsNumericBit) != 0; }
        }

        public bool IsPassword
        {
            get { return (Flags & Offsets.EditBox.IsPasswordBit) != 0; }
        }

        public bool IsMultiline
        {
            get { return (Flags & Offsets.EditBox.IsMultilineBit) != 0; }
        }

        public bool IsCountInvisibleLetters
        {
            get { return (Flags & Offsets.EditBox.IsCountInvisibleLettersBit) != 0; }
        }

        public Font FontObject { get { throw new NotImplementedException(); } }

        public FontInfo FontInfo { get { throw new NotImplementedException(); } }

    }
}

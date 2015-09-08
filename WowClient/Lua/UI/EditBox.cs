using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WowClient.Lua.UI
{
    public class EditBox : Frame, IFontInstance
    {
        public EditBox(WowWrapper wow, IAbsoluteAddress address) : base(wow, address) { }

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
                var bytePos = Address.Deref<int>(Offsets.EditBox.AsciiCursorPositionOffset);
                // calculate position in a utf8 string.
                return text.Take(bytePos).Count();
            }
        }

        public int Flags
        {
            get { return Address.Deref<int>(Offsets.EditBox.FlagsOffset); }
        }

        public bool HasFocus
        {
            get { return Equals(Wrapper.FocusedWidget); }
        }

        /// <summary>
        /// Gets the max bytes. NOTE: this can return 0.
        /// </summary>
        /// <value>
        /// The max bytes.
        /// </value>
        public uint MaxBytes
        {
            get { return Address.Deref<uint>(Offsets.EditBox.MaxBytesOffset) + 1; }
        }

        /// <summary>
        /// Gets the max letters.
        /// </summary>
        /// <value>
        /// The max letters.
        /// </value>
        public uint MaxLetters
        {
            get { return Address.Deref<uint>(Offsets.EditBox.MaxLettersOffset); }
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
                var ptr = Address.Deref(Offsets.EditBox.TextOffset);
                if (ptr.Value == IntPtr.Zero) return string.Empty;
                var maxBytes = MaxBytes;
                var maxLetters = MaxLetters;
                return Wrapper.Memory.ReadString(ptr, maxBytes > 0 ? maxBytes : maxLetters * 4, Encoding.UTF8);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} value: \"{1}\"", base.ToString(), Text);
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
            get { return (Address.Deref<int>(Offsets.EditBox.IsEnabledFlagOffset) & Offsets.EditBox.IsEnabledBit) == 0; }
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

/* Copyright 2017-2022 Cinegy GmbH. */
// Modified by Kreisverkehr in 2026: rewrote the text decoder to be more failsafe.

using System;
using System.Linq;
using System.Text;

namespace Cinegy.TsDecoder.TransportStream
{
    public class Text
    {
        private readonly byte[] _characters;

        public string Value => ToString();

        public Text(Text text)
        {
            if (text == null) return;

            _characters = new byte[text._characters.Length];
            Buffer.BlockCopy(text._characters, 0, _characters, 0, text._characters.Length);
        }

        public Text(byte[] inputChars, int start, int length)
        {
            if (null == inputChars || inputChars.Length <= start) return;

            if (inputChars.Length <= length + start)
            {
                length = inputChars.Length - start;
            }

            try
            {
                _characters = new byte[length];
                Buffer.BlockCopy(inputChars, start, _characters, 0, length);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Overflow copying text chars from input - improve bounds checking! Exception: ", ex.Message);
            }
        }

        public override string ToString()
        {
            if (!_characters?.Any() ?? true)
                return string.Empty;

            // ensure codepages are available
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                // first byte 0x15 marks utf8 text
                if (_characters[0] == 0x15)
                    return Encoding.UTF8.GetString(_characters[1..]).TrimEnd('\0', ' ');

                // first byte 0x10 marks a dynamically selected coding table identified by the following two bytes
                // luckily they correspond to the codepage numbers nicely and we only need to add an offset and
                // exclude some reserved values
                if (_characters[0] == 0x10 && _characters.Length >= 3 
                    && _characters[1] == 0x00
                    && _characters[2] <= 0x0F
                    && _characters[2] != 0x0C
                    && _characters[2] != 0x00)
                    return Encoding.GetEncoding(28590 + _characters[2]).GetString(_characters[3..]).TrimEnd('\0', ' ');

                // first byte directly specifies the coding table and with an offset we can calculate the codepage numer
                if (_characters[0] is >= 0x01 and <= 0x0b)
                {
                    var isoNumber = _characters[0] switch
                    {
                        <= 0x07 => _characters[0] + 4,
                        0x09 => 13,
                        0x0a => 14,
                        _ => 15,
                    };
                    return Encoding.GetEncoding(28590 + isoNumber).GetString(_characters[1..]).TrimEnd('\0', ' ');
                }

                // first byte is 0x1F. This specifies that the second byte is an encoding_type_id which broadcasters can 
                // register. What that means? Only the broadcaster knows. Hopefully the Latin1 encoding will get somethin 
                // usefull...
                if (_characters[0] == 0x1F)
                    return Encoding.Latin1.GetString(_characters[1..]).TrimEnd('\0', ' ');    

                // if none of these conditions match there is no marker for the coding table
                return Encoding.Latin1.GetString(_characters).TrimEnd('\0', ' ');
            }
            catch (ArgumentException)
            {
                return Encoding.Latin1.GetString(_characters).TrimEnd('\0', ' ');
            }
        }
    }
}

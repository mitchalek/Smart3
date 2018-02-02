using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Smart3.Protocols
{
    internal sealed class KeyboardSimulationSequencer
    {
        #region Static dictionary based string parse sequencing.
        private static Dictionary<char, ushort> charKeyCodes;
        private static Dictionary<string, ushort> stringKeyCodes;
        static KeyboardSimulationSequencer()
        {
            charKeyCodes = new Dictionary<char, ushort>(13);
            charKeyCodes.Add('*', 42);
            charKeyCodes.Add('.', 43);
            charKeyCodes.Add('-', 44);
            charKeyCodes.Add('0', 48);
            charKeyCodes.Add('1', 49);
            charKeyCodes.Add('2', 50);
            charKeyCodes.Add('3', 51);
            charKeyCodes.Add('4', 52);
            charKeyCodes.Add('5', 53);
            charKeyCodes.Add('6', 54);
            charKeyCodes.Add('7', 55);
            charKeyCodes.Add('8', 56);
            charKeyCodes.Add('9', 57);


            stringKeyCodes = new Dictionary<string, ushort>(10);
            stringKeyCodes.Add("KEY", 1);
            stringKeyCodes.Add("CLEAR", 3);
            stringKeyCodes.Add("RETURN", 27);
            stringKeyCodes.Add("000", 46);
            stringKeyCodes.Add("00", 47);
            stringKeyCodes.Add("PLU", 62);
            stringKeyCodes.Add("SHIFT", 95);
            stringKeyCodes.Add("SUBTOTAL", 101);
            stringKeyCodes.Add("TOTAL", 102);
            stringKeyCodes.Add("KEYBOARD", 109);

            //keyCodes.Add("CR", 13);
            //keyCodes.Add("ESC", 27);
            //keyCodes.Add("SPACE", 32);
            //keyCodes.Add(" ", 32);
            //keyCodes.Add("!", 33);
            //keyCodes.Add("#", 35);
            //keyCodes.Add("%", 37);
            //keyCodes.Add("&", 38);
            //keyCodes.Add("'", 39);
            //keyCodes.Add("(", 40);
            //keyCodes.Add(")", 41);
            //keyCodes.Add("*", 42);
            //keyCodes.Add("+", 43);
            //keyCodes.Add(",", 44);
            //keyCodes.Add("-", 45);
            //keyCodes.Add(".", 46);
            //keyCodes.Add("/", 47);
            //keyCodes.Add("<", 60);
            //keyCodes.Add(">", 62);
            //keyCodes.Add("?", 63);
            //keyCodes.Add("A", 65);
            //keyCodes.Add("B", 66);
            //keyCodes.Add("C", 67);
            //keyCodes.Add("D", 68);
            //keyCodes.Add("E", 69);
            //keyCodes.Add("F", 70);
            //keyCodes.Add("G", 71);
            //keyCodes.Add("H", 72);
            //keyCodes.Add("I", 73);
            //keyCodes.Add("J", 74);
            //keyCodes.Add("K", 75);
            //keyCodes.Add("L", 76);
            //keyCodes.Add("M", 77);
            //keyCodes.Add("N", 78);
            //keyCodes.Add("O", 79);
            //keyCodes.Add("P", 80);
            //keyCodes.Add("Q", 81);
            //keyCodes.Add("R", 82);
            //keyCodes.Add("S", 83);
            //keyCodes.Add("T", 84);
            //keyCodes.Add("U", 85);
            //keyCodes.Add("V", 86);
            //keyCodes.Add("W", 87);
            //keyCodes.Add("X", 88);
            //keyCodes.Add("Y", 89);
            //keyCodes.Add("Z", 90);
            //keyCodes.Add("a", 97);
            //keyCodes.Add("b", 98);
            //keyCodes.Add("c", 99);
            //keyCodes.Add("d", 100);
            //keyCodes.Add("e", 101);
            //keyCodes.Add("f", 102);
            //keyCodes.Add("g", 103);
            //keyCodes.Add("h", 104);
            //keyCodes.Add("i", 105);
            //keyCodes.Add("j", 106);
            //keyCodes.Add("k", 107);
            //keyCodes.Add("l", 108);
            //keyCodes.Add("m", 109);
            //keyCodes.Add("n", 110);
            //keyCodes.Add("o", 111);
            //keyCodes.Add("p", 112);
            //keyCodes.Add("q", 113);
            //keyCodes.Add("r", 114);
            //keyCodes.Add("s", 115);
            //keyCodes.Add("t", 116);
            //keyCodes.Add("u", 117);
            //keyCodes.Add("v", 118);
            //keyCodes.Add("w", 119);
            //keyCodes.Add("x", 120);
            //keyCodes.Add("y", 121);
            //keyCodes.Add("z", 122);
        }
        internal static MessageData Parse(string input, bool requestStatusReport)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length == 0) throw new ArgumentException("String must not be empty.", nameof(input));
            StringBuilder sbMessage = new StringBuilder("0;#S");
            StringBuilder sbKeyCode = new StringBuilder();
            bool isBuildingKeyCode = false;
            bool isFirstKeyCode = true;
            ushort keyCode;
            for (int i = 0; i < input.Length; i++)
            {
                // String keycode escape.
                if (input[i] == '$')
                {
                    // Parse keycode using string dictionary and exit building mode.
                    if (isBuildingKeyCode)
                    {
                        if (stringKeyCodes.TryGetValue(sbKeyCode.ToString(), out keyCode))
                        {
                            // Use delimiter when joining two keycodes.
                            if (!isFirstKeyCode)
                            {
                                sbMessage.Append(':');
                            }
                            else
                            {
                                isFirstKeyCode = false;
                            }
                            sbMessage.Append(keyCode);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid input.", nameof(input));
                        }
                        isBuildingKeyCode = false;
                    }
                    // Enter building mode.
                    else
                    {
                        sbKeyCode.Clear();
                        isBuildingKeyCode = true;
                    }
                }
                // Read char.
                else
                {
                    // Build keycode string.
                    if (isBuildingKeyCode)
                    {
                        sbKeyCode.Append(input[i]);
                    }
                    // Parse using char dictionary.
                    else
                    {
                        if (charKeyCodes.TryGetValue(input[i], out keyCode))
                        {
                            // Use delimiter when joining two keycodes.
                            if (!isFirstKeyCode)
                            {
                                sbMessage.Append(':');
                            }
                            else
                            {
                                isFirstKeyCode = false;
                            }

                            sbMessage.Append(keyCode);
                        }
                        else
                        {
                            throw new ArgumentException("Invalid input.", nameof(input));
                        }
                    }
                }
            }
            // Optional transmission of hello message when cash register completes execution.
            if (requestStatusReport)
            {
                sbMessage.Append(";#A");
            }

            return new MessageData(sbMessage.ToString());
        }
        #endregion
        #region Instance enum based sequencing.
        private ushort[] buffer = new ushort[50];
        private int position = 0;
        internal MessageData ConsumeInput(bool requestStatusReport)
        {
            if (position == 0) throw new InvalidOperationException("Input buffer is empty.");
            // Message string builder (cash register command).
            StringBuilder sbMessage = new StringBuilder("0;#S");
            // Process the input buffer.
            for (int i = 0; i < position; i++)
            {
                // Append key code.
                sbMessage.Append(buffer[i]);
                // Append key sequence delimiter if there are more key codes to be added.
                if (i < position - 1)
                {
                    sbMessage.Append(':');
                }
            }
            // Optional transmission of hello message when cash register completes execution.
            if (requestStatusReport)
            {
                sbMessage.Append(";#A");
            }

            return new MessageData(sbMessage.ToString());
        }
        internal void Input(KeyboardSimulationKeys key)
        {
            ushort keyCode = (ushort)key;
            if (position >= buffer.Length) throw new InvalidOperationException($"Exceeded limit of {buffer.Length} inputs.");
            if (!Enum.IsDefined(typeof(KeyboardSimulationKeys), keyCode)) throw new InvalidEnumArgumentException(nameof(key), keyCode, typeof(KeyboardSimulationKeys));
            buffer[position++] = keyCode;
        }
        internal void Input(KeyboardSimulationKeys[] keySequence)
        {
            if (keySequence == null) throw new ArgumentNullException(nameof(keySequence));
            for (int i = 0; i < keySequence.Length; i++)
            {
                Input(keySequence[i]);
            }
        }
        #endregion
    }
}
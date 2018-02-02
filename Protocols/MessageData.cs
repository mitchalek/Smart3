using System;
using System.Text;

namespace Smart3.Protocols
{
    /// <summary>
    /// Protocoled message data class.
    /// </summary>
    internal sealed class MessageData
    {
        private readonly char[] data;
        private readonly char[] separators = { ':', ';' };
        /// <summary>
        /// Indexed access to individual chars.
        /// </summary>
        /// <param name="index">Zero based index.</param>
        /// <returns>Char at specified index.</returns>
        internal char this[int index] { get { return data[index]; } }
        /// <summary>
        /// Number of chars in this message.
        /// </summary>
        internal int Length { get { return data.Length; } }
        /// <summary>
        /// String data organized into separate fields.
        /// </summary>
        internal FieldCollection Fields { get; private set; }
        /// <summary>
        /// Syntactic shortcut for the first data field indicating the message type.
        /// </summary>
        internal string Type { get { return Fields[0]; } }
        /// <summary>
        /// Allows implicit conversion of <see cref="MessageData"/> type to a <see cref="string"/> type.
        /// </summary>
        public static implicit operator string(MessageData md)
        {
            return new string(md.data);
        }
        #region Constructors.
        private MessageData()
        {
            Fields = new FieldCollection(this);
        }
        internal MessageData(byte[] bytes) : this()
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            data = Encoding.UTF8.GetChars(bytes);
        }
        internal MessageData(byte[] bytes, int index, int count) : this()
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            data = Encoding.UTF8.GetChars(bytes, index, count);
        }
        internal MessageData(string text) : this()
        {
            if (text == null) throw new ArgumentNullException("text");
            data = text.ToCharArray();
        }
        internal MessageData(params object[] fields) : this()
        {
            data = string.Join(separators[0].ToString(), fields).ToCharArray();
        }
        #endregion
        /// <summary>
        /// Indexed property type that implements readonly array element access.
        /// </summary>
        internal sealed class FieldCollection
        {
            private readonly MessageData md;
            internal FieldCollection(MessageData md)
            {
                this.md = md;
            }
            internal string this[int index]
            {
                get
                {
                    int start, length;
                    if (GetField(md.data, md.separators, 0, index, out start, out length))
                    {
                        return new string(md.data, start, length);
                    }
                    throw new IndexOutOfRangeException();
                }
            }
            internal int Count
            {
                get
                {
                    int count = 0, start = 0, length = 0;
                    while (GetField(md.data, md.separators, start + length, 0, out start, out length))
                    {
                        start++;
                        count++;
                    }
                    return count;
                }
            }
            /// <summary>
            /// Search for a start index and length information of the nth field.
            /// </summary>
            /// <param name="data">A <see cref="char"/> array whose data is separated by a single <see cref="char"/> separator.</param>
            /// <param name="separators">A separators that are used to split data into fields.</param>
            /// <param name="begin">Zero based index at which to begin search.</param>
            /// <param name="count">Zero based nth field for which to retrieve the information.</param>
            /// <param name="start">Returns the starting index of the nth field.</param>
            /// <param name="length">Returns the length of the nth field.</param>
            /// <returns>True if search was successful, flase otherwise.</returns>
            private bool GetField(char[] data, char[] separators, int begin, int count, out int start, out int length)
            {
                start = begin;
                length = 0;
                int end = data.Length;
                int counter = 0;
                bool isData;
                for (int i = begin; i <= end; i++)
                {
                    isData = i < end && Array.IndexOf(separators, data[i]) < 0;
                    if (!isData)
                    {
                        if (counter++ == count)
                        {
                            return true;
                        }
                        start = i + 1;
                        length = 0;
                    }
                    else
                    {
                        length++;
                    }

                }
                return false;
            }
        }
    }
}
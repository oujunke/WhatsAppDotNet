using System;
using System.Collections.Generic;
using System.Text;
using WhatsAppLib.Models;
using WhatsAppLib.Utils;
using static WhatsAppLib.Serialization.BinaryDecoder;

namespace WhatsAppLib.Serialization
{
    internal class BinaryEncoder
    {
        private List<byte> _data = new List<byte>();
        public byte[] WriteNode(Node node)
        {
            var numAttributes = 0;

            if (node.Attributes != null && node.Attributes.Count > 0)
            {
                numAttributes = node.Attributes.Count;
            }
            var hasContent = 0;
            if (node.Content != null)
            {
                hasContent = 1;
            }
            WriteListStart(2 * numAttributes + 1 + hasContent);
            WriteString(node.Description);
            WriteAttributes(node.Attributes);
            WriteChildren(node.Content);
            return _data.ToArray();
        }
        private void WriteChildren(object obj)
        {
            switch (obj)
            {
                case string str:
                    WriteString(str,true);
                    break;
                case byte[] bs:
                    WriteByteLength(bs.Length);
                    PushBytes(bs);
                    break;
                case List<Node> ns:
                    WriteListStart(ns.Count);
                    foreach (var item in ns)
                    {
                        WriteNode(item);
                    }
                    break;
                default:
                    LogUtil.Warn("cannot write child of type:"+obj);
                    break;
            }
        }
        private void WriteAttributes(Dictionary<string,string> attributes)
        {
            if (attributes == null)
            {
                return;
            }
            foreach (var item in attributes)
            {
                if (string.IsNullOrWhiteSpace(item.Value))
                {
                    continue;
                }
                WriteString(item.Key);
                WriteString(item.Value);
            }
        }
        private void WriteString(string str, bool i=false)
        {
            if (!i && str == "c.us")
            {
                WriteToken(SingleByteTokens.IndexOf("s.whatsapp.net"));
                return;
            }
            var tokenIndex = SingleByteTokens.IndexOf(str);
            if (tokenIndex == -1)
            {
                var jidSepIndex = str.IndexOf("@");
                if (jidSepIndex < 1)
                {
                    WriteStringRaw(str);
                }
                else
                {
                    WriteJid(str.Substring(0, jidSepIndex), str.Substring(jidSepIndex + 1));
                }
            }
            else
            {
                if (tokenIndex < 256)
                {
                    WriteToken(tokenIndex);
                }
                else
                {
                    var singleByteOverflow = tokenIndex - 256;
                    var dictionaryIndex = singleByteOverflow >> 8;
                    if (dictionaryIndex < 0 || dictionaryIndex > 3)
                    {
                        LogUtil.Warn("double byte dictionary token out of range:"+str);
                        return;
                    }
                    WriteToken((int)ReadStringTag.DICTIONARY_0 + dictionaryIndex);
                    WriteToken(singleByteOverflow % 256);
                }
            }
        }
        private void WriteJid(string jidLeft, string jidRight)
        {
            PushByte((byte)ReadStringTag.JID_PAIR);
            if (!string.IsNullOrWhiteSpace(jidLeft))
            {
                WritePackedBytes(jidLeft);
            }
            else
            {
                WriteToken((int)ReadStringTag.LIST_EMPTY);
            }
            WriteString(jidRight);
        }
        private void WritePackedBytes(string str)
        {
            if (!WritePackedBytesImpl(str, (int)ReadStringTag.NIBBLE_8))
            {
                if (!WritePackedBytesImpl(str, (int)ReadStringTag.HEX_8))
                {
                    LogUtil.Warn("WritePackedBytes fail");
                }
            }
        }
        private bool WritePackedBytesImpl(string str, int dataType)
        {
            var numBytes = str.Length;
            if (numBytes > 254)
            {
                LogUtil.Warn("too many bytes to pack:" + numBytes);
                return false;
            }
            PushByte(dataType);
            int x = 0;
            if (numBytes % 2 != 0)
            {
                x = 128;
            }
            PushByte(x | (int)(Math.Ceiling(numBytes / 2.0)));
            for (int i = 0; i < numBytes / 2; i++)
            {
                var b = PackBytePair(dataType, str.Substring(2 * i, 1), str.Substring(2 * i + 1, 1));
                if (b < 0)
                {
                    return false;
                }
                PushByte(b);
            }
            if (numBytes % 2 != 0)
            {
                var b = PackBytePair(dataType, str.Substring(numBytes - 1), "\x00");
                if (b < 0)
                {
                    return false;
                }
                PushByte(b);
            }
            return true;
        }
        private int PackBytePair(int packType, string part1, string part2)
        {
            if (packType == (int)ReadStringTag.NIBBLE_8)
            {
                var n1 = PackNibble(part1);
                if (n1 < 0)
                {
                    return -1;
                }
                var n2 = PackNibble(part2);
                if (n2 < 0)
                {
                    return -1;
                }
                return (n1 << 4) | n2;
            }
            else if (packType == (int)ReadStringTag.HEX_8)
            {
                var n1 = PackHex(part1);
                if (n1 < 0)
                {
                    return -1;
                }
                var n2 = PackHex(part2);
                if (n2 < 0)
                {
                    return -1;
                }
                return (n1 << 4) | n2;
            }
            else
            {
                LogUtil.Warn($"invalid pack type {packType} for byte pair:{part1} / {part2}");
                return -1;
            }
        }
        private int PackNibble(string str)
        {
            if (str.Length > 1)
            {
                LogUtil.Warn("PackNibble str length:" + str.Length);
                return -1;
            }
            else if (str[0] >= '0' && str[0] <= '9')
            {
                return Convert.ToInt32(str);
            }
            else if (str == "-")
            {
                return 10;
            }
            else if (str == ".")
            {
                return 11;
            }
            else if (str == "\x00")
            {
                return 15;
            }
            LogUtil.Warn("invalid string to pack as nibble:" + str);
            return -1;
        }
        private int PackHex(string str)
        {
            if (str.Length > 1)
            {
                LogUtil.Warn("PackHex str length:" + str.Length);
                return -1;
            }
            var value = str[0];
            if ((value >= '0' && value <= '9') || (value >= 'A' && value <= 'F') || (value >= 'a' && value <= 'f'))
            {
                var d = Convert.ToInt32(str, 16);
                return d;
            }
            else if (str == "\x00")
            {
                return 15;
            }
            LogUtil.Warn("invalid string to pack as hex: "+ str);
            return -1;
        }
        private void WriteStringRaw(string str)
        {
            WriteByteLength(str.Length);
            PushString(str);
        }
        private void WriteByteLength(int length)
        {
            if (length > int.MaxValue)
            {
                LogUtil.Error("length is too large:" + length);
            }
            else if (length >= (1 << 20))
            {
                PushByte((byte)ReadStringTag.BINARY_32);
                PushInt32(length);
            }
            else if (length >=256)
            {
                PushByte((byte)ReadStringTag.BINARY_20);
                PushInt20(length);
            }
            else
            {
                PushByte((byte)ReadStringTag.BINARY_8);
                PushInt8(length);
            }
        }
        private void WriteToken(int token)
        {
            if (token < SingleByteTokens.Count)
            {
                PushByte((byte)token);
            }
            else if (token <= 500)
            {
                LogUtil.Error("invalid token: " + token);
            }
        }
        private void WriteListStart(int listSize)
        {
            if (listSize == 0)
            {
                PushByte((byte)ReadStringTag.LIST_EMPTY);
            }
            else if (listSize < 256)
            {
                PushByte((byte)ReadStringTag.LIST_8);
                PushInt8(listSize);
            }
            else
            {
                PushByte((byte)ReadStringTag.LIST_16);
                PushInt16(listSize);
            }
        }
        private void PushString(string str)
        {
            PushBytes(Encoding.UTF8.GetBytes(str));
        }
        private void PushIntN(int value, int n, bool littleEndian = false)
        {
            for (int i = 0; i < n; i++)
            {
                int curShift;
                if (littleEndian)
                {
                    curShift = i;
                }
                else
                {
                    curShift = n - i - 1;
                }
                PushByte((byte)((value >> (curShift * 8)) & 0xFF));
            }
        }
        private void PushInt8(int value)
        {
            PushIntN(value, 1);
        }
        private void PushInt16(int value)
        {
            PushIntN(value, 2);
        }
        private void PushInt20(int value)
        {
            PushIntN(value, 3);
        }
        private void PushInt32(int value)
        {
            PushIntN(value, 4);
        }
        private void PushByte(byte b)
        {
            _data.Add(b);
        }
        private void PushByte(int b)
        {
            _data.Add((byte)b);
        }
        private void PushBytes(IEnumerable<byte> bs)
        {
            _data.AddRange(bs);
        }
    }
}

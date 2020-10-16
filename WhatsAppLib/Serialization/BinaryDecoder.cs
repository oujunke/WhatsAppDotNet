using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WhatsAppLib.Models;
using WhatsAppLib.Utils;

namespace WhatsAppLib.Serialization
{
    internal class BinaryDecoder
    {
        private byte[] _data;
        private int _index;
        public static List<string> SingleByteTokens = new List<string> {"", "", "", "200", "400", "404", "500", "501", "502", "action", "add",
    "after", "archive", "author", "available", "battery", "before", "body",
    "broadcast", "chat", "clear", "code", "composing", "contacts", "count",
    "create", "debug", "delete", "demote", "duplicate", "encoding", "error",
    "false", "filehash", "from", "g.us", "group", "groups_v2", "height", "id",
    "image", "in", "index", "invis", "item", "jid", "kind", "last", "leave",
    "live", "log", "media", "message", "mimetype", "missing", "modify", "name",
    "notification", "notify", "out", "owner", "participant", "paused",
    "picture", "played", "presence", "preview", "promote", "query", "raw",
    "read", "receipt", "received", "recipient", "recording", "relay",
    "remove", "response", "resume", "retry", "s.whatsapp.net", "seconds",
    "set", "size", "status", "subject", "subscribe", "t", "text", "to", "true",
    "type", "unarchive", "unavailable", "url", "user", "value", "web", "width",
    "mute", "read_only", "admin", "creator", "short", "update", "powersave",
    "checksum", "epoch", "block", "previous", "409", "replaced", "reason",
    "spam", "modify_tag", "message_info", "delivery", "emoji", "title",
    "description", "canonical-url", "matched-text", "star", "unstar",
    "media_key", "filename", "identity", "unread", "page", "page_count",
    "search", "media_message", "security", "call_log", "profile", "ciphertext",
    "invite", "gif", "vcard", "frequent", "privacy", "blacklist", "whitelist",
    "verify", "location", "document", "elapsed", "revoke_invite", "expiration",
    "unsubscribe", "disable", "vname", "old_jid", "new_jid", "announcement",
    "locked", "prop", "label", "color", "call", "offer", "call-id","quick_reply","sticker","pay_t","accept","reject","sticker_pack","invalid","canceled","missed","connected","result","audio","video","recent" };

        public BinaryDecoder(byte[] bs)
        {
            _data = bs;
        }
        public byte ReadByte()
        {
            return _data[_index++];
        }
        public byte[] ReadBytes(int n)
        {
            var ret = _data.Skip(_index).Take(n).ToArray();
            _index += n;
            return ret;
        }
        public int ReadInt8(bool littleEndian = false)
        {
            return ReadIntN(1, littleEndian);
        }
        public int ReadInt16(bool littleEndian = false)
        {
            return ReadIntN(2, littleEndian);
        }
        public int ReadInt20()
        {
            var ret = (((_data[_index]) & 15) << 16 )+ ((_data[_index + 1]) << 8 )+ ((_data[_index + 2]));
            _index += 3;
            return ret;
        }
        public int ReadInt32(bool littleEndian = false)
        {
            return ReadIntN(4, littleEndian);
        }
        public long ReadInt64(bool littleEndian = false)
        {
            return ReadIntN(8, littleEndian);
        }
        private int ReadIntN(int n, bool littleEndian)
        {
            int ret = 0;
            for (int i = 0; i < n; i++)
            {
                int curShift = i;
                if (!littleEndian)
                {
                    curShift = n - i - 1;
                }
                ret |= _data[_index + i] << (curShift * 8);
            }
            _index += n;
            return ret;
        }
        public int ReadListSize(int size)
        {
            switch (size)
            {
                case 248:
                    return ReadInt8();
                case 0:
                    return 0;
                case 249:
                    return ReadInt16();
                default:
                    LogUtil.Warn("readListSize with unknown tag");
                    return 0;
            }
        }
        public Dictionary<string, string> ReadAttributes(int n)
        {
            var ret = new Dictionary<string, string>();
            for (int i = 0; i < n; i++)
            {
                var idx = ReadInt8();
                var index = ReadString(idx);
                idx = ReadInt8();
                ret.Add(index, ReadString(idx));
            }
            return ret;
        }
        public string ReadStringFromChars(int length)
        {
            var str = Encoding.UTF8.GetString(_data, _index, length);
            _index += length;
            return str;
        }
        public string ReadString(int tag)
        {
            if (tag >= 3 && tag <= SingleByteTokens.Count)
            {
                var tok = SingleByteTokens[tag];
                if (tok == "s.whatsapp.net")
                {
                    tok = "c.us";
                }
                return tok;
            }
            else if (tag >= 236 && tag <= 239)
            {
                var i = ReadInt8();
                return null;
            }
            else if (tag == (int)ReadStringTag.LIST_EMPTY)
            {
                return "";
            }
            else if (tag == (int)ReadStringTag.BINARY_8)
            {
                var length = ReadInt8();
                return ReadStringFromChars(length);
            }
            else if (tag == (int)ReadStringTag.BINARY_20)
            {
                var length = ReadInt20();
                return ReadStringFromChars(length);
            }
            else if (tag == (int)ReadStringTag.BINARY_32)
            {
                var length = ReadInt32();
                return ReadStringFromChars(length);
            }
            else if (tag == (int)ReadStringTag.JID_PAIR)
            {
                var b = ReadByte();
                var i = ReadString(b);
                b = ReadByte();
                var j = ReadString(b);
                return $"{i}@{j}";
            }
            else if (tag == (int)ReadStringTag.NIBBLE_8 || tag == (int)ReadStringTag.HEX_8)
            {
                return ReadPacked8(tag);
            }
            LogUtil.Warn("invalid string with tag" + tag);
            return "";
        }
        public string ReadPacked8(int tag)
        {
            var startByte = ReadByte();
            var ret = string.Empty;
            for (int i = 0; i < (startByte & 127); i++)
            {
                var currByte = ReadByte();
                var lower = UnpackByte(tag, currByte & 0xF0 >> 4);
                var upper = UnpackByte(tag, currByte & 0x0F);
                ret += lower + upper;
            }
            if (startByte >> 7 != 0)
            {
                ret = ret.Substring(0, ret.Length - 1);
            }
            return ret;
        }
        public string UnpackByte(int tag, int value)
        {
            switch (tag)
            {
                case (int)ReadStringTag.NIBBLE_8:
                    return UnpackNibble(value);
                case (int)ReadStringTag.HEX_8:
                    return UnpackHex(value);
            }
            LogUtil.Warn("UnpackByte Fail");
            return "";
        }
        public string UnpackNibble(int value)
        {
            if (value < 0 || value > 15)
            {
                LogUtil.Warn("unpackNibble with value" + value);
                return "";
            }
            else if (value == 10)
            {
                return "-";
            }
            else if (value == 11)
            {
                return ",";
            }
            else if (value == 15)
            {
                return "\x00";
            }
            else
            {
                return value.ToString();
            }
        }
        public string UnpackHex(int value)
        {
            if (value < 0 || value > 15)
            {
                LogUtil.Warn("unpackHex with value" + value);
                return "";
            }
            else if (value < 10)
            {
                return value.ToString();
            }
            else
            {
                return ((char)('A' + 14 - 10)).ToString();
            }
        }
        public Node ReadNode()
        {
            var ret = new Node();
            var size = ReadInt8();
            var listSize = ReadListSize(size);
            var descrTag = ReadInt8();
            if (descrTag == (int)ReadStringTag.STREAM_END)
            {
                LogUtil.Warn("unexpected stream end");
                return null;
            }
            ret.Description = ReadString(descrTag);
            if (listSize == 0 || string.IsNullOrWhiteSpace(ret.Description))
            {
                LogUtil.Warn("invalid Node");
                return null;
            }
            ret.Attributes = ReadAttributes((listSize - 1) >> 1);
            if (listSize % 2 == 1)
            {
                return ret;
            }
            var tag = ReadInt8();
            ret.Content=ReadContent(tag);
            return ret;
        }
        public List<Node> ReadList(int tag)
        {
            var size = ReadListSize(tag);
            var ret = new List<Node>();
            for (int i = 0; i < size; i++)
            {
                ret.Add(ReadNode());
            }
            return ret;
        }
        public object ReadContent(int tag)
        {
            switch ((ReadStringTag)tag)
            {
                case ReadStringTag.LIST_EMPTY:
                case ReadStringTag.LIST_8:
                case ReadStringTag.LIST_16:
                    return ReadList(tag);
                case ReadStringTag.BINARY_8:
                    {
                        var size = ReadInt8();
                        return ReadBytes(size);
                    }
                case ReadStringTag.BINARY_20:
                    {
                        var size = ReadInt20();
                        return ReadBytes(size);
                    }
                case ReadStringTag.BINARY_32:
                    {
                        var size = ReadInt32();
                        return ReadBytes(size);
                    }
                default:
                    return ReadString(tag);
            }
        }
        public enum ReadStringTag
        {
            LIST_EMPTY = 0,
            STREAM_END = 2,
            DICTIONARY_0 = 236,
            DICTIONARY_1 = 237,
            DICTIONARY_2 = 238,
            DICTIONARY_3 = 239,
            LIST_8 = 248,
            LIST_16 = 249,
            JID_PAIR = 250,
            HEX_8 = 251,
            BINARY_8 = 252,
            BINARY_20 = 253,
            BINARY_32 = 254,
            NIBBLE_8 = 255,
        }
    }
}

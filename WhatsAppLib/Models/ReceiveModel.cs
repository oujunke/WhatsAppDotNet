using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppLib.Models
{
    public class ReceiveModel
    {
        public byte[] ByteData { private set; get; }
        public string StringData { private set; get; }
        public string Tag { private set; get; }
        public string Body { private set; get; }
        public WebSocketMessageType MessageType { set; get; }
        internal Node Nodes { set; get; }
        internal ArraySegment<byte> ReceiveData { set; get; }
        private List<byte> _bs = new List<byte>();
        private ReceiveModel(int length)
        {
            ReceiveData = new ArraySegment<byte>(new byte[length]);
        }
        private ReceiveModel(byte[] bs)
        {
            ByteData = bs;
            if (bs != null)
            {
                StringData = Encoding.UTF8.GetString(ByteData);
            }
        }
        internal static ReceiveModel GetReceiveModel(int length = 1024)
        {
            return new ReceiveModel(length);
        }
        internal static ReceiveModel GetReceiveModel(byte[] bs)
        {
            return new ReceiveModel(bs);
        }
        internal void Continue(int count)
        {
            _bs.AddRange(ReceiveData.Take(count));
        }
        internal void End(int count, WebSocketMessageType messageType)
        {
            _bs.AddRange(ReceiveData.Take(count));
            ByteData = _bs.ToArray();
            StringData = Encoding.UTF8.GetString(ByteData);
            var index = StringData.IndexOf(",");
            if (index >= 0 && index < StringData.Length)
            {
                Tag = StringData.Substring(0, index);
                Body = StringData.Substring(index + 1);
            }
            MessageType = messageType;
        }
    }
}

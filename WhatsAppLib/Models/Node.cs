using Proto;
using System.Collections.Generic;
using System.Linq;
using WhatsAppLib.Serialization;

namespace WhatsAppLib.Models
{
    internal class Node
    {
        public string Description { set; get; }
        public Dictionary<string, string> Attributes { set; get; }
        public object Content { set; get; }
        public byte[] Marshal()
        {
            if (Attributes != null && Attributes.Count > 0 && Content != null&& Content is List<WebMessageInfo> wms)
            {
                var nl = new List<Node>();
                foreach (var wm in wms)
                {
                    var bs = new byte[4096];
                    var me = new Google.Protobuf.CodedOutputStream(bs);
                    wm.WriteTo(me);
                    nl.Add( new Node
                    {
                        Content = bs.Take((int)me.Position).ToArray(),
                        Description = "message"
                    });
                }
                Content = nl;
            }
            BinaryEncoder binaryEncoder = new BinaryEncoder();
            return binaryEncoder.WriteNode(this);
        }
    }
}

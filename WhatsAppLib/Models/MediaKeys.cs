using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppLib.Models
{
    internal class MediaKeys
    {
        public byte[] Iv { set; get; }
        public byte[] CipherKey { set; get; }
        public byte[] MacKey { set; get; }
        public MediaKeys(byte[] data)
        {
            Iv = data.Take(16).ToArray();
            CipherKey = data.Skip(16).Take(32).ToArray();
            MacKey = data.Skip(48).Take(32).ToArray();
        }
    }
}

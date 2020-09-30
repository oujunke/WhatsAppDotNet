using System;
using System.Collections.Generic;
using System.Text;

namespace WhatsAppLib.Messages
{
    public class ImageMessage:MessageBase
    {
        public byte[] ImageData { set; get; }
    }
}

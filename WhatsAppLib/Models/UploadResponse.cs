using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppLib.Models
{
    internal class UploadResponse
    {
        public string DownloadUrl { set; get; }
        public byte[] MediaKey { set; get; }
        public byte[] FileSha256 { set; get; }
        public byte[] FileEncSha256 { set; get; }
        public ulong FileLength { set; get; }
    }
}

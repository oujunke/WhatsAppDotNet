using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppLib.Models
{
    public class SessionInfo
    {
        /// <summary>
        /// 客户端Id
        /// </summary>
        public string ClientId { set; get; }
        /// <summary>
        /// 客户端Token
        /// </summary>
        public string ClientToken { set; get; }
        /// <summary>
        /// 服务端Token
        /// </summary>
        public string ServerToken { set; get; }
        /// <summary>
        /// 通信加密密钥  Communication encryption key
        /// </summary>
        public byte[] EncKey { set; get; }
        /// <summary>
        /// 签名加密密钥  Signature encryption key
        /// </summary>
        public byte[] MacKey { set; get; }
        /// <summary>
        /// 当前登录Id Current login ID
        /// </summary>
        public string Wid { set; get; }
    }
}

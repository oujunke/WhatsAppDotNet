using System;
using System.Collections.Generic;
using System.Text;

namespace WhatsAppLib.Messages
{
    public abstract class MessageBase
    {
        /// <summary>
        /// 消息生成时间
        /// </summary>
        public ulong MessageTimestamp { set; get; }
        /// <summary>
        /// 对方Id
        /// </summary>
        public string RemoteJid { set; get; }
        /// <summary>
        /// 消息文字
        /// </summary>
        public string Text { set; get; }
    }
}

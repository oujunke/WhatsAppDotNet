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
        /// <summary>
        /// 消息ID
        /// </summary>
        public string MsgId { set; get; }
        /// <summary>
        /// 是否是本人发送消息
        /// </summary>
        public bool FromMe { set; get; }
        public int Status { set; get; }
    }
}

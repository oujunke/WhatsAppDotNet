using AronParker.Hkdf;
using Elliptic;
using Google.Protobuf;
using Newtonsoft.Json;
using Proto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WhatsAppLib.Messages;
using WhatsAppLib.Models;
using WhatsAppLib.Serialization;
using WhatsAppLib.Utils;
using Yove.Proxy;
using ImageMessage = Proto.ImageMessage;

namespace WhatsAppLib
{
    public class WhatsApp : IDisposable
    {
        #region 公有事件    Event
        /// <summary>
        /// 需要扫码登录  Scan code to log in
        /// </summary>
        public event Action<string> LoginScanCodeEvent;
        /// <summary>
        /// 登录成功后返回Session信息    Return Session information after successful login
        /// </summary>
        public event Action<SessionInfo> LoginSuccessEvent;
        /// <summary>
        /// 接收其他未处理的消息  Receive Remaining messages
        /// </summary>
        public event Action<ReceiveModel> ReceiveRemainingMessagesEvent;
        /// <summary>
        /// 接受到的文字消息    Receive Text messages
        /// </summary>
        public event Action<TextMessage> ReceiveTextMessageEvent;
        /// <summary>
        /// 接受到的图片消息    Receive Image messages
        /// </summary>
        public event Action<Messages.ImageMessage> ReceiveImageMessageEvent;
        /// <summary>
        /// 掉线事件
        /// </summary>
        public event Action AccountDroppedEvent;
        #endregion
        #region 公有属性    public property
        /// <summary>
        /// Http代理  Http Proxy
        /// </summary>
        public ProxyClient WebProxy
        {
            set
            {
                _webSocket.Options.Proxy = value;
                _webProxy = value;
            }
            get => _webProxy;
        }
        /// <summary>
        /// 登录Session   Login Session
        /// </summary>
        public SessionInfo Session { set; get; }
        #endregion
        #region 私有成员    private member
        private ClientWebSocket _webSocket;
        private ProxyClient _webProxy;
        //private object _sendObj = new object();
        private int _msgCount;
        private bool _loginSuccess;
        private object _snapReceiveLock = new object();
        private Dictionary<string, Func<ReceiveModel, bool>> _snapReceiveDictionary = new Dictionary<string, Func<ReceiveModel, bool>>();
        private Dictionary<string, int> _snapReceiveRemoveCountDictionary = new Dictionary<string, int>();
        private const string MediaImage = "WhatsApp Image Keys";
        private const string MediaVideo = "WhatsApp Video Keys";
        private const string MediaAudio = "WhatsApp Audio Keys";
        private const string MediaDocument = "WhatsApp Document Keys";
        private static Dictionary<string, string> MediaTypeMap = new Dictionary<string, string>{
            { MediaImage,"/mms/image" },
            { MediaVideo,"/mms/video" },
            { MediaAudio,"/mms/document" },
            { MediaDocument,"/mms/audio" },
        };
        #endregion
        #region 构造函数    Constructor
        static WhatsApp()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
        }
        public WhatsApp()
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Origin", "https://web.whatsapp.com");
        }
        public WhatsApp(ProxyClient webProxy) : this()
        {
            _webProxy = webProxy;
            _webSocket.Options.Proxy = webProxy;
        }
        public WhatsApp(SessionInfo session) : this()
        {
            Session = session;
        }
        public WhatsApp(SessionInfo session, ProxyClient webProxy) : this(webProxy)
        {
            Session = session;
        }
        #endregion
        #region 公有方法    public method
        public async Task<bool> Connect()
        {
            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.Empty, "Close", CancellationToken.None);
            }
            await _webSocket.ConnectAsync(new Uri("wss://web.whatsapp.com/ws"), CancellationToken.None);
            Receive(ReceiveModel.GetReceiveModel());
            Send("?,,");
            return true;
        }
        /// <summary>
        /// 登录,需要监听LoginScanCodeEvent,和LoginSuccessEvent事件  To log in, you need to monitor the LoginScanCodeEvent and LoginSuccessEvent events
        /// </summary>
        public async void Login()
        {
            _snapReceiveDictionary.Clear();
            if(! await Connect())
            {
                throw new Exception("Connect Error");
            }
            if (Session == null)
            {
                Session = new SessionInfo();
            }
            if (string.IsNullOrEmpty(Session.ClientToken))
            {
                WhatsAppLogin();
            }
            else
            {
                ReLogin();
            }
        }
        /// <summary>
        /// 发送图片消息
        /// </summary>
        /// <param name="remoteJid">发送给谁</param>
        /// <param name="data">图片字节码</param>
        /// <param name="caption">消息名称</param>
        /// <returns></returns>
        public async Task<string> SendImage(string remoteJid, byte[] data, string caption = null, Action<ReceiveModel> act = null)
        {
            var uploadResponse = await Upload(data, MediaImage);
            if (uploadResponse == null)
            {
                return null;
            }
            return SendProto(new WebMessageInfo()
            {
                Key = new MessageKey
                {
                    RemoteJid = remoteJid
                },
                Message = new Message
                {
                    ImageMessage = new ImageMessage
                    {
                        Url = uploadResponse.DownloadUrl,
                        Caption = caption,
                        Mimetype = "image/jpeg",
                        MediaKey = ByteString.CopyFrom(uploadResponse.MediaKey),
                        FileEncSha256 = ByteString.CopyFrom(uploadResponse.FileEncSha256),
                        FileSha256 = ByteString.CopyFrom(uploadResponse.FileSha256),
                        FileLength = uploadResponse.FileLength
                    }
                }
            }, act);
        }
        public string SendText(string remoteJid, string text, Action<ReceiveModel> act = null)
        {
            return SendProto(new WebMessageInfo()
            {
                Key = new MessageKey
                {
                    RemoteJid = remoteJid
                },
                Message = new Message
                {
                    Conversation = text
                }
            }, act);
        }
        public string LoadMediaInfo(string jid, string messageId, string owner, Action<ReceiveModel> act = null)
        {
            return SendQuery("media", jid, messageId, "", owner, "", 0, 0, act);
        }
        public string CreateGroup(string subject, string[] participants, Action<ReceiveModel> act = null)
        {
            return SendGroup("create", "", subject, participants, act);
        }
        public string GetFullChatHistory(string jid, int count = 300)
        {
            if (string.IsNullOrWhiteSpace(jid))
            {
                return string.Empty;
            }
            var beforeMsg = "";
            var beforeMsgIsOwner = true;
            //while (true)
            {
                SendQuery("message", jid, beforeMsg, "before", beforeMsgIsOwner ? "true" : "false", "", count, 0, async rm =>
                           {
                               var node = await GetDecryptNode(rm);
                               if (node != null)
                               {
                                   if (node.Content is List<Node> nodeList)
                                   {
                                       foreach (var item in nodeList)
                                       {
                                           if (item.Description == "message")
                                           {
                                               var messageData = item.Content as byte[];
                                               var ms = WebMessageInfo.Parser.ParseFrom(messageData);
                                               if (ms.Message != null)
                                               {
                                               }
                                           }
                                       }
                                   }
                               }
                           }, 2);
            }
            return string.Empty;
        }
        public void Dispose()
        {
            _webSocket.CloseAsync(WebSocketCloseStatus.Empty, null, CancellationToken.None);
        }
        #endregion
        #region 私有方法    private method
        private async Task<byte[]> DownloadImage(string url, byte[] mediaKey)
        {
            return await Download(url, mediaKey, MediaImage);
        }
        private async Task<byte[]> Download(string url, byte[] mediaKey, string info)
        {
            var memory = await url.GetStream(WebProxy);
            var mk = GetMediaKeys(mediaKey, info);
            var data = memory.ToArray();
            var file = data.Take(data.Length - 10).ToArray();
            var mac = data.Skip(file.Length).ToArray();
            var sign = (mk.Iv.Concat(file).ToArray()).HMACSHA256_Encrypt(mk.MacKey);
            if (!sign.Take(10).ToArray().ValueEquals(mac))
            {
                LogUtil.Error("invalid media hmac");
                return null;
            }
            var fileData = file.AesCbcDecrypt(mk.CipherKey, mk.Iv);
            return fileData;

        }
        private MediaKeys GetMediaKeys(byte[] mediaKey, string info)
        {
            var sharedSecretExtract = new Hkdf(HashAlgorithmName.SHA256).Extract(mediaKey);
            var sharedSecretExpand = new Hkdf(HashAlgorithmName.SHA256).Expand(sharedSecretExtract, 112, Encoding.UTF8.GetBytes(info));
            return new MediaKeys(sharedSecretExpand);
        }
        private async Task<UploadResponse> Upload(byte[] data, string info)
        {
            return await await Task.Factory.StartNew(async () =>
            {
                var uploadResponse = new UploadResponse();
                uploadResponse.FileLength = (ulong)data.Length;
                uploadResponse.MediaKey = GetRandom(32);
                var mk = GetMediaKeys(uploadResponse.MediaKey, MediaImage);
                var enc = data.AesCbcEncrypt(mk.CipherKey, mk.Iv);
                var mac = (mk.Iv.Concat(enc).ToArray()).HMACSHA256_Encrypt(mk.MacKey).Take(10);
                uploadResponse.FileSha256 = data.SHA256_Encrypt();
                var joinData = enc.Concat(mac).ToArray();
                uploadResponse.FileEncSha256 = joinData.SHA256_Encrypt();
                var mediaConnResponse = await QueryMediaConn();
                if (mediaConnResponse == null)
                {
                    return null;
                }
                var token = Convert.ToBase64String(uploadResponse.FileEncSha256).Replace("+", "-").Replace("/", "_");
                var url = $"https://{mediaConnResponse.MediaConn.Hosts[0].Hostname}{MediaTypeMap[info]}/{token}?auth={mediaConnResponse.MediaConn.Auth}&token={token}";
                var response = await url.PostHtml(joinData, WebProxy, new Dictionary<string, string> {
                    { "Origin","https://web.whatsapp.com" },
                    { "Referer","https://web.whatsapp.com/"}
                });
                uploadResponse.DownloadUrl = response.RegexGetString("url\":\"([^\"]*)\"");
                return uploadResponse;
            }).ConfigureAwait(false);

        }
        private async Task<MediaConnResponse> QueryMediaConn()
        {
            MediaConnResponse connResponse = null;
            SendJson("[\"query\",\"mediaConn\"]", rm => connResponse = JsonConvert.DeserializeObject<MediaConnResponse>(rm.Body));
            await await Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    if (connResponse != null)
                    {
                        return;
                    }
                    await Task.Delay(100);
                }
            }).ConfigureAwait(false);
            return connResponse;
        }
        private void AddCallback(string tag, Action<ReceiveModel> act, int count = 0)
        {
            if (act != null)
            {
                AddSnapReceive(tag, rm =>
                {
                    act(rm);
                    return true;
                }, count);
            }
        }
        private string SendProto(WebMessageInfo webMessage, Action<ReceiveModel> act = null)
        {
            if (webMessage.Key.Id.IsNullOrWhiteSpace())
            {
                webMessage.Key.Id = GetRandom(10).ToHexString().ToUpper();
            }
            if (webMessage.MessageTimestamp == 0)
            {
                webMessage.MessageTimestamp = (ulong)DateTime.Now.GetTimeStampInt();
            }
            webMessage.Key.FromMe = true;
            webMessage.Status = WebMessageInfo.Types.WEB_MESSAGE_INFO_STATUS.Error;
            var n = new Node
            {
                Description = "action",
                Attributes = new Dictionary<string, string> {
                    { "type", "relay" },
                    {"epoch",( Interlocked.Increment(ref _msgCount) - 1).ToString() }//"5" }//
                },
                Content = new List<WebMessageInfo> { webMessage }
            };
            AddCallback(webMessage.Key.Id, act);
            SendBinary(n, WriteBinaryType.Message, webMessage.Key.Id);
            return webMessage.Key.Id;
        }
        private void SendBinary(Node node, WriteBinaryType binaryType, string messageTag)
        {
            var data = EncryptBinaryMessage(node);
            var bs = new List<byte>(Encoding.UTF8.GetBytes($"{messageTag},"));
            bs.Add((byte)binaryType);
            bs.Add(128);
            bs.AddRange(data);
            _webSocket.SendAsync(new ArraySegment<byte>(bs.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
        }
        private string SendQuery(string t, string jid, string messageId, string kind, string owner, string search, int count, int page, Action<ReceiveModel> act = null, int removeCount = 0)
        {
            var msgCount = Interlocked.Increment(ref _msgCount) - 1;
            var tag = $"{DateTime.Now.GetTimeStampInt()}.--{msgCount}";
            AddCallback(tag, act, removeCount);
            var n = new Node
            {
                Description = "query",
                Attributes = new Dictionary<string, string> {
                    { "type", t },
                    {"epoch",msgCount.ToString() }//"5" }//
                },
            };
            if (!jid.IsNullOrWhiteSpace())
            {
                n.Attributes.Add("jid", jid);
            }
            if (!messageId.IsNullOrWhiteSpace())
            {
                n.Attributes.Add("index", messageId);
            }
            if (!kind.IsNullOrWhiteSpace())
            {
                n.Attributes.Add("kind", kind);
            }
            if (!owner.IsNullOrWhiteSpace())
            {
                n.Attributes.Add("owner", owner);
            }
            if (!search.IsNullOrWhiteSpace())
            {
                n.Attributes.Add("search", search);
            }
            if (count > 0)
            {
                n.Attributes.Add("count", count.ToString());
            }
            if (page > 0)
            {
                n.Attributes.Add("page", page.ToString());
            }
            var msgType = WriteBinaryType.Group;
            if (t == "media")
            {
                msgType = WriteBinaryType.QueryMedia;
            }
            SendBinary(n, msgType, tag);
            return tag;
        }
        private string SendGroup(string t, string jid, string subject, string[] participants, Action<ReceiveModel> act = null)
        {
            var msgCount = Interlocked.Increment(ref _msgCount) - 1;
            var tag = $"{DateTime.Now.GetTimeStampInt()}.--{msgCount}";
            AddCallback(tag, act);
            var g = new Node
            {
                Description = "group",
                Attributes = new Dictionary<string, string> {
                    { "author", Session.Wid },
                    { "id", tag },
                    { "type", t }
                }
            };
            if (participants != null && participants.Length > 0)
            {
                var ns = new List<Node>();
                foreach (var participant in participants)
                {
                    ns.Add(new Node
                    {
                        Description = "participant",
                        Attributes = new Dictionary<string, string> { { "jid", participant } }
                    });
                }
                g.Content = ns;
            }
            if (!jid.IsNullOrWhiteSpace())
            {
                g.Attributes.Add("jid", jid);
            }

            if (!subject.IsNullOrWhiteSpace())
            {
                g.Attributes.Add("subject", subject);
            }
            SendBinary(new Node
            {
                Description = "action",
                Attributes = new Dictionary<string, string> {
                    { "type", "set" },
                    {"epoch",msgCount.ToString() }
                },
                Content = new List<Node> { g }
            }, WriteBinaryType.Group, tag);
            return tag;
        }
        private string SendJson(string str, Action<ReceiveModel> act = null)
        {
            var tag = $"{DateTime.Now.GetTimeStampInt()}.--{Interlocked.Increment(ref _msgCount) - 1}";
            AddCallback(tag, act);
            Send($"{tag},{str}");
            return tag;
        }
        private void Send(string str)
        {
            Send(Encoding.UTF8.GetBytes(str));
        }
        private void Send(byte[] bs)
        {
            //lock (_sendObj)
            //{
            _webSocket.SendAsync(new ArraySegment<byte>(bs, 0, bs.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            //}
        }
        private void Receive(ReceiveModel receiveModel)
        {
            Task.Factory.StartNew(async () =>
            {
                var receiveResult = await _webSocket.ReceiveAsync(receiveModel.ReceiveData, CancellationToken.None);
                try
                {
                    if (receiveResult.EndOfMessage)
                    {
                        Receive(ReceiveModel.GetReceiveModel());
                        receiveModel.End(receiveResult.Count, receiveResult.MessageType);
                        await ReceiveHandle(receiveModel);
                    }
                    else
                    {
                        receiveModel.Continue(receiveResult.Count);
                        Receive(receiveModel);
                    }
                }
                catch
                {
                    LogUtil.Warn("连接断开");
                    _webSocket.Dispose();
                    _ = Task.Factory.StartNew(() => AccountDroppedEvent?.Invoke());
                }
            });

        }
        private byte[] EncryptBinaryMessage(Node node)
        {
            var b = node.Marshal();
            var iv = Convert.FromBase64String("aKs1sBxLFMBHVkUQwS/YEg=="); //GetRandom(16);
            var cipher = b.AesCbcEncrypt(Session.EncKey, iv);
            var cipherIv = iv.Concat(cipher).ToArray();
            var hash = cipherIv.HMACSHA256_Encrypt(Session.MacKey);
            var data = new byte[cipherIv.Length + 32];
            Array.Copy(hash, data, 32);
            Array.Copy(cipherIv, 0, data, 32, cipherIv.Length);
            return data;
        }
        private bool LoginResponseHandle(ReceiveModel receive)
        {
            var challenge = receive.Body.RegexGetString("\"challenge\":\"([^\"]*)\"");
            if (challenge.IsNullOrWhiteSpace())
            {
                var jsData = JsonConvert.DeserializeObject<dynamic>(receive.Body);
                Session.ClientToken = jsData[1]["clientToken"];
                Session.ServerToken = jsData[1]["serverToken"];
                Session.Wid = jsData[1]["wid"];
                _ = Task.Factory.StartNew(() => LoginSuccessEvent?.Invoke(Session));
                _loginSuccess = true;
            }
            else
            {
                AddSnapReceive("s2", LoginResponseHandle);
                ResolveChallenge(challenge);
            }
            return true;
        }
        private void ReLogin()
        {
            AddSnapReceive("s1", LoginResponseHandle);
            SendJson($"[\"admin\",\"init\",[2,2033,7],[\"Windows\",\"Chrome\",\"10\"],\"{Session.ClientId}\",true]");
            Task.Delay(5000).ContinueWith(t =>
            {
                SendJson($"[\"admin\",\"login\",\"{Session.ClientToken}\",\"{Session.ServerToken}\",\"{Session.ClientId}\",\"takeover\"]");
            });

        }
        private void ResolveChallenge(string challenge)
        {
            var decoded = Convert.FromBase64String(challenge);
            var loginChallenge = decoded.HMACSHA256_Encrypt(Session.MacKey);
            SendJson($"[\"admin\",\"challenge\",\"{Convert.ToBase64String(loginChallenge)}\",\"{Session.ServerToken}\",\"{Session.ClientId}\"]");
        }
        private void WhatsAppLogin()
        {
            Task.Factory.StartNew(async () =>
            {
                var clientId = GetRandom(16);
                Session.ClientId = Convert.ToBase64String(clientId);
                var tag = SendJson($"[\"admin\",\"init\",[2,2033,7],[\"Windows\",\"Chrome\",\"10\"],\"{Session.ClientId}\",true]");
                string refUrl = null;
                AddSnapReceive(tag, rm =>
                {
                    if (rm.Body.Contains("\"ref\":\""))
                    {
                        refUrl = rm.Body.RegexGetString("\"ref\":\"([^\"]*)\"");
                        return true;
                    }
                    return false;
                });
                var privateKey = Curve25519.CreateRandomPrivateKey();
                var publicKey = Curve25519.GetPublicKey(privateKey);
                AddSnapReceive("s1", rm =>
                {
                    var jsData = JsonConvert.DeserializeObject<dynamic>(rm.Body);
                    Session.ClientToken = jsData[1]["clientToken"];
                    Session.ServerToken = jsData[1]["serverToken"];
                    Session.Wid = jsData[1]["wid"];
                    string secret = jsData[1]["secret"];
                    var decodedSecret = Convert.FromBase64String(secret);
                    var pubKey = decodedSecret.Take(32).ToArray();
                    var sharedSecret = Curve25519.GetSharedSecret(privateKey, pubKey);
                    var data = sharedSecret.HMACSHA256_Encrypt(new byte[32]);
                    var sharedSecretExtended = new Hkdf(HashAlgorithmName.SHA256).Expand(data, 80);
                    var checkSecret = new byte[112];
                    Array.Copy(decodedSecret, checkSecret, 32);
                    Array.Copy(decodedSecret, 64, checkSecret, 32, 80);
                    var sign = checkSecret.HMACSHA256_Encrypt(sharedSecretExtended.Skip(32).Take(32).ToArray());
                    if (!sign.ValueEquals(decodedSecret.Skip(32).Take(32).ToArray()))
                    {
                        LogUtil.Error("签名校验错误");
                        return true;
                    }
                    var keysEncrypted = new byte[96];
                    Array.Copy(sharedSecretExtended, 64, keysEncrypted, 0, 16);
                    Array.Copy(decodedSecret, 64, keysEncrypted, 16, 80);
                    var keyDecrypted = decodedSecret.Skip(64).ToArray().AesCbcDecrypt(sharedSecretExtended.Take(32).ToArray(), sharedSecretExtended.Skip(64).ToArray());
                    Session.EncKey = keyDecrypted.Take(32).ToArray();
                    Session.MacKey = keyDecrypted.Skip(32).ToArray();
                    _ = Task.Factory.StartNew(() => LoginSuccessEvent?.Invoke(Session));
                    _loginSuccess = true;
                    return true;
                });
                while (refUrl.IsNullOrWhiteSpace())
                {
                    await Task.Delay(100);
                }
                var loginUrl = $"{refUrl},{Convert.ToBase64String(publicKey)},{Session.ClientId}";
                _ = Task.Factory.StartNew(() => LoginScanCodeEvent?.Invoke(loginUrl));

            });

        }
        private async Task<Node> GetDecryptNode(ReceiveModel rm)
        {
            if (rm.Nodes != null)
            {
                return rm.Nodes;
            }
            if (rm.MessageType == WebSocketMessageType.Binary && rm.ByteData.Length >= 33)
            {
                while (!_loginSuccess)
                {
                    await Task.Delay(100);
                }
                var tindex = Array.IndexOf(rm.ByteData, (byte)44, 0, rm.ByteData.Length);
                var wd = rm.ByteData.Skip(tindex + 1).ToArray();
                var data = wd.Skip(32).ToArray();
                if (!wd.Take(32).ToArray().ValueEquals(data.HMACSHA256_Encrypt(Session.MacKey)))
                {
                    return null;
                }
                var decryptData = data.AesCbcDecrypt(Session.EncKey);
                var bd = new BinaryDecoder(decryptData);
                var node = bd.ReadNode();
                rm.Nodes = node;
                return rm.Nodes;
            }
            return null;
        }
        private async Task ReceiveHandle(ReceiveModel rm)
        {
            var node = await GetDecryptNode(rm);
            if (rm.Tag != null && _snapReceiveDictionary.ContainsKey(rm.Tag))
            {
                var result = await Task.Factory.StartNew(() => _snapReceiveDictionary[rm.Tag](rm));
                if (result)
                {
                    lock (_snapReceiveLock)
                    {
                        if (_snapReceiveRemoveCountDictionary.ContainsKey(rm.Tag))
                        {
                            if (_snapReceiveRemoveCountDictionary[rm.Tag] <= 1)
                            {
                                _snapReceiveRemoveCountDictionary.Remove(rm.Tag);
                            }
                            else
                            {
                                _snapReceiveRemoveCountDictionary[rm.Tag] = _snapReceiveRemoveCountDictionary[rm.Tag] - 1;
                                return;
                            }
                        }
                        _snapReceiveDictionary.Remove(rm.Tag);
                    }
                    return;
                }
            }
            if (node != null)
            {
                if (node.Content is List<Node> nodeList)
                {
                    foreach (var item in nodeList)
                    {
                        if (item.Description == "message")
                        {
                            var messageData = item.Content as byte[];
                            var ms = WebMessageInfo.Parser.ParseFrom(messageData);
                            if (ms.Message != null)
                            {
                                if (ms.Message.ImageMessage != null && ReceiveImageMessageEvent != null)
                                {
                                    _ = Task.Factory.StartNew(async () =>
                                    {
                                        try
                                        {

                                            var fileData = await DownloadImage(ms.Message.ImageMessage.Url, ms.Message.ImageMessage.MediaKey.ToArray());
                                            ReceiveImageMessageEvent.Invoke(new Messages.ImageMessage
                                            {
                                                MessageTimestamp = ms.MessageTimestamp,
                                                RemoteJid = ms.Key.RemoteJid,
                                                Text = ms.Message.ImageMessage.Caption,
                                                ImageData = fileData,
                                                MsgId = ms.Key.Id,
                                                FromMe = ms.Key.FromMe,
                                                Status = (int)ms.Status,
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            LoadMediaInfo(ms.Key.RemoteJid, ms.Key.Id, ms.Key.FromMe ? "true" : "false", async _ =>
                                             {
                                                 try
                                                 {
                                                     var fileData = await DownloadImage(ms.Message.ImageMessage.Url, ms.Message.ImageMessage.MediaKey.ToArray());
                                                     var ignore = Task.Factory.StartNew(() => ReceiveImageMessageEvent.Invoke(new Messages.ImageMessage
                                                     {
                                                         MessageTimestamp = ms.MessageTimestamp,
                                                         RemoteJid = ms.Key.RemoteJid,
                                                         Text = ms.Message.ImageMessage.Caption,
                                                         ImageData = fileData
                                                     }));
                                                 }
                                                 catch
                                                 {
                                                     LogUtil.Error($"图片下载失败");
                                                     return;
                                                 }
                                             });
                                        }
                                    });
                                }
                                else if (ms.Message.HasConversation && ReceiveTextMessageEvent != null)
                                {
                                    _ = Task.Factory.StartNew(() => ReceiveTextMessageEvent?.Invoke(new TextMessage
                                    {
                                        MessageTimestamp = ms.MessageTimestamp,
                                        RemoteJid = ms.Key.RemoteJid,
                                        Text = ms.Message.Conversation,
                                        MsgId = ms.Key.Id,
                                        FromMe = ms.Key.FromMe,
                                        Status = (int)ms.Status,
                                    }));
                                }
                                else
                                {
                                    InvokeReceiveRemainingMessagesEvent(messageData);
                                }
                            }
                            else
                            {
                                InvokeReceiveRemainingMessagesEvent(messageData);
                            }
                        }
                        else if (item.Content is byte[] bs)
                        {
                            InvokeReceiveRemainingMessagesEvent(bs);
                        }
                    }
                }
                else
                {
                    InvokeReceiveRemainingMessagesEvent(rm);
                }
            }
            else
            {
                InvokeReceiveRemainingMessagesEvent(rm);
            }
        }
        private byte[] GetRandom(int length)
        {
            var random = new Random();
            byte[] bs = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bs[i] = (byte)random.Next(0, 255);
            }
            return bs;
        }
        private void InvokeReceiveRemainingMessagesEvent(ReceiveModel receiveModel)
        {
            Task.Factory.StartNew(() => ReceiveRemainingMessagesEvent?.Invoke(receiveModel));
        }
        private void InvokeReceiveRemainingMessagesEvent(byte[] data)
        {
            InvokeReceiveRemainingMessagesEvent(ReceiveModel.GetReceiveModel(data));
        }
        private void AddSnapReceive(string tag, Func<ReceiveModel, bool> func, int count = 0)
        {
            if (count != 0)
            {
                _snapReceiveRemoveCountDictionary.Add(tag, count);
            }
            _snapReceiveDictionary.Add(tag, func);
        }

        #endregion
    }
}

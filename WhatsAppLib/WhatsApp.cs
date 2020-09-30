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
using ImageMessage = Proto.ImageMessage;

namespace WhatsAppLib
{
    public class WhatsApp
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
        #endregion
        #region 公有属性    public property
        /// <summary>
        /// Http代理  Http Proxy
        /// </summary>
        public IWebProxy WebProxy { set => _webSocket.Options.Proxy = value; get => _webSocket?.Options.Proxy; }
        /// <summary>
        /// 登录Session   Login Session
        /// </summary>
        public SessionInfo Session { set; get; }
        #endregion
        #region 私有成员    private member
        private ClientWebSocket _webSocket;
        private object _sendObj = new object();
        private int _msgCount;
        private bool _loginSuccess;
        private Dictionary<string, Func<ReceiveModel, bool>> _snapReceiveDictionary = new Dictionary<string, Func<ReceiveModel, bool>>();
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
        public WhatsApp(WebProxy webProxy) : this()
        {
            _webSocket.Options.Proxy = webProxy;
        }
        public WhatsApp(SessionInfo session) : this()
        {
            Session = session;
        }
        public WhatsApp(SessionInfo session, WebProxy webProxy) : this(webProxy)
        {
            Session = session;
        }
        #endregion
        #region 公有方法    public method
        /// <summary>
        /// 登录,需要监听LoginScanCodeEvent,和LoginSuccessEvent事件  To log in, you need to monitor the LoginScanCodeEvent and LoginSuccessEvent events
        /// </summary>
        public void Login()
        {
            var task = _webSocket.ConnectAsync(new Uri("wss://web.whatsapp.com/ws"), CancellationToken.None);
            task.Wait();
            Receive(ReceiveModel.GetReceiveModel());
            Send("?,,");
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
                //SendJson("[\"admin\",\"test\"]");
            }
        }
        /// <summary>
        /// 发送图片消息
        /// </summary>
        /// <param name="remoteJid">发送给谁</param>
        /// <param name="data">图片字节码</param>
        /// <param name="caption">消息名称</param>
        /// <returns></returns>
        public string SendImage(string remoteJid, byte[] data, string caption = null)
        {
            var uploadResponse = Upload(data, MediaImage);
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
            });
        }
        public string SendText(string remoteJid, string text)
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
            });
        }
        #endregion
        #region 私有方法    private method
        private void HandleMessage(WebMessageInfo webMessage)
        {
            var message = webMessage.Message;
            if (webMessage.MessageTimestamp <= (ulong)(DateTime.Now.GetTimeStampLong() / 1000 - 240))
            {
                return;
            }
            if (message.ImageMessage != null)
            {
                var fileData = DownloadImage(message.ImageMessage.Url, message.ImageMessage.MediaKey.ToArray());
                File.WriteAllBytes("test.jpg", fileData);
            }
            else if (message.HasConversation)
            {
                if (message.Conversation == "图片")
                {
                    SendImage(webMessage.Key.RemoteJid, File.ReadAllBytes("test.jpg"), "OK");
                }
            }
        }
        private byte[] DownloadImage(string url, byte[] mediaKey)
        {
            return Download(url, mediaKey, MediaImage);
        }
        private byte[] Download(string url, byte[] mediaKey, string info)
        {
            var webClient = new WebClient()
            {
                Encoding = Encoding.UTF8,
                Proxy = WebProxy,
                UseDefaultCredentials = false
            };
            var stream = webClient.OpenRead($"{url}");
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            stream.Close();
            var mk = GetMediaKeys(mediaKey, info);
            var data = memory.ToArray();
            var file = data.Take(data.Length - 10).ToArray();
            var mac = data.Skip(file.Length).ToArray();
            var sign = (mk.Iv.Concat(file).ToArray()).HMACSHA256_Encrypt(mk.MacKey);
            if (!sign.Take(10).ToArray().ValueEquals(mac))
            {
                Console.WriteLine("invalid media hmac");
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
        private UploadResponse Upload(byte[] data, string info)
        {
            var uploadResponse = new UploadResponse();
            uploadResponse.FileLength = (ulong)data.Length;
            uploadResponse.MediaKey = GetRandom(32);
            var mk = GetMediaKeys(uploadResponse.MediaKey, MediaImage);
            var enc = data.AesCbcEncrypt(mk.CipherKey, mk.Iv);
            var mac = (mk.Iv.Concat(data).ToArray()).HMACSHA256_Encrypt(mk.MacKey);
            uploadResponse.FileSha256 = data.SHA256_Encrypt();
            var joinData = enc.Concat(mac).ToArray();
            uploadResponse.FileEncSha256 = joinData.SHA256_Encrypt();
            var mediaConnResponse = QueryMediaConn();
            if (mediaConnResponse == null)
            {
                return null;
            }
            var token = Convert.ToBase64String(uploadResponse.FileEncSha256);
            var url = $"https://{mediaConnResponse.MediaConn.Hosts[0].Hostname}{MediaTypeMap[info]}/{token}?auth={mediaConnResponse.MediaConn.Auth}&token={token}";
            WebClient webClient = new WebClient
            {
                Encoding = Encoding.UTF8,
                Proxy = WebProxy
            };
            webClient.Headers["Origin"] = "https://web.whatsapp.com";
            webClient.Headers["Referer"] = "https://web.whatsapp.com/";
            var responseByte = webClient.UploadData(url, joinData);
            var response = Encoding.UTF8.GetString(responseByte);
            uploadResponse.DownloadUrl = response.RegexGetString("url\":\"([^\"]*)\"");
            return uploadResponse;
        }
        private MediaConnResponse QueryMediaConn()
        {
            MediaConnResponse connResponse = null;
            var tag = SendJson("[\"query\",\"mediaConn\"]");
            _snapReceiveDictionary.Add(tag, rm =>
             {
                 connResponse = JsonConvert.DeserializeObject<MediaConnResponse>(rm.Body);
                 return true;
             });
            for (int i = 0; i < 1000; i++)
            {
                if (connResponse != null)
                {
                    return connResponse;
                }
                Thread.Sleep(10);
            }
            return connResponse;
        }

        private string SendProto(WebMessageInfo webMessage)
        {
            if (webMessage.Key.Id.IsNullOrWhiteSpace())
            {
                webMessage.Key.Id = GetRandom(10).ToHexString().ToUpper();
            }
            if (webMessage.MessageTimestamp == 0)
            {
                webMessage.MessageTimestamp = (ulong)(DateTime.Now.GetTimeStampLong() / 1000);
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
            WriteBinary(n, webMessage.Key.Id);
            return webMessage.Key.Id;
        }
        private void WriteBinary(Node node, string messageTag)
        {
            var data = EncryptBinaryMessage(node);
            var bs = new List<byte>(Encoding.UTF8.GetBytes($"{messageTag},"));
            bs.Add(16);
            bs.Add(128);
            bs.AddRange(data);
            lock (_sendObj)
            {
                _webSocket.SendAsync(new ArraySegment<byte>(bs.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None).Wait();
            }
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
                LoginSuccessEvent?.Invoke(Session);
                _loginSuccess = true;
            }
            else
            {
                _snapReceiveDictionary.Add("s2", LoginResponseHandle);
                ResolveChallenge(challenge);
            }
            return true;
        }
        private void ReLogin()
        {
            _snapReceiveDictionary.Add("s1", LoginResponseHandle);
            SendJson($"[\"admin\",\"login\",\"{Session.ClientToken}\",\"{Session.ServerToken}\",\"{Session.ClientId}\",\"takeover\"]");
            SendJson($"[\"admin\",\"init\",[2,2033,7],[\"Windows\",\"Chrome\",\"10\"],\"{Session.ClientId}\",true]");
        }
        private void ResolveChallenge(string challenge)
        {
            var decoded = Convert.FromBase64String(challenge);
            var loginChallenge = decoded.HMACSHA256_Encrypt(Session.MacKey);
            SendJson($"[\"admin\",\"challenge\",\"{Convert.ToBase64String(loginChallenge)}\",\"{Session.ServerToken}\",\"{Session.ClientId}\"]");
        }
        private void WhatsAppLogin()
        {
            Task.Factory.StartNew(() =>
            {
                var clientId = GetRandom(16);
                Session.ClientId = Convert.ToBase64String(clientId);
                var tag = SendJson($"[\"admin\",\"init\",[2,2033,7],[\"Windows\",\"Chrome\",\"10\"],\"{Session.ClientId}\",true]");
                string refUrl = null;
                _snapReceiveDictionary.Add(tag, rm =>
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
                _snapReceiveDictionary.Add("s1", rm =>
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
                        Console.WriteLine("签名校验错误");
                        return true;
                    }
                    var keysEncrypted = new byte[96];
                    Array.Copy(sharedSecretExtended, 64, keysEncrypted, 0, 16);
                    Array.Copy(decodedSecret, 64, keysEncrypted, 16, 80);
                    var keyDecrypted = decodedSecret.Skip(64).ToArray().AesCbcDecrypt(sharedSecretExtended.Take(32).ToArray(), sharedSecretExtended.Skip(64).ToArray());
                    Session.EncKey = keyDecrypted.Take(32).ToArray();
                    Session.MacKey = keyDecrypted.Skip(32).ToArray();
                    LoginSuccessEvent?.Invoke(Session);
                    _loginSuccess = true;
                    return true;
                });
                while (refUrl.IsNullOrWhiteSpace())
                {
                    Thread.Sleep(1);
                }
                var loginUrl = $"{refUrl},{Convert.ToBase64String(publicKey)},{Session.ClientId}";
                LoginScanCodeEvent?.Invoke(loginUrl);

            });

        }
        public string SendJson(string str)
        {
            var tag = $"{DateTime.Now.GetTimeStampLong() / 1000}.--{Interlocked.Increment(ref _msgCount) - 1}";
            Send($"{tag},{str}");
            return tag;
        }
        private void Send(string str)
        {
            Send(Encoding.UTF8.GetBytes(str));
        }
        private void Send(byte[] bs)
        {
            lock (_sendObj)
            {
                _webSocket.SendAsync(new ArraySegment<byte>(bs, 0, bs.Length), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
        }
        private void Receive(ReceiveModel receiveModel)
        {
            Task.Factory.StartNew(() =>
            {
                var receiveResult = _webSocket.ReceiveAsync(receiveModel.ReceiveData, CancellationToken.None);
                receiveResult.Wait();
                if (receiveResult.Result.EndOfMessage)
                {
                    Receive(ReceiveModel.GetReceiveModel());
                    receiveModel.End(receiveResult.Result.Count, receiveResult.Result.MessageType);
                    ReceiveHandle(receiveModel);
                }
                else
                {
                    receiveModel.Continue(receiveResult.Result.Count);
                    Receive(receiveModel);
                }
            });

        }
        public void ReceiveHandle(ReceiveModel rm)
        {
            if (rm.Tag != null && _snapReceiveDictionary.ContainsKey(rm.Tag))
            {
                if (_snapReceiveDictionary[rm.Tag](rm))
                {
                    _snapReceiveDictionary.Remove(rm.Tag);
                    return;
                }
            }
            if (rm.MessageType == WebSocketMessageType.Binary && rm.ByteData.Length >= 33)
            {
                while (!_loginSuccess)
                {
                    Thread.Sleep(10);
                }
                var tindex = Array.IndexOf(rm.ByteData, (byte)44, 0, rm.ByteData.Length);
                var wd = rm.ByteData.Skip(tindex + 1).ToArray();
                var data = wd.Skip(32).ToArray();
                if (!wd.Take(32).ToArray().ValueEquals(data.HMACSHA256_Encrypt(Session.MacKey)))
                {
                    InvokeReceiveRemainingMessagesEvent(rm);
                    return;
                }
                var decryptData = data.AesCbcDecrypt(Session.EncKey);
                var bd = new BinaryDecoder(decryptData);
                var str1 = Convert.ToBase64String(decryptData);
                var node = bd.ReadNode();
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
                                    var fileData = DownloadImage(ms.Message.ImageMessage.Url, ms.Message.ImageMessage.MediaKey.ToArray());
                                    ReceiveImageMessageEvent.Invoke(new Messages.ImageMessage
                                    {
                                        MessageTimestamp = ms.MessageTimestamp,
                                        RemoteJid = ms.Key.RemoteJid,
                                        Text = ms.Message.ImageMessage.Caption,
                                        ImageData = fileData
                                    });
                                }
                                else if (ms.Message.HasConversation && ReceiveTextMessageEvent != null)
                                {
                                    ReceiveTextMessageEvent?.Invoke(new TextMessage
                                    {
                                        MessageTimestamp = ms.MessageTimestamp,
                                        RemoteJid = ms.Key.RemoteJid,
                                        Text = ms.Message.Conversation,
                                    });
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
                        else if(item.Content is byte[] bs)
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
            ReceiveRemainingMessagesEvent?.Invoke(receiveModel);
        }
        private void InvokeReceiveRemainingMessagesEvent(byte[] data)
        {
            InvokeReceiveRemainingMessagesEvent(ReceiveModel.GetReceiveModel(data));
        }
        #endregion
    }
}

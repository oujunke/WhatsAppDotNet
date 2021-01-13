using Gma.QrCodeNet.Encoding;
using Newtonsoft.Json;
using System;
using System.IO;
using WhatsAppLib;
using WhatsAppLib.Messages;
using WhatsAppLib.Models;

namespace WhatsAppDotNet
{
    class Program
    {
        static void Main(string[] args)
        {
            //使用代理
            //WhatsApp whatsApp = new WhatsApp(new Yove.Proxy.ProxyClient("127.0.0.1", 8888,Yove.Proxy.ProxyType.Http));
            WhatsApp whatsApp = new WhatsApp();
            if (File.Exists("Session.ini"))
            {
                whatsApp.Session = JsonConvert.DeserializeObject<SessionInfo>(File.ReadAllText("Session.ini"));
            }
            whatsApp.LoginScanCodeEvent += WhatsApp_LoginScanCodeEvent;
            whatsApp.LoginSuccessEvent += WhatsApp_LoginSuccessEvent;
            whatsApp.ReceiveImageMessageEvent += WhatsApp_ReceiveImageMessageEvent;
            whatsApp.ReceiveTextMessageEvent += WhatsApp_ReceiveTextMessageEvent;
            whatsApp.ReceiveRemainingMessagesEvent += WhatsApp_ReceiveRemainingMessagesEvent;
            whatsApp.Login();
            Console.ReadLine();
        }

        private static void WhatsApp_ReceiveTextMessageEvent(TextMessage obj)
        {
            Console.WriteLine($"{obj.RemoteJid}-{obj.MessageTimestamp}-{obj.Text}");
        }

        private static void WhatsApp_ReceiveRemainingMessagesEvent(ReceiveModel obj)
        {
            Console.WriteLine($"{obj.StringData}");
        }

        private static void WhatsApp_ReceiveImageMessageEvent(ImageMessage obj)
        {
            Console.WriteLine($"{obj.RemoteJid}-{obj.MessageTimestamp}-{obj.Text}-{obj.ImageData?.Length}");
        }

        private static void WhatsApp_LoginSuccessEvent(SessionInfo obj)
        {
            File.WriteAllText("Session.ini", JsonConvert.SerializeObject(obj));
        }

        private static void WhatsApp_LoginScanCodeEvent(string obj)
        {
            Console.WriteLine($"请使用手机WhatsApp扫描该二维码登录(Please use your mobile WhatsApp to scan the QR code to log in):{obj}");
            //createQRCode(obj);
        }
        //private static void createQRCode(string sampleText)
        //{
        //    QrEncoder qrEncoder = new QrEncoder(ErrorCorrectionLevel.M);
        //    QrCode qrCode = qrEncoder.Encode(sampleText);
        //    for (int j = 0; j < qrCode.Matrix.Width; j++)
        //    {
        //        for (int i = 0; i < qrCode.Matrix.Width; i++)
        //        {
        //            Console.Write(i == 0 ? "\t" : "");
        //            Console.BackgroundColor = qrCode.Matrix[i, j] ? ConsoleColor.Black : ConsoleColor.White;
        //            Console.Write('　');//中文全角的空格符
        //            Console.BackgroundColor = ConsoleColor.White;
        //            Console.Write(i == qrCode.Matrix.Width - 1 ? "\t" : "");
        //        }
        //        Console.WriteLine();
        //    }
        //}
    }
}

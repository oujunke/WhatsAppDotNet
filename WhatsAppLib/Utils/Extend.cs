using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WhatsAppLib.Utils
{
    internal static class Extend
    {
        /// <summary>
        /// 正则获取匹配的字符串  Regularly get the matched string
        /// </summary>
        /// <param name="str"></param>
        /// <param name="pattern"></param>
        /// <param name="retuenIndex"></param>
        /// <returns></returns>
        public static string RegexGetString(this string str, string pattern, int retuenIndex = 1)
        {
            Regex r = new Regex(pattern, RegexOptions.None);
            return r.Match(str).Groups[retuenIndex].Value;
        }
        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static long GetTimeStampInt(this DateTime dateTime)
        {
            return ((dateTime.ToUniversalTime().Ticks - 621355968000000000) / 10000000);
        }

        /// <summary>
        /// 时间戳转时间
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        public static DateTime GetDateTime(this string timeStamp)
        {
            if (string.IsNullOrWhiteSpace(timeStamp))
            {
                return DateTime.MinValue;
            }
            var num = long.Parse(timeStamp);
            DateTime dtStart = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
            if (num > 9466560000)
            {
                TimeSpan toNow = new TimeSpan(num * 10000);
                return dtStart.Add(toNow);
            }
            else
            {
                TimeSpan toNow = new TimeSpan(num * 1000 * 10000);
                return dtStart.Add(toNow);
            }
        }

        public static bool IsNullOrWhiteSpace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }
        /// <summary>
        /// 使用HMACSHA256进行加密
        /// </summary>
        /// <param name="bs"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] HMACSHA256_Encrypt(this byte[] bs, byte[] key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                byte[] computedHash = hmac.ComputeHash(bs);
                return computedHash;
            }
        }
        /// <summary>
        /// SHA256加密
        /// </summary>
        /// <param name="bs"></param>
        /// <returns></returns>
        public static byte[] SHA256_Encrypt(this byte[] bs)
        {
            HashAlgorithm iSha = new SHA256CryptoServiceProvider();
            return iSha.ComputeHash(bs);
        }
        /// <summary>
        /// 判断是否相同
        /// </summary>
        /// <param name="bs"></param>
        /// <param name="bs2"></param>
        /// <returns></returns>
        public static bool ValueEquals(this byte[] bs, byte[] bs2)
        {
            if (bs.Length != bs.Length)
            {
                return false;
            }
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] != bs2[i])
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 字节转字节字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToHexString(this byte[] bytes)
        {
            string hexString = string.Empty;
            if (bytes != null)
            {
                StringBuilder strB = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    strB.Append(bytes[i].ToString("X2"));
                }
                hexString = strB.ToString();
            }
            return hexString;
        }
        public static byte[] AesCbcDecrypt(this byte[] data, byte[] key, byte[] iv)
        {
            var rijndaelCipher = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = key.Length * 8,
                BlockSize = iv.Length * 8
            };
            rijndaelCipher.Key = key;
            rijndaelCipher.IV = iv;
            var transform = rijndaelCipher.CreateDecryptor();
            var plainText = transform.TransformFinalBlock(data, 0, data.Length);
            return plainText;
        }
        public static byte[] AesCbcEncrypt(this byte[] data, byte[] key, byte[] iv)
        {
            var rijndaelCipher = new RijndaelManaged
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = key.Length * 8,
                BlockSize = iv.Length * 8
            };
            rijndaelCipher.Key = key;
            rijndaelCipher.IV = iv;
            var transform = rijndaelCipher.CreateEncryptor();
            var plainText = transform.TransformFinalBlock(data, 0, data.Length);
            return plainText;
        }
        public static byte[] AesCbcDecrypt(this byte[] data, byte[] key)
        {
            return AesCbcDecrypt(data.Skip(16).ToArray(), key, data.Take(16).ToArray());
        }
        public static async Task<MemoryStream> GetStream(this string url, WebProxy webProxy = null)
        {
            MemoryStream memory = new MemoryStream();
            HttpClientHandler Handler = new HttpClientHandler { Proxy = webProxy };
            using (var client = new HttpClient(Handler))
            {
                var message = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version20,
                };
                message.Headers.Add("user-agent", "Mozilla/5.0 (MSIE 10.0; Windows NT 6.1; Trident/5.0)");
                var response = await client.SendAsync(message);
                if (response.IsSuccessStatusCode)
                {
                    await response.Content.CopyToAsync(memory);
                }
            }
            return memory;
        }
        public static async Task<MemoryStream> Post(this string url, byte[] data, WebProxy webProxy = null, Dictionary<string, string> head = null)
        {
            MemoryStream memory = new MemoryStream();
            HttpClientHandler Handler = new HttpClientHandler { Proxy = webProxy };
            using (var client = new HttpClient(Handler))
            {
                var message = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version20,
                };
                message.Content = new ByteArrayContent(data);
                message.Headers.Add("user-agent", "Mozilla/5.0 (MSIE 10.0; Windows NT 6.1; Trident/5.0)");
                if (head != null)
                {
                    foreach (var item in head)
                    {
                        message.Headers.Add(item.Key, item.Value);
                    }
                }
                var response = await client.SendAsync(message);
                if (response.IsSuccessStatusCode)
                {
                    await response.Content.CopyToAsync(memory);
                }
            }
            return memory;
        }
        public static async Task<string> PostHtml(this string url, byte[] data, WebProxy webProxy = null, Dictionary<string, string> head = null, Encoding encoding = null)
        {
            var memory = await Post(url, data, webProxy, head);
            if (encoding == null)
            {
                return Encoding.UTF8.GetString(memory.ToArray());
            }
            else
            {
                return encoding.GetString(memory.ToArray());
            }
        }
    }
}

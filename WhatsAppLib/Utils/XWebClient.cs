using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WhatsAppLib.Utils
{
   public class XWebClient:WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest webRequest = base.GetWebRequest(address);
            bool flag = webRequest is HttpWebRequest;
            if (flag)
            {
                ((HttpWebRequest)webRequest).KeepAlive = false;
            }
            return webRequest;
        }
    }
}

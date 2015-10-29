using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Configuration;

namespace Proxy
{
    /// <summary>
    /// Do 的摘要说明
    /// </summary>
    public class Do : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            string realUrl = context.Request.Path;           
            if (!string.IsNullOrWhiteSpace(realUrl))
            {
                string ap = context.Request.ApplicationPath;
                context.Response.AddHeader("proxy-url", ap);
                Regex rx = new Regex("^(" + ap + ")", RegexOptions.IgnoreCase);
                realUrl = rx.Replace(realUrl, "");
                if (string.IsNullOrWhiteSpace(realUrl))
                {
                    realUrl = "/";
                }
                else
                {
                    if (realUrl[0] != '/')
                    {
                        realUrl = "/" + realUrl;
                    }
                }
                string qs = context.Request.QueryString + "";
                realUrl = ConfigurationManager.AppSettings["proxy_base_url"] + realUrl;
                if (!string.IsNullOrWhiteSpace(qs))
                {
                    realUrl += "?" + qs;
                }
                context.Response.AddHeader("proxy-real-url", realUrl);
                HttpWebRequest wReq = (HttpWebRequest)WebRequest.Create(realUrl);
                wReq.AllowAutoRedirect = true;
                wReq.Method = context.Request.HttpMethod;
                wReq.ContentType = context.Request.ContentType;
                wReq.UserAgent = context.Request.UserAgent;
                if (context.Request.AcceptTypes != null && context.Request.AcceptTypes.Length > 0)
                {
                    string accept = "";
                    foreach (string ai in context.Request.AcceptTypes)
                    {
                        if (!string.IsNullOrWhiteSpace(accept))
                        {
                            accept += ";";
                        }
                        accept += ai;
                    }
                    if (!string.IsNullOrWhiteSpace(accept))
                    {
                        wReq.Accept = accept;
                    }
                }
                foreach (string h in context.Request.Headers.Keys)
                {
                    if (h == "Connection" || h == "Accept" || h == "Host" || h == "User-Agent"
                       || h == "Content-Length" || h == "Content-Type" || h == "Referer" || h == "If-Modified-Since")
                    {
                        continue;
                    }
                    else
                    {
                        wReq.Headers.Add(h, context.Request.Headers[h]);
                    }
                }
                string ifm = context.Request.Headers["If-Modified-Since"];
                if (!string.IsNullOrWhiteSpace(ifm))
                {
                    wReq.IfModifiedSince = DateTime.Parse(ifm);
                }
                wReq.Referer = context.Request.Headers["Referer"];
                wReq.ContentLength = context.Request.ContentLength;
                if (wReq.Method == "POST" && context.Request.ContentLength > 0)
                {
                    byte[] rd = new byte[4 * 1024];
                    int rc = 0;
                    Stream rs = context.Request.InputStream;
                    Stream ins = wReq.GetRequestStream();
                    while ((rc = rs.Read(rd, 0, rd.Length)) > 0)
                    {
                        ins.Write(rd, 0, rc);
                    }
                    ins.Close();

                }
               
                HttpWebResponse wResp;
                try
                {
                    wResp = wReq.GetResponse() as HttpWebResponse;                    
                    readResponse(wResp, context, WebExceptionStatus.Success);                    
                }
                catch (WebException ex)
                {
                    wResp = ex.Response as HttpWebResponse;
                    readResponse(wResp, context, ex.Status);
                }
            }
            else
            {
                context.Response.AddHeader("proxy-real-url-error", realUrl);
            }
        }



        private void readResponse(HttpWebResponse wResp, HttpContext context,WebExceptionStatus s)
        {           
            foreach (string h in wResp.Headers.Keys)
            {
                if (!(h == "Content-Length" || h == "Transfer-Encoding"))
                {                   
                    context.Response.AppendHeader(h, wResp.Headers[h]);                  
                }
            }
            if (s == WebExceptionStatus.ProtocolError)
            {
                context.Response.StatusCode = (int)wResp.StatusCode;
            }
            context.Response.ContentType = wResp.ContentType;
            if (wResp != null)
            {
                using (wResp)
                {
                    using (System.IO.Stream ins = wResp.GetResponseStream())
                    {
                        byte[] data = new byte[4 * 1024];
                        int c = 0;
                        while ((c = ins.Read(data, 0, data.Length)) > 0)
                        {
                            byte[] bb = new byte[c];
                            Buffer.BlockCopy(data, 0, bb, 0, c);
                            context.Response.BinaryWrite(bb);
                            bb = null;
                        }
                        data = null;
                    }
                }
            }

        }
        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Web;
using System.Threading;
using HtmlAgilityPack;

namespace MvcApplication45.Common {
    public class ScrapeUtility {
        public enum RedirectType {
            AutoRedirect,
            NoRedirect,
            CustomRedirect
        }
        public static string FetchPageByPost(string URL, string Body, string Referer, CookieContainer cookies = null) {
            HttpWebRequest WebReq =
                (HttpWebRequest)WebRequest.Create(URL);
            WebReq.CookieContainer = cookies;
            WebReq.Host = WebReq.RequestUri.Host;
            WebReq.KeepAlive = true;

            WebReq.Method = "POST";
            WebReq.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36";
            WebReq.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            WebReq.Headers.Add("Accept-Language: en-US,en");
            WebReq.ContentType = "application/x-www-form-urlencoded";
            WebReq.Referer = Referer;

            byte[] buffer = Encoding.ASCII.GetBytes(Body);
            WebReq.ContentLength = buffer.Length;
            Stream PostData = WebReq.GetRequestStream();
            PostData.Write(buffer, 0, buffer.Length);
            PostData.Close();

            using (HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse()) {
                if (WebResp.StatusCode == HttpStatusCode.OK || WebResp.StatusCode == HttpStatusCode.NotModified) {
                    var response = new StreamReader(WebResp.GetResponseStream());
                    var HTML = response.ReadToEnd();

                    return HTML;
                }
                throw new WebException();
            }
        }

        public static string FetchPageByGet(string URL, CookieContainer cookies = null,
                string referer = null, RedirectType redirectType = RedirectType.AutoRedirect) {
            HttpWebRequest WebReq =
                (HttpWebRequest)WebRequest.Create(URL);
            WebReq.CookieContainer = cookies;
            WebReq.Host = WebReq.RequestUri.Host;
            WebReq.KeepAlive = true;
            WebReq.AllowAutoRedirect = redirectType == RedirectType.AutoRedirect;

            WebReq.Method = "GET";
            WebReq.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.63 Safari/537.36";
            WebReq.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
            WebReq.Headers.Add("Accept-Language: en-US,en");
            WebReq.Referer = referer;

            using (HttpWebResponse WebResp = (HttpWebResponse)WebReq.GetResponse()) {
                if (WebResp.StatusCode == HttpStatusCode.OK || WebResp.StatusCode == HttpStatusCode.NotModified) {
                    var Answer = new StreamReader(WebResp.GetResponseStream());
                    var HTML = Answer.ReadToEnd();

                    return HTML;
                }
                if (WebResp.StatusCode == HttpStatusCode.Found && redirectType == RedirectType.CustomRedirect)
                    return FetchPageByGet(WebResp.Headers["Location"], cookies, referer, redirectType);
                return "";
            }
        }

        public static string PostDataFromForm(string html, Dictionary<string, string> enteredData) {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Enter data into form.
            foreach (var entry in enteredData) {
                var attributes = doc.DocumentNode.SelectSingleNode("//*[@id='" + entry.Key + "']").Attributes;
                if (attributes["value"] == null)
                    attributes.Add("value", entry.Value);
                else
                    attributes["value"].Value = entry.Value;
            }

            string data = "";
            foreach (var element in doc.DocumentNode.SelectNodes("//input | //select")) {
                var nameAttribute = element.Attributes["name"];
                if (nameAttribute != null) {
                    var name = nameAttribute.Value;
                    data += name += "=";

                    var valueAttribute = element.Attributes["value"];
                    data += HttpUtility.UrlEncode(valueAttribute != null ? valueAttribute.Value : "");
                    data += "&";
                }
            }
            data = data.Remove(data.Length - 1);
            return data;
        }
    }
}

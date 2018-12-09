using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace ChromiumUpdate
{
    public static class HTTP
    {
        public static Stream Get(string URL, bool ErrNull = false)
        {
            return Get(new Uri(URL), ErrNull);
        }

        public static Stream Get(Uri URL, bool ErrNull = false)
        {
            var Req = WebRequest.CreateHttp(URL);
            Req.UserAgent = "ChromiumUpdate/" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " +https://github.com/AyrA/ChromiumUpdate";

            try
            {
                using (var Res = Req.GetResponse())
                {
                    var MS = new MemoryStream();
                    using (var S = Res.GetResponseStream())
                    {
                        S.CopyTo(MS);
                        MS.Position = 0;
                    }
                    return MS;
                }
            }
            catch (Exception ex)
            {
                AppLog.WriteException($"Error accessing {URL}", ex);
            }
            return ErrNull ? Stream.Null : null;
        }
    }
}

using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace ChromiumUpdate
{
    public static class Extensions
    {
        public static T FromJson<T>(this string s)
        {
            return s == null ? default(T) : JsonConvert.DeserializeObject<T>(s);
        }

        public static string ToJson(this object o, bool pretty = false, bool includeNull = false)
        {
            var Settings = new JsonSerializerSettings()
            {
                Formatting = pretty ? Formatting.Indented : Formatting.None,
                NullValueHandling = includeNull ? NullValueHandling.Include : NullValueHandling.Ignore
            };
            if (o == null)
            {
                return "null";
            }
            return JsonConvert.SerializeObject(o, Settings);
        }

        public static string ToString(this Stream S, Encoding E = null, bool LeaveOpen = false)
        {
            if (S == null)
            {
                return null;
            }
            if (E == null)
            {
                E = Encoding.UTF8;
            }
            using (var SR = new StreamReader(S, E, true, 1024, LeaveOpen))
            {
                return SR.ReadToEnd();
            }
        }

        public static string ToString(this byte[] Data,Encoding E = null)
        {
            if (E == null)
            {
                E = Encoding.UTF8;
            }
            return E.GetString(Data);
        }

        public static byte[] ReadAll(this Stream S)
        {
            using (var MS = new MemoryStream())
            {
                S.CopyTo(MS);
                return MS.ToArray();
            }
        }

        public static string TrimRight(this string S, int MaxLength, bool Ellipsis = true, bool Extend = false)
        {
            if (S == null)
            {
                S = string.Empty;
            }
            if (S.Length < MaxLength && Extend)
            {
                return S.PadRight(MaxLength);
            }
            if (S.Length > MaxLength)
            {
                if (Ellipsis)
                {
                    return S.Substring(0, MaxLength - 3) + "...";
                }
                return S.Substring(0, MaxLength);
            }
            return S;
        }
    }
}

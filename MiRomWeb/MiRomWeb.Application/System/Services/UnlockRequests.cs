using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace MiRomWeb.Application.System.Services;

public class UnlockRequests
{
    public class DeviceToken
    {
        public ushort TokenTag;
        public byte TokenVersion;
        public byte Length;
        public byte[] Random;
        public string Product;
        public byte[] Serials;

        public DeviceToken(byte[] blob)
        {
            using var ms = new MemoryStream(blob);
            using var br = new BinaryReader(ms);
            TokenTag = br.ReadUInt16();
            TokenVersion = br.ReadByte();
            Length = br.ReadByte();

            while (ms.Position != ms.Length)
            {
                var tag = br.ReadByte();
                var length = br.ReadByte();
                var data = br.ReadBytes(length);
                switch (tag)
                {
                    case 0x01:
                        Random = data;
                        break;
                    case 0x02:
                        Serials = data;
                        break;
                    case 0x03:
                        Product = Encoding.UTF8.GetString(data);
                        break;
                }

            }
        }
        public DeviceToken(string blob) : this(DecodeBase64Url(blob))
        {

        }
        private static byte[] DecodeBase64Url(string base64Url)
        {
            if (string.IsNullOrEmpty(base64Url))
                throw new ArgumentException("Input cannot be null or empty.", nameof(base64Url));
            if (base64Url.Length % 2 > 0)
            {
                base64Url = base64Url.Substring(base64Url.Length % 2);
            }
            // 替换 Base64Url 字符为标准 Base64 字符
            string padded = base64Url.Replace('-', '+').Replace('_', '/');

            // 补全填充（=）
            int padding = (4 - padded.Length % 4) % 4;
            if (padding > 0)
                padded += new string('=', padding);

            // 解码
            return Convert.FromBase64String(padded);
        }
        public static bool IsToken(string blob,out string error)
        {
            error = "错误的Token";
            try
            {
                var token = new DeviceToken(blob);
                var isToken = token.TokenTag == 341;
                if (!isToken)
                    error = "错误的Token版本";
                return isToken;
            }
            catch { }
            return false;
        }
    }
    private string _host;
    private MiServiceManager _service;

    public UnlockRequests(MiServiceManager service, string host = "unlock.update.miui.com")
    {
        _service = service;
        _host = host;
    }
    public async Task<bool> FrePermission()
    {
        string path = "/api/v1/recovery/permission";

        var request = new Requests(
            _service,
            HttpMethod.Post,
            _host,
            path,
            new()
            {
                { "appId", "1" },
                {
                    "data",
                    new JObject()
                    {
                        { "clientId", "1" },
                        { "clientVersion", "7.3.706.21" },
                        { "language", "en" },
                        { "operate", "permission" },
                        { "pcId", _service.GetPcId() },
                        //{ "product", "alioth" },
                        { "region", "" },
                        { "uid", _service.GetAccountInfo()?.UserId ?? throw new InvalidOperationException("无法获取UserId，请确保账户已初始化") },

                    }
                },
            }
        );
        //await request.AddNonce("mi_postsale_client");
        await request.AddNonce();
        var data = await request.Run();
        return data["data"]!["erasefrp"]!.ToObject<bool>();
    }
    public async Task<JObject> DeviceInfo(string product,string sn )
    {
        string path = "/api/v1/postsale/deviceInfo";
        //string path = "/api/v3/ahaUnlock";

        var request = new Requests(
            _service,
            HttpMethod.Post,
            _host,
            path,
            new()
            {
                { "appId", "1" },
                {
                    "data",
                    new JObject()
                    {
                        { "clientId", "2" },
                        { "clientVersion", "1.1.814.80" },
                        {
                            "deviceInfo",
                            new JObject()
                            {
                                { "deviceName", product },
                                { "product", product },

                            }
                        },
                        { "language", "en" },
                        { "pcId", _service.GetPcId() },
                        { "region","" },
                        { "sn",sn },
                        { "uid", _service.GetAccountInfo()?.UserId ?? throw new InvalidOperationException("无法获取UserId，请确保账户已初始化") },

                    }
                },
            }
        );
        //await request.AddNonce("mi_postsale_client");
        await request.AddNonce();
        var data = await request.Run();
        return data;
    }
}

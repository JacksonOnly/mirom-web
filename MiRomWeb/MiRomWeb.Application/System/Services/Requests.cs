using Flurl.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MiRomWeb.Application.System.Services;

internal class Requests
{
    private string _host;
    private string _path;
    private HttpMethod _method;
    private MiServiceManager _service;
    private Dictionary<string, object> _params;
    const string HMACSHA_KEY = "2tBeoEyJTunmWUGq7bQH2Abn0k2NhhurOaqBfyxCuLVgn4AVj7swcawe53uDUno";
    const string HMACSHA_KEY2 = "hRW3J8wXAZ1hcTRx9ruPn1OqrV5K1XtaN4R3cKn5SDpsm7kJGCCSZIYCKpxtz79";
    const string AES_IV = "0102030405060708";

    public Requests(MiServiceManager service, HttpMethod method, string host,string path,Dictionary<string,object> param)
    {
        _service = service;
        _host = host;
        _path = path;

        foreach(var (k,v) in param)
        {
            if(v is JObject j)
            {
                param[k] = Convert.ToBase64String(Encoding.UTF8.GetBytes(j.ToString()));
            }
        }
        _params = param;

        _method = method;
    }
    private string GetParams(string sep)
    {
        var sb = new StringBuilder();
        sb.Append(_method.ToString());
        sb.Append(sep);
        sb.Append(_path);
        sb.Append(sep);
        sb.Append(string.Join("&", _params.Select(kv => $"{kv.Key}={kv.Value}")));
        return sb.ToString();
    }
    public void AddSign()
    {
        var key = Encoding.UTF8.GetBytes(HMACSHA_KEY);
        var paramStr = GetParams("\n");
        using var hmac = new HMACSHA1(key);
        var sign = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(paramStr))).Replace("-", "").ToLower();
        _params["sign"] = sign;
    }
    public void AddSign_2()
    {
        var key = Encoding.UTF8.GetBytes(HMACSHA_KEY2);
        var paramStr = GetParams("\n");
        using var hmac = new HMACSHA1(key);
        var sign = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(paramStr))).Replace("-", "").ToLower();
        _params["sign"] = sign;
    }
    private async Task EncryptAsync()
    {
        var currentAccountId = _service.GetCurrentAccountId();
        var aesKey = await _service.GetAesKeyAsync(currentAccountId, "unlockApi");
        if (string.IsNullOrEmpty(aesKey))
        {
            throw new InvalidOperationException("无法获取AesKey，请确保账户已初始化");
        }

        foreach (var key in _params.Keys.ToList())
        {
            _params[key] = _Encrypt(_params[key].ToString()!, aesKey);
        }
    }
    
    private string _Encrypt(string value, string aesKey)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(aesKey);
        aes.IV = Encoding.UTF8.GetBytes(AES_IV);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(value);
        var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(encrypted);
    }
    
    public async Task AddSignatureAsync()
    {
        var currentAccountId = _service.GetCurrentAccountId();
        var aesKey = await _service.GetAesKeyAsync(currentAccountId, "unlockApi");
        if (string.IsNullOrEmpty(aesKey))
        {
            throw new InvalidOperationException("无法获取AesKey，请确保账户已初始化");
        }

        var paramStr = GetParams("&") + "&" + aesKey;
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(paramStr));
        _params["signature"] = Convert.ToBase64String(hash);
    }
    private async Task<string> _DecryptAsync(string value)
    {
        var currentAccountId = _service.GetCurrentAccountId();
        var aesKey = await _service.GetAesKeyAsync(currentAccountId, "unlockApi");
        if (string.IsNullOrEmpty(aesKey))
        {
            throw new InvalidOperationException("无法获取AesKey，请确保账户已初始化");
        }

        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(aesKey);
        aes.IV = Encoding.UTF8.GetBytes(AES_IV);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var encrypted = Convert.FromBase64String(value);
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
    public async Task AddNonce(string sid = "miui_unlocktool_client")
    {
        var r = new Requests(_service, _method, _host, "/api/v2/nonce",new  Dictionary<string, object>()
        {
            {"r",RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz",16) },
            {"sid",sid }
        });
        var response =  await r.Run();
        _params["nonce"] = response["nonce"]!.ToString();
        _params["sid"] = sid;
    }
    public async Task<JObject> Run()
    {
        AddSign();
        await EncryptAsync();
        await AddSignatureAsync();
        var data = JObject.Parse(await Send());
        var code = data["code"]!.ToObject<int>();
        if (code != 0)
        {
            throw new Exception($"未知的响应代码: {code}");
        }
        return data;
    }
    public async Task<JObject> JustRun()
    {
        var data = JObject.Parse(await Send());
        var code = data["code"]!.ToObject<int>();
        if (code != 0)
        {
            throw new Exception($"未知的响应代码: {code}");
        }
        return data;
    }
    private async Task<string> Send()
    {
        // 尝试所有可用账户（排除被限流的账户），直到成功或所有账户都失败
        var availableAccountIds = _service.GetAvailableAccountIds();
        if (availableAccountIds.Count == 0)
        {
            throw new InvalidOperationException("没有可用的账户（所有账户可能都被限流），请确保账户已初始化");
        }

        Exception lastException = null;
        
        foreach (var accountId in availableAccountIds)
        {
            try
            {
                // 切换到当前账户
                _service.SwitchAccount(accountId);
                
                // 异步获取ServiceToken（按需获取）
                var serviceToken = await _service.GetServiceTokenAsync(accountId,"unlockApi");
                if (serviceToken == null)
                {
                    // 获取Token失败，标记账户无效并尝试下一个
                    _service.MarkAccountAsInvalid(accountId, "获取ServiceToken失败");
                    continue;
                }

                var uriBuilder = new UriBuilder("https", _host);
                uriBuilder.Path = _path;
                var uri = uriBuilder.Uri;
                
                bool isUnauthorized = false;
                
                var responseStr = await uri
                    .OnError((call) =>
                    {
                        call.ExceptionHandled = true;
                        if (call.Response?.StatusCode is 401)
                        {
                            isUnauthorized = true;
                        }
                    })
                    .WithHeader("User-Agent", "XiaomiPCSuite")
                    .WithCookies(serviceToken)
                    .SendUrlEncodedAsync(_method, _params).ReceiveString();

                // 如果返回401，标记账户无效并尝试下一个
                if (isUnauthorized)
                {
                    _service.MarkAccountAsInvalid(accountId, "HTTP 401 未授权，Token可能已过期");
                    continue;
                }

                // 成功获取响应，解密并检查是否有限流错误
                var decryptedResponse = Encoding.UTF8.GetString(Convert.FromBase64String(await _DecryptAsync(responseStr)));
                
                // 检查响应中是否包含限流错误码
                try
                {
                    var responseObj = JObject.Parse(decryptedResponse);
                    var code = responseObj["code"]?.ToObject<long?>();
                    if (code == 400479102) // 操作太频繁错误码
                    {
                        // 标记账户为限流24小时
                        _service.MarkAccountAsRateLimited(accountId, 24);
                        continue; // 尝试下一个账户
                    }
                }
                catch
                {
                    // 如果解析失败，可能是正常的响应格式，继续处理
                }
                
                // 成功获取响应，返回结果
                return decryptedResponse;
            }
            catch (Exception ex)
            {
                lastException = ex;
                // 标记账户无效并尝试下一个
                _service.MarkAccountAsInvalid(accountId, $"请求失败: {ex.Message}");
                continue;
            }
        }

        // 所有账户都失败了
        throw new InvalidOperationException($"所有账户都无法完成请求: {(lastException != null ? lastException.Message : "未知错误")}", lastException);
    }

}

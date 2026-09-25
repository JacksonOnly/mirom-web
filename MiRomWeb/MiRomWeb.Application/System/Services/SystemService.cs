using Furion.JsonSerialization;
using MiRomWeb.Application.System.Dtos;
using MiRomWeb.Application.System.Services;
using MiRomWeb.Application.System.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Flurl;
using Flurl.Http;
using XiaomiLib;
using System.Text.RegularExpressions;
using MiRomWeb.Core.Helper;
using System.Web;

namespace MiRomWeb.Application
{
    public class SystemService : ISystemService, ITransient
    {
        private const string romUrl = "http://update.miui.com/api/miflashpro/roms";
        private const string fullRomUrl = "http://update.miui.com/api/miflashpro/fullroms";
        private const string productsUrl = "http://update.miui.com/api/miflashpro/allPublishProducts";
        private const string aesKey = "bWl1aW90YXZhbGlkZWQxMQ==";
        private const string aesIv = "0102030405060708";
        private const string miid = "110032";
        private const string userAgent = "Mozilla/5.0 (Windows NT 6.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36 MiFlashToolBox/1.1.814.73 tz/utc-8 resolution/2560x1440 language/zh-CN";

        // 手机信息查询接口
        private const string insuranceInfoUrl = "https://trade-api.retail.mi.com/mtop/mit-aftersale/serviceTabProvider/getInsuranceInfoByImei";
        private const string deviceInfoUrl = "https://trade-api.retail.mi.com/mtop/b2csvr/serviceCommonProvider/getInfoByImeiOrSn";
        private const string activationUrl = "https://m.mi.com/v1/miaftersale/ocl_imei";
        private const string findMyDeviceStatusUrl = "https://i.mi.com/support/anonymous/status";

        private const long ExpiredTokenCode = 999999999302;
        private const long RateLimitErrorCode = 400479102; // 操作太频繁，请稍后再试
        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MiServiceManager _serviceManager;

        public SystemService(IHttpClientFactory httpClientFactory, MiServiceManager serviceManager)
        {
            _httpClientFactory = httpClientFactory;
            _serviceManager = serviceManager;
        }

        public async Task<FullRomData> GetFullRom(string product)
        {
            if (string.IsNullOrWhiteSpace(product)) throw Oops.Oh("产品代号不能为空");
            var dict = new Dictionary<string, string>()
            {
                {"q", AESEncryption.Encrypt(JSON.Serialize(new
                {
                    device = product,
                    miid = miid,
                    product = product
                }),aesKey,Encoding.UTF8.GetBytes(aesIv),isBase64:true)},
                {"t" ,""},
                {"s","1" }
            };
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            using var content = new FormUrlEncodedContent(dict);
            using var response = await httpClient.PostAsync(fullRomUrl, content);
            response.EnsureSuccessStatusCode();
            var responseStr = await response.Content.ReadAsStringAsync();
            var baseDto = JsonConvert.DeserializeObject<OtaBaseDto<List<RomEntry>>>(responseStr);
            if (baseDto?.Code == 2000 && baseDto.Data != null)
                return new FullRomData() { Fastboot = baseDto.Data };
            throw Oops.Oh($"错误的响应代码: {baseDto?.Code}");

        }

        /// <summary>
        /// 获取手机信息（自动判断IMEI或SN）
        /// </summary>
        public async Task<PhoneInfo> GetPhoneInfo(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                throw Oops.Oh("关键字不能为空");
            }
            keyword = keyword.Trim();

            var phoneInfo = new PhoneInfo();
            string? imei = null;
            string? sn = null;
            UnlockRequests.DeviceToken? token = null;
            // 自动判断类型：IMEI是15位数字
            bool isImei = keyword.Length == 15 && Regex.IsMatch(keyword, @"^\d{15}$");
            bool isSn = keyword.IndexOf("/") > 0;
            bool isToken = UnlockRequests.DeviceToken.IsToken(keyword,out var msg);
            if (isImei &&!isSn)
            {
                imei = keyword;
            }
            else if (!isImei && isSn && !keyword.StartsWith("V"))
            {
                sn = keyword;
            }
            else if (isToken)
            {
                token = new UnlockRequests.DeviceToken(keyword);
            }
            else
            {
                throw Oops.Oh("可能是错误的信息哦");
            }


            try
            {
                if (isToken)
                {
                    var serial = BitConverter.ToString(token.Serials ?? []).Replace("-", "");
                    if(serial.Length == 0)
                    {
                        throw Oops.Oh("无法获取到IMEI (联发科请使用 oem get_token获取)");
                    }
                    var jobj = await GetUnlockDeviceInfoAsync(token.Product, serial);
                    var code = jobj["code"];
                    if(code.ToObject<int>() != 0)
                    {
                        throw Oops.Oh("内部错误");
                    }
                    var found = jobj["data"]["found"];
                    var imei1 = jobj["data"]["imei1"];
                    var imei2 = jobj["data"]["imei2"];
                    var serialNum = jobj["data"]["sn"];
                    if (found.ToObject<bool>() || imei1?.ToObject<string>()?.Length > 0 || serialNum?.ToObject<string>()?.Length > 0)
                    {
                        phoneInfo.IMEI1 = imei1?.ToObject<string>();
                        phoneInfo.IMEI2 = imei2?.ToObject<string>();
                        phoneInfo.SN = serialNum?.ToObject<string>();
                        imei = phoneInfo.IMEI1;
                        sn = phoneInfo.SN;
                        if (imei?.Length > 0)
                        {
                            isImei = true;
                            isSn = false;
                        }
                        else if (sn?.Length > 0)
                        {
                            isImei = false;
                            isSn = true;
                        }
                    }
                    else
                        throw Oops.Oh("无法获取到IMEI (联发科请使用 oem get_token获取)");

                }


                // 根据查询类型优化调用顺序
                // 注意：GetInsuranceInfoAsync 和 GetDeviceInfoAsync 内部会自动获取Token，无需传入
                if (isImei)
                {
                    // 如果有IMEI，先调用只需要IMEI的接口（查询三包和SN信息）
                    // 这个接口可以返回SN，后续接口可能需要SN
                    await GetInsuranceInfoAsync(null, imei, phoneInfo);
                    
                    // 如果从第一个接口获取到了SN，更新sn变量
                    if (string.IsNullOrEmpty(sn) && !string.IsNullOrEmpty(phoneInfo.SN))
                    {
                        sn = phoneInfo.SN;
                    }
                    
                    // 2. 查询机型信息（可以使用IMEI或SN）
                    await GetDeviceInfoAsync(null, imei, sn, phoneInfo);
                    
                    // 3. 并行执行激活日期和查找设备状态查询（它们相互独立）
                    // 需要先获取Token用于这两个接口
                    var serviceToken = await EnsureServiceTokenAsync();
                    var activationTask = GetActivationInfoAsync(serviceToken, imei, sn, phoneInfo);
                    var findDeviceTask = GetFindMyDeviceStatusAsync(serviceToken, imei, phoneInfo);
                    await Task.WhenAll(activationTask, findDeviceTask);
                }
                else // SN查询
                {
                    // 如果有SN，先调用只需要SN的接口（查询机型信息）
                    await GetDeviceInfoAsync(null, imei, sn, phoneInfo);
                    
                    // 如果从第一个接口获取到了IMEI，更新imei变量
                    if (string.IsNullOrEmpty(imei) && !string.IsNullOrEmpty(phoneInfo.IMEI1))
                    {
                        imei = phoneInfo.IMEI1;
                    }
                    
                    // 获取Token用于后续查询
                    var serviceToken = await EnsureServiceTokenAsync();
                    
                    // 并行执行剩余查询
                    var tasks = new List<Task>();
                    
                    // 如果有IMEI，查询三包和SN信息
                    if (!string.IsNullOrEmpty(imei))
                    {
                        tasks.Add(GetInsuranceInfoAsync(null, imei, phoneInfo));
                        tasks.Add(GetFindMyDeviceStatusAsync(serviceToken, imei, phoneInfo));
                    }
                    
                    // 查询激活日期（可以使用IMEI或SN）
                    tasks.Add(GetActivationInfoAsync(serviceToken, imei, sn, phoneInfo));
                    
                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch (TokenExpiredException)
            {
                // Token过期，刷新后重试（自动切换到可用账户）
                Log.Information("检测到Token过期，正在刷新并尝试切换账户...");
                var serviceTokenCookie = await RefreshServiceTokenAsync();
                if (string.IsNullOrEmpty(serviceTokenCookie))
                {
                    throw Oops.Oh("刷新失败，内部错误");
                }

                // 重新执行查询逻辑（使用并行优化）
                if (isImei)
                {
                    await GetInsuranceInfoAsync(null, imei, phoneInfo);
                    if (string.IsNullOrEmpty(sn) && !string.IsNullOrEmpty(phoneInfo.SN))
                    {
                        sn = phoneInfo.SN;
                    }
                    await GetDeviceInfoAsync(null, imei, sn, phoneInfo);
                    
                    // 并行执行激活日期和查找设备状态查询
                    var activationTask = GetActivationInfoAsync(serviceTokenCookie, imei, sn, phoneInfo);
                    var findDeviceTask = GetFindMyDeviceStatusAsync(serviceTokenCookie, imei, phoneInfo);
                    await Task.WhenAll(activationTask, findDeviceTask);
                }
                else // SN查询
                {
                    await GetDeviceInfoAsync(null, imei, sn, phoneInfo);
                    if (string.IsNullOrEmpty(imei) && !string.IsNullOrEmpty(phoneInfo.IMEI1))
                    {
                        imei = phoneInfo.IMEI1;
                    }
                    
                    var tasks = new List<Task>();
                    if (!string.IsNullOrEmpty(imei))
                    {
                        tasks.Add(GetInsuranceInfoAsync(null, imei, phoneInfo));
                        tasks.Add(GetFindMyDeviceStatusAsync(serviceTokenCookie, imei, phoneInfo));
                    }
                    tasks.Add(GetActivationInfoAsync(serviceTokenCookie, imei, sn, phoneInfo));
                    
                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks);
                    }
                }
            }
            if(isToken)
            {
                phoneInfo.IMEI1 = MaskHelper.MaskString(phoneInfo.IMEI1,4,5);
                phoneInfo.IMEI2 = MaskHelper.MaskString(phoneInfo.IMEI2,4,5);
                phoneInfo.SN = MaskHelper.MaskString(phoneInfo.SN,7,0);
            }
            return phoneInfo;
        }

        /// <summary>
        /// 初始化ServiceToken管理器（设置userId和passToken）
        /// </summary>
        public void InitializeServiceManager(string userId, string passToken, string? sid = null)
        {
            // 使用账户ID作为账户的唯一标识（使用userId作为accountId）
            var accountId = userId;
            _serviceManager.AddOrUpdateAccount(accountId, userId, passToken);
            
            if (!string.IsNullOrEmpty(sid))
            {
                _serviceManager.SetDefaultSid(sid);
            }
        }

        /// <summary>
        /// 预加载ServiceToken（在启动时调用）- 已弃用，Token现在按需加载
        /// </summary>
        [Obsolete("ServiceToken现在按需自动加载，无需预加载")]
        public async Task PreloadServiceTokenAsync()
        {
            Log.Information("ServiceToken现在按需自动加载，跳过预加载");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 确保有可用的ServiceToken Cookie字符串（支持自动切换账户）
        /// </summary>
        /// <param name="sid">指定的SID，如果为null则使用默认SID</param>
        /// <param name="userId">指定的UserId，如果为null则尝试所有账户</param>
        private async Task<string?> EnsureServiceTokenAsync(string? sid = null, string? userId = null)
        {
            // 如果指定了userId，尝试使用该账户
            if (!string.IsNullOrEmpty(userId))
            {
                var accountId = _serviceManager.FindAccountIdByUserId(userId);
                if (accountId != null)
                {
                    try
                    {
                        var token = await _serviceManager.GetServiceTokenCookieAsync(accountId, sid);
                        if (!string.IsNullOrEmpty(token))
                        {
                            return token;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("获取指定账户的ServiceToken失败: UserId={UserId}, 错误: {Error}", userId, ex.Message);
                        // 继续尝试其他账户
                    }
                }
            }

            // 先尝试通过SID查找已有可用的Token
            if (!string.IsNullOrEmpty(sid))
            {
                var availableToken = _serviceManager.FindAvailableTokenBySid(sid);
                if (availableToken.HasValue)
                {
                    Log.Debug("找到已存在的ServiceToken: AccountId={AccountId}, SID={Sid}", 
                        availableToken.Value.AccountId, sid);
                    return availableToken.Value.CookieValue;
                }
            }

            // 尝试所有可用账户（排除被限流的账户），直到找到可用的
            var availableAccountIds = _serviceManager.GetAvailableAccountIds();
            if (availableAccountIds.Count == 0)
            {
                Log.Warning("没有可用的账户（所有账户可能都被限流），请先调用InitializeServiceManager设置userId和passToken");
                return null;
            }

            foreach (var accountId in availableAccountIds)
            {
                try
                {
                    var token = await _serviceManager.GetServiceTokenCookieAsync(accountId, sid);
                    if (!string.IsNullOrEmpty(token))
                    {
                        Log.Debug("成功获取ServiceToken: AccountId={AccountId}, SID={Sid}", accountId, sid);
                        return token;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("账户获取ServiceToken失败，将尝试下一个: AccountId={AccountId}, 错误: {Error}", accountId, ex.Message);
                    // 异常已在GetServiceTokenCookieAsync中处理并标记账户无效，继续尝试下一个
                }
            }

            Log.Error("所有账户都无法获取ServiceToken");
            return null;
        }

        /// <summary>
        /// 刷新ServiceToken（支持自动切换账户）
        /// </summary>
        /// <param name="sid">指定的SID</param>
        private async Task<string?> RefreshServiceTokenAsync(string? sid = null)
        {
            var accountId = _serviceManager.GetCurrentAccountId();
            if (!string.IsNullOrEmpty(accountId))
            {
                // 清除当前账户的Token，强制重新获取
                _serviceManager.ClearServiceToken(accountId, sid);
            }

            // 使用EnsureServiceTokenAsync自动尝试所有账户
            return await EnsureServiceTokenAsync(sid);
        }

        /// <summary>
        /// 检查响应是否包含过期错误
        /// </summary>
        private void CheckTokenExpired(JObject response)
        {
            var code = response["code"]?.ToObject<long?>();
            if (code == ExpiredTokenCode)
            {
                var message = response["message"]?.ToString();
                Log.Warning($"Token过期: {message}");
                throw new TokenExpiredException("Token已过期，需要重新登录");
            }
        }

        /// <summary>
        /// 检查响应错误（合并Token过期和限流检查）
        /// </summary>
        /// <returns>如果是限流错误返回true，如果是Token过期抛出异常，否则返回false</returns>
        private bool CheckResponseError(JObject response, string accountId)
        {
            var code = response["code"]?.ToObject<long?>();
            if (code == null) return false;

            if (code == ExpiredTokenCode)
            {
                var message = response["message"]?.ToString();
                Log.Warning($"Token过期: {message}");
                throw new TokenExpiredException("Token已过期，需要重新登录");
            }

            if (code == RateLimitErrorCode)
            {
                var message = response["message"]?.ToString();
                Log.Warning($"操作太频繁，需要切换账户: {message}");
                _serviceManager.MarkAccountAsRateLimited(accountId, 24);
                Log.Information("账户操作太频繁，已标记为限流24小时，切换到下一个账户: AccountId={AccountId}", accountId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 查询三包和SN信息（支持自动切换账户重试）
        /// </summary>
        private async Task GetInsuranceInfoAsync(string? serviceTokenCookie, string? imei, PhoneInfo phoneInfo)
        {
            if (string.IsNullOrEmpty(imei))
            {
                return;
            }

            // 请求体是JSON数组格式，但Content-Type是application/x-www-form-urlencoded
            var requestBodyJson = JSON.Serialize(new object[]
            {
                new { },
                new { imei = HttpUtility.UrlEncode(imei) }
            });

            // 尝试所有可用账户（排除被限流的账户），直到成功或所有账户都失败
            var availableAccountIds = _serviceManager.GetAvailableAccountIds();
            if (availableAccountIds.Count == 0)
            {
                throw Oops.Oh("没有可用的账户（所有账户可能都被限流）");
            }

            Exception? lastException = null;

            foreach (var accountId in availableAccountIds)
            {
                try
                {
                    // 获取Token（内部会自动切换账户）
                    var token = await _serviceManager.GetServiceTokenCookieAsync(accountId);
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    var responseStr = await insuranceInfoUrl
                        .WithHeader("Cookie", $"serviceToken={token}")
                        .WithHeader("User-Agent", "okhttp/3.12.3")
                        .WithHeader("x-user-agent", "channel/mishop platform/mishop.android")
                        .WithHeader("Accept", "application/json")
                        .WithHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8")
                        .PostStringAsync(requestBodyJson)
                        .ReceiveString();

                    var response = JObject.Parse(responseStr);
                    
                    // 合并检查错误（Token过期和限流）
                    if (CheckResponseError(response, accountId))
                    {
                        continue; // 限流错误，尝试下一个账户
                    }

                    // 成功获取响应，解析数据
                    var data = response["data"]?["deviceInfo"];
                    if (data != null)
                    {
                        phoneInfo.IMEI1 = response["data"]["deviceInfo"]["imei"]?.ToString() ?? phoneInfo.IMEI1;
                        phoneInfo.GoodsPicture = response["data"]["deviceInfo"]["imgUrl"]?.ToString() ?? phoneInfo.GoodsPicture;
                        phoneInfo.RepairEndTime = response["data"]["deviceInfo"]["repairEndTime"]?.ToString() ?? phoneInfo.RepairEndTime;
                        phoneInfo.SN = response["data"]["deviceInfo"]["sn"]?.ToString() ?? phoneInfo.SN;
                    }

                    return; // 成功，返回
                }
                catch (TokenExpiredException)
                {
                    _serviceManager.ClearServiceToken(accountId);
                    continue;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Log.Warning("获取保险信息失败，尝试下一个账户: AccountId={AccountId}, Error={Error}", accountId, ex.Message);
                    continue;
                }
            }

            // 所有账户都失败了
            throw Oops.Oh($"所有账户都无法获取保险信息: {lastException?.Message ?? "未知错误"}");
        }

        /// <summary>
        /// 查询机型信息（支持自动切换账户重试）
        /// </summary>
        private async Task GetDeviceInfoAsync(string? serviceTokenCookie, string? imei, string? sn, PhoneInfo phoneInfo)
        {
            if (string.IsNullOrEmpty(imei) && string.IsNullOrEmpty(sn))
            {
                return;
            }

            // 请求体是JSON数组格式，但Content-Type是application/x-www-form-urlencoded
            var requestBodyJson = JSON.Serialize(new object[]
            {
                new { },
                new
                {
                    serviceType = "WX",
                    sn = sn ?? phoneInfo.SN ?? "",
                }
            });

            // 尝试所有可用账户（排除被限流的账户），直到成功或所有账户都失败
            var availableAccountIds = _serviceManager.GetAvailableAccountIds();
            if (availableAccountIds.Count == 0)
            {
                throw Oops.Oh("没有可用的账户（所有账户可能都被限流）");
            }

            Exception? lastException = null;

            foreach (var accountId in availableAccountIds)
            {
                try
                {
                    // 获取Token（内部会自动切换账户）
                    var token = await _serviceManager.GetServiceTokenCookieAsync(accountId);
                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    var responseStr = await deviceInfoUrl
                        .WithHeader("Cookie", $"serviceToken={token}")
                        .WithHeader("User-Agent", "okhttp/3.12.3")
                        .WithHeader("x-user-agent", "channel/mishop platform/mishop.android")
                        .WithHeader("Accept", "application/json")
                        .WithHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8")
                        .PostStringAsync(requestBodyJson)
                        .ReceiveString();

                    var response = JObject.Parse(responseStr);
                    
                    // 合并检查错误（Token过期和限流）
                    if (CheckResponseError(response, accountId))
                    {
                        continue; // 限流错误，尝试下一个账户
                    }

                    // 成功获取响应，解析数据
                    var data = response["data"];
                    if (data != null)
                    {
                        phoneInfo.Model = response["data"]["name"]?.ToString() ?? phoneInfo.Model;
                        phoneInfo.GoodsPicture = response["data"]["imgUrl"]?.ToString() ?? phoneInfo.GoodsPicture;
                        phoneInfo.IMEI1 = response["data"]["imei"]?.ToString() ?? phoneInfo.IMEI1;
                        phoneInfo.SN = response["data"]["sn"]?.ToString() ?? phoneInfo.SN;
                        if (string.IsNullOrEmpty(phoneInfo.RepairEndTime))
                        {
                            var supportInsurance = response["data"]["supportInsurance"]?.ToObject<bool>();
                            if (supportInsurance.HasValue)
                            {
                                phoneInfo.RepairEndTime = supportInsurance.Value ? "保内" : "保外";
                            }
                        }
                    }

                    return; // 成功，返回
                }
                catch (TokenExpiredException)
                {
                    _serviceManager.ClearServiceToken(accountId);
                    continue;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Log.Warning("获取设备信息失败，尝试下一个账户: AccountId={AccountId}, Error={Error}", accountId, ex.Message);
                    continue;
                }
            }

            // 所有账户都失败了
            throw Oops.Oh($"所有账户都无法获取设备信息: {lastException?.Message ?? "未知错误"}");
        }

        /// <summary>
        /// 查询激活日期
        /// </summary>
        private async Task GetActivationInfoAsync(string serviceTokenCookie, string? imei, string? sn, PhoneInfo phoneInfo)
        {
            if (string.IsNullOrEmpty(imei) && string.IsNullOrEmpty(sn))
            {
                return;
            }

            var requestBody = new Dictionary<string, string>
            {
                { "imei", imei ?? phoneInfo.IMEI1 ??"" },
                { "sn", sn ?? phoneInfo.SN ?? "" },
                { "webp", "1" }
            };

            var responseStr = await activationUrl
                .WithHeader("Cookie", $"serviceToken={serviceTokenCookie}")
                .WithHeader("User-Agent", "Mozilla/5.0 (Linux; Android 13; M2012K11AC Build/TKQ1.221114.001; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/116.0.0.0 Mobile Safari/537.36 XiaoMi/MiuiBrowser/4.3/Shop/5.16.0.20230955.b.1")
                .WithHeader("x-user-agent", "channel/mishop platform/mishop.m")
                .WithHeader("Referer", "https://m.mi.com/aftersale/security?headless=1%5Cu0026actionbarTitle%3D%E7%9C%9F%E4%BC%AA%E6%9F%A5%E8%AF%A2&spmref=MiShop_MA.17369.349514.1.38335706")
                .WithHeader("Content-Type", "application/x-www-form-urlencoded")
                .PostUrlEncodedAsync(requestBody)
                .ReceiveString();

            var response = JObject.Parse(responseStr);
            CheckTokenExpired(response);

            // 解析响应数据
            var data = response["data"];
            if (data != null)
            {
                phoneInfo.Model = response["data"]["goods_name"]?.ToString() ?? phoneInfo.Model;
                phoneInfo.ActivationTime = response["data"]["order_time"]?.ToString() ?? phoneInfo.ActivationTime;
            }
        }

        /// <summary>
        /// 查询查找设备状态（FindMyDevice Status）
        /// </summary>
        private async Task GetFindMyDeviceStatusAsync(string serviceTokenCookie, string? imei, PhoneInfo phoneInfo)
        {
            if (string.IsNullOrEmpty(imei))
            {
                return;
            }

            // 生成时间戳（毫秒）
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var responseStr = await findMyDeviceStatusUrl
                .SetQueryParams(new
                {
                    ts = timestamp,
                    id = HttpUtility.UrlEncode(imei)
                })
                .WithHeader("Referer", "https://i.mi.com/find/device/activationlock/?_locale=en#/")
                .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36")
                .WithHeader("Accept", "application/json")
                .GetAsync()
                .ReceiveString();

            var response = JObject.Parse(responseStr);
            CheckTokenExpired(response);

            // 解析响应数据
            var data = response["data"];
            if (data != null)
            {
                var locked = data["locked"]?.ToObject<bool?>();
                // locked为true表示ON，false表示OFF
                phoneInfo.FindMyDeviceStatus = locked == true ? "ON" : "OFF";
            }
        }

        public async Task<Dictionary<string, string>> GetCacheProducts()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "products.json");
            var json = await File.ReadAllTextAsync(path);
            return JSON.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        public async Task<Dictionary<string, string>> GetProducts()
        {
            var dict = new Dictionary<string, string>()
            {
                {"q", AESEncryption.Encrypt(miid,aesKey,Encoding.UTF8.GetBytes(aesIv),isBase64:true)},
                {"t" ,""},
                {"s","1" }
            };

            var responseStr = await productsUrl
                .SetQueryParams(dict)
                .WithHeader("User-Agent", userAgent)
                .GetAsync()
                .ReceiveString();
            var baseDto = JsonConvert.DeserializeObject<OtaBaseDto<List<ProductEntry>>>(responseStr);
            if (baseDto?.Code == 2000)
                return baseDto?.Data?.ToDictionary(it => it.DisplayName, it => it.Product) ?? new Dictionary<string, string>();
            throw Oops.Oh($"错误的响应代码: {baseDto?.Code}");
        }

        public async Task<List<RomEntry>> GetRecoveryRom(string product)
        {
            if (string.IsNullOrWhiteSpace(product)) throw Oops.Oh("产品代号不能为空");

            var dict = new Dictionary<string, string>()
            {
                {"q", AESEncryption.Encrypt(JSON.Serialize(new
                {
                devices = new[] { product },
                miid = miid,
                products = new[] { product }
                }),aesKey,Encoding.UTF8.GetBytes(aesIv),isBase64:true)},
                {"t" ,""},
                {"s","1" }
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            using var content = new FormUrlEncodedContent(dict);
            using var response = await httpClient.PostAsync(romUrl, content);
            response.EnsureSuccessStatusCode();
            var responseStr = await response.Content.ReadAsStringAsync();
            var baseDto = JsonConvert.DeserializeObject<OtaBaseDto<RomData>>(responseStr);
            if (baseDto?.Code == 2000)
                return baseDto.Data?.Recovery?.FirstOrDefault().Value ?? new List<RomEntry>();
            throw Oops.Oh($"错误的响应代码: {baseDto?.Code}");

        }

        /// <summary>
        /// 检查FRP（Factory Reset Protection）权限示例
        /// 使用UnlockRequests调用解锁相关API
        /// </summary>
        /// <returns>是否具有FRP权限</returns>
        public async Task<bool> CheckFrePermissionAsync()
        {
            try
            {
                var unlockRequests = new UnlockRequests(_serviceManager);
                var hasPermission = await unlockRequests.FrePermission();
                Log.Information("FRP权限检查完成: {HasPermission}", hasPermission);
                return hasPermission;
            }
            catch (Exception ex)
            {
                Log.Error("检查FRP权限时发生错误: {Error}", ex.Message);
                throw Oops.Oh($"检查FRP权限失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取设备解锁信息示例
        /// 使用UnlockRequests获取设备详细信息
        /// </summary>
        /// <param name="product">设备产品名称（如：alioth）</param>
        /// <param name="sn">设备序列号</param>
        /// <returns>设备信息JSON对象</returns>
        public async Task<JObject> GetUnlockDeviceInfoAsync(string product, string sn)
        {
            if (string.IsNullOrWhiteSpace(product))
            {
                throw Oops.Oh("产品名称不能为空");
            }

            if (string.IsNullOrWhiteSpace(sn))
            {
                throw Oops.Oh("序列号不能为空");
            }

            try
            {
                var unlockRequests = new UnlockRequests(_serviceManager);
                var deviceInfo = await unlockRequests.DeviceInfo(product, sn);
                Log.Information("获取设备解锁信息成功: Product={Product}, SN={SN}", product, sn);
                return deviceInfo;
            }
            catch (Exception ex)
            {
                Log.Error("获取设备解锁信息时发生错误: Product={Product}, SN={SN}, Error={Error}", 
                    product, sn, ex.Message);
                throw Oops.Oh("从Token获取IMEI失败");
            }
        }
    }
}

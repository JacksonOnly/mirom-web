using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace MiRomWeb.Application.System.Services
{
    /// <summary>
    /// 账户信息
    /// </summary>
    public class AccountInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string PassToken { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        
        /// <summary>
        /// 账户被限流的时间戳（操作太频繁时设置）
        /// </summary>
        public DateTime? RateLimitedUntil { get; set; }
    }

    /// <summary>
    /// ServiceToken信息
    /// </summary>
    public class ServiceTokenInfo
    {
        public CookieJar? CookieJar { get; set; }
        public string? CookieValue { get; set; }
        public string? Ssecurity { get; set; }
        public DateTime? ExpireTime { get; set; }
    }

    /// <summary>
    /// 小米服务管理器，用于获取和管理ServiceToken（支持多账户和多sid）
    /// </summary>
    public class MiServiceManager : IDisposable
    {
        private const string serviceLogin = "https://account.xiaomi.com/pass/serviceLogin";
        private const string serviceLogin2 = "https://account.xiaomi.com/pass/serviceLoginAuth2";
        private const string PublicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCYEVrK/4Mahiv0pUJgTybx4J9P5dUT/Y0PuwMbk+gMU+jrZnBiXGv6/hCH1avIhoBcE535F8nJQQN3UavZdFkYidsoXuEnat3+eVTp3FslyhRwIBDF09v4vDhRtxFOT+R7uH7h/mzmyA2/+lfIMWGIrffXprYizbV76+YQKhoqFQIDAQAB";

        private readonly RSA? _passwordEncryper;
        private readonly Dictionary<string, AccountInfo> _accounts = new();
        private readonly Dictionary<string, ServiceTokenInfo> _serviceTokens = new();
        private readonly object _lockObject = new object();
        private readonly ILogger<MiServiceManager> _logger;
        
        private string? _currentAccountId;
        private string _defaultSid = "eshopmobile";

        ~MiServiceManager()
        {
            Dispose();
        }

        public MiServiceManager(ILogger<MiServiceManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _passwordEncryper = RSA.Create();
            _passwordEncryper.KeySize = 1024;
            _passwordEncryper.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey), out _);
        }

        #region 账户管理

        /// <summary>
        /// 添加或更新账户
        /// </summary>
        /// <param name="accountId">账户ID（唯一标识，建议使用userId或自定义名称）</param>
        /// <param name="userId">用户ID</param>
        /// <param name="passToken">PassToken</param>
        /// <param name="displayName">显示名称（可选）</param>
        public void AddOrUpdateAccount(string accountId, string userId, string passToken, string? displayName = null)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(passToken))
            {
                throw new ArgumentException("accountId、userId和passToken不能为空");
            }

            lock (_lockObject)
            {
                _accounts[accountId] = new AccountInfo
                {
                    UserId = userId,
                    PassToken = passToken,
                    DisplayName = displayName ?? userId
                };

                // 如果这是第一个账户，设置为当前账户
                if (string.IsNullOrEmpty(_currentAccountId))
                {
                    _currentAccountId = accountId;
                }

                // 清除该账户的所有ServiceToken（需要重新获取）
                RemoveAccountTokens(accountId);
            }
        }

        /// <summary>
        /// 删除账户
        /// </summary>
        public bool RemoveAccount(string accountId)
        {
            lock (_lockObject)
            {
                if (!_accounts.ContainsKey(accountId))
                {
                    return false;
                }

                _accounts.Remove(accountId);
                RemoveAccountTokens(accountId);

                // 如果删除的是当前账户，切换到第一个可用账户
                if (_currentAccountId == accountId)
                {
                    _currentAccountId = _accounts.Keys.FirstOrDefault();
                }

                return true;
            }
        }

        /// <summary>
        /// 切换当前账户
        /// </summary>
        public bool SwitchAccount(string accountId)
        {
            // 先清理所有过期的限流标记
            ClearExpiredRateLimits();

            lock (_lockObject)
            {
                if (!_accounts.ContainsKey(accountId))
                {
                    return false;
                }

                // 检查账户是否被限流（IsAccountRateLimited内部也会清理该账户的过期标记）
                if (IsAccountRateLimited(accountId))
                {
                    _logger.LogWarning("尝试切换到被限流的账户: AccountId={AccountId}", accountId);
                    return false;
                }

                _currentAccountId = accountId;
                return true;
            }
        }

        /// <summary>
        /// 获取所有账户ID（包括被限流的账户）
        /// </summary>
        public List<string> GetAllAccountIds()
        {
            lock (_lockObject)
            {
                return _accounts.Keys.ToList();
            }
        }

        /// <summary>
        /// 获取可用的账户ID（排除被限流且在24小时内的账户）
        /// </summary>
        public List<string> GetAvailableAccountIds()
        {
            // 先清理所有过期的限流标记
            ClearExpiredRateLimits();

            lock (_lockObject)
            {
                var availableAccounts = new List<string>();
                
                foreach (var kvp in _accounts)
                {
                    var account = kvp.Value;
                    // 如果账户未被限流，添加到可用列表
                    if (account.RateLimitedUntil == null)
                    {
                        availableAccounts.Add(kvp.Key);
                    }
                }
                
                return availableAccounts;
            }
        }

        /// <summary>
        /// 获取账户信息
        /// </summary>
        public AccountInfo? GetAccountInfo(string? accountId = null)
        {
            lock (_lockObject)
            {
                accountId ??= _currentAccountId;
                return accountId != null && _accounts.TryGetValue(accountId, out var account) ? account : null;
            }
        }

        /// <summary>
        /// 获取当前账户ID
        /// </summary>
        public string? GetCurrentAccountId()
        {
            return _currentAccountId;
        }

        /// <summary>
        /// 设置默认SID
        /// </summary>
        public void SetDefaultSid(string sid)
        {
            _defaultSid = sid ?? "eshopmobile";
        }

        /// <summary>
        /// 获取默认SID
        /// </summary>
        public string GetDefaultSid()
        {
            return _defaultSid;
        }

        #endregion

        #region ServiceToken管理

        /// <summary>
        /// 清理所有过期的限流标记
        /// </summary>
        private void ClearExpiredRateLimits()
        {
            lock (_lockObject)
            {
                var now = DateTime.UtcNow;
                foreach (var kvp in _accounts)
                {
                    var account = kvp.Value;
                    if (account.RateLimitedUntil != null && account.RateLimitedUntil.Value <= now)
                    {
                        account.RateLimitedUntil = null;
                        _logger.LogInformation("账户限流已自动解除: AccountId={AccountId}", kvp.Key);
                    }
                }
            }
        }

        /// <summary>
        /// 获取ServiceToken的Cookie字符串值（懒加载）
        /// </summary>
        /// <param name="accountId">账户ID，如果为null则使用当前账户</param>
        /// <param name="sid">SID，如果为null则使用默认SID</param>
        public async Task<string?> GetServiceTokenCookieAsync(string? accountId = null, string? sid = null)
        {
            // 先清理所有过期的限流标记
            ClearExpiredRateLimits();

            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                throw new InvalidOperationException("没有可用的账户，请先添加账户");
            }

            // 检查账户是否被限流（IsAccountRateLimited内部也会清理该账户的过期标记）
            if (IsAccountRateLimited(accountId))
            {
                throw new InvalidOperationException($"账户 {accountId} 被限流，无法使用");
            }

            var key = GetTokenKey(accountId, sid);

            // 检查是否已有Token
            lock (_lockObject)
            {
                if (_serviceTokens.TryGetValue(key, out var tokenInfo) && 
                    !string.IsNullOrEmpty(tokenInfo.CookieValue))
                {
                    return tokenInfo.CookieValue;
                }
            }

            // 懒加载：获取Token（自动切换到指定账户）
            lock (_lockObject)
            {
                if (_currentAccountId != accountId)
                {
                    _currentAccountId = accountId;
                }
            }

            try
            {
                var token = await GetAuthTokenAsync(accountId, sid);
                if (token == null)
                {
                    MarkAccountAsInvalid(accountId, "获取ServiceToken失败，返回null");
                    return null;
                }

                lock (_lockObject)
                {
                    if (_serviceTokens.TryGetValue(key, out var tokenInfo))
                    {
                        return tokenInfo.CookieValue;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取ServiceToken时发生异常，账户: {AccountId}", accountId);
                MarkAccountAsInvalid(accountId, $"获取ServiceToken异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取ServiceToken的CookieJar（懒加载）
        /// </summary>
        /// <param name="accountId">账户ID，如果为null则使用当前账户</param>
        /// <param name="sid">SID，如果为null则使用默认SID</param>
        public async Task<CookieJar?> GetServiceTokenAsync(string? accountId = null, string? sid = null)
        {
            // 先清理所有过期的限流标记
            ClearExpiredRateLimits();

            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                throw new InvalidOperationException("没有可用的账户，请先添加账户");
            }

            var key = GetTokenKey(accountId, sid);

            // 检查是否已有Token
            lock (_lockObject)
            {
                if (_serviceTokens.TryGetValue(key, out var tokenInfo) && tokenInfo.CookieJar != null)
                {
                    return tokenInfo.CookieJar;
                }
            }

            // 懒加载：获取Token
            try
            {
                return await GetAuthTokenAsync(accountId, sid);
            }
            catch (Exception ex)
            {
                // 获取失败，标记账户无效
                _logger.LogError(ex, "获取ServiceToken CookieJar时发生异常，账户: {AccountId}", accountId);
                MarkAccountAsInvalid(accountId, $"获取ServiceToken异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取AesKey（Ssecurity）- 异步版本，如果不存在则自动获取
        /// </summary>
        /// <param name="accountId">账户ID，如果为null则使用当前账户</param>
        /// <param name="sid">SID，如果为null则使用默认SID</param>
        public async Task<string?> GetAesKeyAsync(string? accountId = null, string? sid = "unlockApi")
        {
            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                return null;
            }

            var key = GetTokenKey(accountId, sid);

            // 先检查是否已有Token
            lock (_lockObject)
            {
                if (_serviceTokens.TryGetValue(key, out var tokenInfo) && 
                    !string.IsNullOrEmpty(tokenInfo.Ssecurity))
                {
                    return tokenInfo.Ssecurity;
                }
            }

            // 如果没有Token，自动获取一次
            try
            {
                var token = await GetAuthTokenAsync(accountId, sid);
                if (token == null)
                {
                    _logger.LogError("自动获取Token失败，无法获取AesKey: AccountId={AccountId}, SID={Sid}", accountId, sid);
                    return null;
                }

                // 再次检查，获取Token后应该已经有了Ssecurity
                lock (_lockObject)
                {
                    if (_serviceTokens.TryGetValue(key, out var tokenInfo))
                    {
                        return tokenInfo.Ssecurity;
                    }
                }

                _logger.LogWarning("获取Token后仍无法找到AesKey: AccountId={AccountId}, SID={Sid}", accountId, sid);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "自动获取Token时发生异常: AccountId={AccountId}, SID={Sid}", accountId, sid);
                return null;
            }
        }

        /// <summary>
        /// 获取AesKey（Ssecurity）- 同步版本，如果不存在则返回null
        /// </summary>
        /// <param name="accountId">账户ID，如果为null则使用当前账户</param>
        /// <param name="sid">SID，如果为null则使用默认SID</param>
        public string? GetAesKey(string? accountId = null, string? sid = null)
        {
            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                return null;
            }

            var key = GetTokenKey(accountId, sid);
            lock (_lockObject)
            {
                if (_serviceTokens.TryGetValue(key, out var tokenInfo))
                {
                    return tokenInfo.Ssecurity;
                }
            }

            return null;
        }

        /// <summary>
        /// 通过SID查找已有可用的ServiceToken Cookie
        /// </summary>
        /// <param name="sid">SID</param>
        /// <returns>返回找到的账户ID和Cookie值，如果没有找到则返回null</returns>
        public (string AccountId, string CookieValue)? FindAvailableTokenBySid(string sid)
        {
            if (string.IsNullOrEmpty(sid))
            {
                sid = _defaultSid;
            }

            lock (_lockObject)
            {
                foreach (var accountId in _accounts.Keys)
                {
                    var key = GetTokenKey(accountId, sid);
                    if (_serviceTokens.TryGetValue(key, out var tokenInfo) && 
                        !string.IsNullOrEmpty(tokenInfo.CookieValue))
                    {
                        return (accountId, tokenInfo.CookieValue);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 通过UserId查找账户ID
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>账户ID，如果未找到则返回null</returns>
        public string? FindAccountIdByUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            lock (_lockObject)
            {
                foreach (var kvp in _accounts)
                {
                    if (kvp.Value.UserId == userId)
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定UserId的账户信息和Token信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="sid">SID，如果为null则使用默认SID</param>
        /// <returns>账户信息和Token信息，如果未找到则返回null</returns>
        public (AccountInfo Account, ServiceTokenInfo? TokenInfo)? GetAccountInfoByUserId(string userId, string? sid = null)
        {
            var accountId = FindAccountIdByUserId(userId);
            if (accountId == null)
            {
                return null;
            }

            var account = GetAccountInfo(accountId);
            if (account == null)
            {
                return null;
            }

            sid ??= _defaultSid;
            var key = GetTokenKey(accountId, sid);
            
            lock (_lockObject)
            {
                _serviceTokens.TryGetValue(key, out var tokenInfo);
                return (account, tokenInfo);
            }
        }
        
        /// <summary>
        /// 标记账户为操作太频繁（限流24小时）
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <param name="hours">限流时长（小时），默认24小时</param>
        public void MarkAccountAsRateLimited(string accountId, int hours = 24)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            lock (_lockObject)
            {
                if (!_accounts.ContainsKey(accountId))
                {
                    return;
                }

                var account = _accounts[accountId];
                var rateLimitedUntil = DateTime.UtcNow.AddHours(hours);
                account.RateLimitedUntil = rateLimitedUntil;

                // 清除该账户的所有Token
                RemoveAccountTokens(accountId);

                _logger.LogWarning("账户被标记为限流: AccountId={AccountId}, UserId={UserId}, 限流至={RateLimitedUntil}", 
                    accountId, account.UserId, rateLimitedUntil);

                // 如果当前账户被限流，切换到第一个可用账户
                if (_currentAccountId == accountId)
                {
                    var availableAccount = GetAvailableAccountIds().FirstOrDefault();
                    if (availableAccount != null)
                    {
                        _currentAccountId = availableAccount;
                        _logger.LogInformation("已切换到可用账户: {AccountId}", _currentAccountId);
                    }
                    else
                    {
                        _currentAccountId = null;
                        _logger.LogWarning("所有账户都被限流或无效");
                    }
                }
            }
        }

        /// <summary>
        /// 检查账户是否被限流
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <returns>如果账户被限流且在限流期内返回true，否则返回false</returns>
        public bool IsAccountRateLimited(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return false;
            }

            lock (_lockObject)
            {
                if (!_accounts.TryGetValue(accountId, out var account))
                {
                    return false;
                }

                if (account.RateLimitedUntil == null)
                {
                    return false;
                }

                // 如果限流时间已过，清除限流标记
                if (account.RateLimitedUntil.Value <= DateTime.UtcNow)
                {
                    account.RateLimitedUntil = null;
                    _logger.LogInformation("账户限流已解除: AccountId={AccountId}", accountId);
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// 标记账户为无效并删除
        /// </summary>
        /// <param name="accountId">账户ID</param>
        /// <param name="reason">原因</param>
        /// <returns>是否成功删除</returns>
        public bool MarkAccountAsInvalid(string accountId, string reason)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return false;
            }

            lock (_lockObject)
            {
                if (!_accounts.ContainsKey(accountId))
                {
                    return false;
                }

                var account = _accounts[accountId];
                _logger.LogWarning("账户无效，正在删除: AccountId={AccountId}, UserId={UserId}, 原因={Reason}", 
                    accountId, account.UserId, reason);

                _accounts.Remove(accountId);
                RemoveAccountTokens(accountId);

                // 如果删除的是当前账户，切换到第一个可用账户
                if (_currentAccountId == accountId)
                {
                    var availableAccount = GetAvailableAccountIds().FirstOrDefault();
                    if (availableAccount != null)
                    {
                        _currentAccountId = availableAccount;
                        _logger.LogInformation("已切换到账户: {AccountId}", _currentAccountId);
                    }
                    else
                    {
                        _currentAccountId = _accounts.Keys.FirstOrDefault();
                        if (_currentAccountId != null)
                        {
                            _logger.LogInformation("已切换到账户: {AccountId}", _currentAccountId);
                        }
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 清除指定账户和SID的ServiceToken（强制刷新）
        /// </summary>
        public void ClearServiceToken(string? accountId = null, string? sid = null)
        {
            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            var key = GetTokenKey(accountId, sid);
            lock (_lockObject)
            {
                _serviceTokens.Remove(key);
            }
        }

        /// <summary>
        /// 清除指定账户的所有ServiceToken
        /// </summary>
        public void ClearAccountTokens(string accountId)
        {
            RemoveAccountTokens(accountId);
        }

        /// <summary>
        /// 清除所有ServiceToken
        /// </summary>
        public void ClearAllTokens()
        {
            lock (_lockObject)
            {
                _serviceTokens.Clear();
            }
        }

        #endregion

        #region 内部方法

        /// <summary>
        /// 生成Token的Key（accountId:sid）
        /// </summary>
        private string GetTokenKey(string accountId, string sid)
        {
            return $"{accountId}:{sid}";
        }

        /// <summary>
        /// 移除账户的所有Token
        /// </summary>
        private void RemoveAccountTokens(string accountId)
        {
            var keysToRemove = _serviceTokens.Keys.Where(k => k.StartsWith($"{accountId}:")).ToList();
            foreach (var key in keysToRemove)
            {
                _serviceTokens.Remove(key);
            }
        }

        /// <summary>
        /// 获取AuthToken，如果location为空则通过userId和passToken获取
        /// </summary>
        public async Task<CookieJar?> GetAuthTokenAsync(string? accountId = null, string? sid = null, string location = "")
        {
            accountId ??= _currentAccountId;
            sid ??= _defaultSid;

            if (string.IsNullOrEmpty(accountId))
            {
                throw new InvalidOperationException("没有可用的账户，请先添加账户");
            }

            AccountInfo? account;
            lock (_lockObject)
            {
                if (!_accounts.TryGetValue(accountId, out account))
                {
                    throw new InvalidOperationException($"账户 {accountId} 不存在");
                }
            }

            var tokenInfo = new ServiceTokenInfo();
            tokenInfo.CookieJar = new CookieJar();

            if (string.IsNullOrEmpty(location))
            {
                if (string.IsNullOrEmpty(account.UserId) || string.IsNullOrEmpty(account.PassToken))
                {
                    _logger.LogError("缺少userId或passToken，无法获取ServiceToken");
                    return null;
                }

                object servceLoginQuery = new
                {
                    sid = sid,
                    _json = true,
                    passive = true,
                    hidden = false,
                    checkSafePhone = false
                };

                _logger.LogDebug("请求ServiceLogin: {ServiceLogin}, 参数: {@QueryParams}", serviceLogin, servceLoginQuery);

                var responseStr = await serviceLogin
                    .SetQueryParams(servceLoginQuery)
                    .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36 Edg/138.0.0.0")
                    .WithHeader("Cookie", $"userId={account.UserId};passToken={account.PassToken};deviceId={GetPcId()}")
                    .GetAsync()
                    .ReceiveString();

                _logger.LogDebug("ServiceLogin响应: {Response}", responseStr);

                var responseJobj = JObject.Parse(responseStr.Replace("&&&START&&&", ""));
                var statusCode = responseJobj["code"]!.ToObject<int>();
                if (statusCode != 0)
                {
                    _logger.LogError("ServiceLogin返回未知的响应代码: {StatusCode}", statusCode);
                    throw new Exception($"未知的响应代码: {statusCode}");
                }

                location = responseJobj["location"]!.ToString();
                tokenInfo.Ssecurity = responseJobj["ssecurity"]!.ToString();
            }

            CookieJar? cookieJar = null;

            Action<FlurlCall> onError = (call) =>
            {
                call.ExceptionHandled = true;
                _logger.LogError(call.Exception, "请求ServiceToken时出错: {Url}", call.Request.Url);
                cookieJar = null;
            };

            var response = await location
                .OnError(onError)
                .WithCookies(out cookieJar)
                .GetAsync();

            if (cookieJar == null)
            {
                _logger.LogError("获取ServiceToken失败：CookieJar为null");
                return null;
            }

            tokenInfo.CookieJar = cookieJar;

            // 从响应头中提取serviceToken的Cookie值
            try
            {
                if (response.Headers != null)
                {
                    var setCookieHeaders = response.Headers.Where(h => 
                        h.Name.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));
                    
                    foreach (var header in setCookieHeaders)
                    {
                        ExtractServiceTokenFromCookie(header.Value, tokenInfo);
                        if (!string.IsNullOrEmpty(tokenInfo.CookieValue))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "从响应头提取Cookie时出错");
            }

            if (string.IsNullOrEmpty(tokenInfo.CookieValue))
            {
                _logger.LogWarning("无法从响应头提取serviceToken，Cookie可能已由Flurl自动管理");
            }

            // 保存Token信息
            var key = GetTokenKey(accountId, sid);
            lock (_lockObject)
            {
                _serviceTokens[key] = tokenInfo;
            }

            _logger.LogInformation("成功获取ServiceToken，账户: {AccountId}, SID: {Sid}", accountId, sid);
            return tokenInfo.CookieJar;
        }

        /// <summary>
        /// 从Cookie字符串中提取serviceToken值
        /// </summary>
        private void ExtractServiceTokenFromCookie(string cookieHeader, ServiceTokenInfo tokenInfo)
        {
            if (string.IsNullOrEmpty(cookieHeader))
            {
                return;
            }

            var match = Regex.Match(cookieHeader, @"serviceToken=([^;]+)");
            if (match.Success)
            {
                tokenInfo.CookieValue = match.Groups[1].Value;
            }
        }

        #endregion

        #region 兼容性方法（向后兼容）

        /// <summary>
        /// 获取ServiceToken的Cookie字符串值（兼容旧API，使用当前账户和默认SID）
        /// </summary>
        [Obsolete("请使用 GetServiceTokenCookieAsync 方法")]
        public string? GetServiceTokenCookie()
        {
            return GetServiceTokenCookieAsync().Result;
        }

        /// <summary>
        /// 获取ServiceToken（兼容旧API，使用当前账户和默认SID）
        /// </summary>
        [Obsolete("请使用 GetServiceTokenAsync 方法")]
        public CookieJar? GetServiceToken()
        {
            return GetServiceTokenAsync().Result;
        }

        /// <summary>
        /// 设置SID（兼容旧API，设置默认SID）
        /// </summary>
        [Obsolete("请使用 SetDefaultSid 方法，或直接使用 GetServiceTokenCookieAsync 时指定 sid 参数")]
        public void SetSid(string sid)
        {
            SetDefaultSid(sid);
        }

        /// <summary>
        /// 设置用户ID（兼容旧API，添加到账户管理）
        /// </summary>
        [Obsolete("请使用 AddOrUpdateAccount 方法")]
        public void SetUserId(string userId)
        {
            if (string.IsNullOrEmpty(_currentAccountId))
            {
                AddOrUpdateAccount(userId, userId, "");
            }
            else
            {
                var account = GetAccountInfo(_currentAccountId);
                if (account != null)
                {
                    AddOrUpdateAccount(_currentAccountId, userId, account.PassToken, account.DisplayName);
                }
            }
        }

        /// <summary>
        /// 设置PassToken（兼容旧API，更新当前账户）
        /// </summary>
        [Obsolete("请使用 AddOrUpdateAccount 方法")]
        public void SetPassToken(string passToken)
        {
            if (string.IsNullOrEmpty(_currentAccountId))
            {
                throw new InvalidOperationException("请先设置UserId");
            }

            var account = GetAccountInfo(_currentAccountId);
            if (account != null)
            {
                AddOrUpdateAccount(_currentAccountId, account.UserId, passToken, account.DisplayName);
            }
        }

        /// <summary>
        /// 获取AuthToken（兼容旧API）
        /// </summary>
        [Obsolete("请使用 GetAuthTokenAsync 方法")]
        public async Task<CookieJar?> GetAuthToken(string location = "")
        {
            return await GetAuthTokenAsync(_currentAccountId, _defaultSid, location);
        }

        /// <summary>
        /// 获取AesKey（兼容旧API）
        /// </summary>
        [Obsolete("请使用 GetAesKey 方法的新版本")]
        public string? GetAesKey()
        {
            return GetAesKey(_currentAccountId, _defaultSid);
        }

        /// <summary>
        /// 获取用户ID（兼容旧API）
        /// </summary>
        [Obsolete("请使用 GetAccountInfo 方法")]
        public string? GetUserId()
        {
            return GetAccountInfo(_currentAccountId)?.UserId;
        }

        /// <summary>
        /// 获取PassToken（兼容旧API）
        /// </summary>
        [Obsolete("请使用 GetAccountInfo 方法")]
        public string? GetPassToken()
        {
            return GetAccountInfo(_currentAccountId)?.PassToken;
        }

        #endregion

        public string GetPcId()
        {
            var data = MD5.HashData(Encoding.UTF8.GetBytes($"wb_{Guid.NewGuid():N}"));
            return BitConverter.ToString(data).Replace("-", "").ToLower();
        }

        public void Dispose()
        {
            _passwordEncryper?.Dispose();
        }
    }
}

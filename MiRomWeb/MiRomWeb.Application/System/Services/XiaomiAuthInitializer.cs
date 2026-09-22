using System;
using Furion;
using MiRomWeb.Application.System.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace MiRomWeb.Application.System.Services
{
    /// <summary>
    /// 小米认证初始化服务
    /// </summary>
    public class XiaomiAuthInitializer : ITransient
    {
        private readonly MiServiceManager _serviceManager;
        private readonly XiaomiAuthOptions _options;
        private readonly ILogger<XiaomiAuthInitializer> _logger;

        public XiaomiAuthInitializer(MiServiceManager serviceManager, IOptions<XiaomiAuthOptions> options, ILogger<XiaomiAuthInitializer> logger)
        {
            _serviceManager = serviceManager;
            _options = options.Value;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 初始化认证信息（仅添加账户，不预加载Token）
        /// </summary>
        public void Initialize()
        {
            if (!_options.AutoInitializeOnStartup)
            {
                return;
            }

            // 设置默认SID
            if (!string.IsNullOrEmpty(_options.Sid))
            {
                _serviceManager.SetDefaultSid(_options.Sid);
            }

            // 优先使用账户列表配置
            if (_options.Accounts != null && _options.Accounts.Count > 0)
            {
                foreach (var account in _options.Accounts)
                {
                    if (string.IsNullOrEmpty(account.UserId) || string.IsNullOrEmpty(account.PassToken))
                    {
                        _logger.LogWarning("跳过无效账户配置：UserId或PassToken为空");
                        continue;
                    }

                    var accountId = account.UserId;
                    _serviceManager.AddOrUpdateAccount(accountId, account.UserId, account.PassToken, account.DisplayName);
                    _logger.LogInformation("已添加账户: {AccountId} (显示名称: {DisplayName})", accountId, account.DisplayName ?? accountId);
                }
            }
            // 兼容旧配置：单个账户
            else if (!string.IsNullOrEmpty(_options.UserId) && !string.IsNullOrEmpty(_options.PassToken))
            {
                var accountId = _options.UserId;
                _serviceManager.AddOrUpdateAccount(accountId, _options.UserId, _options.PassToken);
                _logger.LogInformation("已添加账户（兼容旧配置）: {AccountId}", accountId);
            }
            else
            {
                _logger.LogInformation("未配置小米账户；产品与 ROM 查询仍可使用。设备查询需要设置 XiaomiAuth 环境变量。");
                return;
            }

            _logger.LogInformation("XiaomiAuth初始化完成：已添加 {Count} 个账户（Token将在首次使用时自动获取）", 
                _serviceManager.GetAllAccountIds().Count);
        }

        /// <summary>
        /// 异步初始化认证信息（仅添加账户，不预加载Token）
        /// </summary>
        public async Task InitializeAsync()
        {
            // 由于现在不再预加载Token，异步方法只是调用同步方法
            Initialize();
            await Task.CompletedTask;
        }
    }
}

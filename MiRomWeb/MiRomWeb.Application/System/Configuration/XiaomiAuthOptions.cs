using System.Collections.Generic;

namespace MiRomWeb.Application.System.Configuration
{
    /// <summary>
    /// 账户配置项
    /// </summary>
    public class XiaomiAccountConfig
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// PassToken
        /// </summary>
        public string PassToken { get; set; } = string.Empty;

        /// <summary>
        /// 账户显示名称（可选）
        /// </summary>
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// 小米认证配置选项
    /// </summary>
    public class XiaomiAuthOptions
    {
        /// <summary>
        /// 用户ID（兼容旧配置）
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// PassToken（兼容旧配置）
        /// </summary>
        public string? PassToken { get; set; }

        /// <summary>
        /// 账户列表（支持多个账户）
        /// </summary>
        public List<XiaomiAccountConfig>? Accounts { get; set; }

        /// <summary>
        /// Service ID，默认为 eshopmobile
        /// </summary>
        public string Sid { get; set; } = "eshopmobile";

        /// <summary>
        /// 是否在启动时自动初始化
        /// </summary>
        public bool AutoInitializeOnStartup { get; set; } = true;
    }
}

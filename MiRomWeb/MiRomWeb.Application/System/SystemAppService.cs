using MiRomWeb.Application.System.Dtos;
using MiRomWeb.Application.System.Types;
using XiaomiLib;
using Microsoft.Extensions.Logging;

namespace MiRomWeb.Application
{
    /// <summary>
    /// 系统服务接口
    /// </summary>
    public class SystemAppService : IDynamicApiController
    {
        private readonly ISystemService _systemService;
        private readonly ILogger<SystemAppService> _logger;
        public SystemAppService(ISystemService systemService, ILogger<SystemAppService> logger)
        {
            _systemService = systemService;
            _logger = logger;
        }
        public async Task<Dictionary<string,string>> GetProducts()
        {
            return await _systemService.GetProducts();
        }
        public async Task<FullRomData> PostFullRom(string product)
        {
            var fullRom = await _systemService.GetFullRom(product);
            try
            {
                fullRom.Recovery = await _systemService.GetRecoveryRom(product);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Recovery ROM 查询失败，返回可用的 Fastboot 数据。Product={Product}", product);
            }
            return fullRom;
        }
        public async Task<PhoneInfo> GetPhoneInfo([FromQuery] string keyword)
        {
            return await _systemService.GetPhoneInfo(keyword);
        }

    }
}

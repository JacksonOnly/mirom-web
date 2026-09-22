using MiRomWeb.Application.System.Dtos;
using MiRomWeb.Application.System.Types;
using XiaomiLib;

namespace MiRomWeb.Application
{
    public interface ISystemService
    {
       Task<Dictionary<string,string>> GetProducts();
        Task<FullRomData> GetFullRom(string product);
        Task<PhoneInfo> GetPhoneInfo(string keyword);
        Task<List<RomEntry>> GetRecoveryRom(string product);
        
        /// <summary>
        /// 初始化ServiceToken管理器（设置userId和passToken）
        /// </summary>
        void InitializeServiceManager(string userId, string passToken, string? sid = null);
        
        /// <summary>
        /// 预加载ServiceToken（在启动时调用）
        /// </summary>
        Task PreloadServiceTokenAsync();
    }
}

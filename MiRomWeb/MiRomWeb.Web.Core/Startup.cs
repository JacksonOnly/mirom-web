using Furion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MiRomWeb.Application.System.Configuration;
using MiRomWeb.Application.System.Services;

namespace MiRomWeb.Web.Core
{
    public class Startup : AppStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddHttpClient();
            services.AddConsoleFormatter();

            // 配置小米认证选项
            var configuration = App.Configuration;
            services.Configure<XiaomiAuthOptions>(configuration.GetSection("XiaomiAuth"));

            // 注册MiServiceManager为单例，以便共享ServiceToken
            services.AddSingleton<MiServiceManager>();

            services.AddCors((cors) =>
            {
                cors.AddPolicy("AllowOrigins", policy =>
                {
                    policy.WithOrigins( "http://localhost:3000", "https://localhost:3000","https://mirom.kisopro.com") // 👈 明确列出可信源
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); 
                });
            });

            services.AddControllers()
                    .AddInjectWithUnifyResult();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("AllowOrigins");


            app.UseAuthentication();
            app.UseAuthorization();

            app.UseInject(string.Empty);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            // 初始化仅注册账户，实际 ServiceToken 仍按需获取。
            using var scope = app.ApplicationServices.CreateScope();
            scope.ServiceProvider.GetRequiredService<XiaomiAuthInitializer>().Initialize();
        }
    }
}

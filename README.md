# MiRomWeb

[中文](#中文) · [English](#english)

## 中文

MiRomWeb 是一个小米设备信息查询与 ROM 固件浏览工具。前端使用 Vue 3、TypeScript 和 Vite；后端使用 .NET 8 与 Furion。它提供 IMEI / 序列号设备信息查询、产品目录搜索，以及 Fastboot 线刷包和 Recovery 卡刷包的版本与下载地址展示。

> 本项目为非官方工具。设备信息和固件数据依赖上游服务，返回内容、可用性及下载地址可能变化。刷机前请自行核对设备代号、版本和校验值，并备份数据。

### 功能

- 通过 IMEI 或 SN 查询型号、激活、保修及查找设备状态（具体字段取决于上游响应）。
- 搜索产品名称或代号，按类型和版本筛选 ROM。
- 展示固件文件名、大小、系统版本、MD5 / SHA1 和下载链接。
- 支持移动端布局、键盘导航、加载 / 错误 / 空状态。

### 项目结构

| 路径 | 用途 |
| --- | --- |
| `Web/` | Vue 前端与 Vite 开发服务器 |
| `MiRomWeb/MiRomWeb.Web.Entry/` | ASP.NET Core 启动项目、产品数据、运行配置 |
| `MiRomWeb/MiRomWeb.Web.Core/` | 中间件、CORS 与服务注册 |
| `MiRomWeb/MiRomWeb.Application/` | API、ROM 与设备查询业务逻辑 |
| `MiRomWeb/MiRomWeb.Core/` | 公共辅助代码 |

### 环境要求

- .NET SDK 8.0
- Node.js 20.19+ 或 22.12+（Vite 7 要求）
- npm
- 如需设备信息查询：有效的小米账户 UserId 与 PassToken，并且账户具备相关上游接口权限

### 本地运行

在仓库根目录分别打开两个终端：

```powershell
dotnet restore .\MiRomWeb.sln
dotnet run --project .\MiRomWeb\MiRomWeb.Web.Entry --launch-profile MiRomWeb.Web.Entry
```

```powershell
cd .\Web
npm ci
npm run dev
```

前端默认访问 `http://localhost:3000`，Vite 将 `/api` 转发到 `http://127.0.0.1:5001`。若修改后端端口，请同步修改 `Web/vite.config.ts`。

### 账户配置与安全

小米账号只需配置在**后端**，前端无需登录。各功能的要求如下：

| 功能 | 是否需要添加账号 | 说明 |
| --- | --- | --- |
| 产品目录浏览、机型名称或代号搜索 | 不需要 | 使用项目随附的产品数据 |
| Fastboot / Recovery ROM 查询、筛选与下载链接查看 | 不需要 | 使用 ROM 上游接口 |
| 更新日志查看 | 不需要 | 页面内的静态记录 |
| IMEI / SN 设备信息查询 | 需要 | 后端必须配置有效的 Xiaomi UserId 与 PassToken，且账号拥有上游接口权限 |

仓库中的 `appsettings.json` 不包含真实账户凭据。仅在需要设备信息查询时，通过环境变量设置账号；PowerShell 示例：

```powershell
$env:XiaomiAuth__Accounts__0__UserId = 'your-user-id'
$env:XiaomiAuth__Accounts__0__PassToken = 'your-pass-token'
$env:XiaomiAuth__Accounts__0__DisplayName = 'main'
```

配置会在后端启动时读取。可继续设置 `Accounts__1` 等索引添加账户。不要将 PassToken 写入源码、提交记录、截图或日志。若旧版本配置曾包含真实凭据，请立即轮换；修改当前文件无法清除历史版本中的秘密。

### 构建与部署

```powershell
dotnet build .\MiRomWeb.sln
cd .\Web
npm run build
```

前端产物在 `Web/dist/`。生产环境将其部署为静态站点，并把 `/api` 反向代理到后端。后端可用 `dotnet publish .\MiRomWeb\MiRomWeb.Web.Entry -c Release -o .\publish` 发布；若启用设备信息查询，还需提供账户环境变量。部署时配置 HTTPS 和实际站点的 CORS 来源。当前 CORS 来源在 `MiRomWeb.Web.Core/Startup.cs` 中配置。

### API 概览

| 请求 | 说明 | 需要后端账号 |
| --- | --- | --- |
| `GET /api/system/products` | 产品代号和名称 | 否 |
| `POST /api/system/full-rom/{product}` | Fastboot 与 Recovery 固件 | 否 |
| `GET /api/system/phone-info?keyword=...` | 设备信息 | 是 |

接口由 Furion 统一包装，前端依据 `succeeded` 和 `data` 处理响应。ROM 上游接口或账户服务不可用时，应检查后端日志和上游状态。

### 常见问题

- **产品目录无法加载**：确认后端已启动，且 `products.json` 已随启动项目复制到输出目录。
- **设备查询失败**：确认账户环境变量、权限和上游服务状态；IMEI 应为 15 位数字。
- **开发代理报错**：确认后端监听 `5001`，并检查 `Web/vite.config.ts` 的代理目标。
- **下载地址不可用**：地址由上游接口返回，可能过期；请重新查询并核对官方来源。

## English

MiRomWeb is an unofficial Xiaomi device lookup and ROM browser. Its frontend uses Vue 3, TypeScript, and Vite; its backend uses .NET 8 and Furion. It supports IMEI / serial number lookup, product search, and Fastboot / Recovery firmware browsing.

> Upstream services control data availability and download links. Verify the exact device codename, firmware version, and checksums, and back up your data before flashing.

### Features

- Look up model, activation, warranty, and Find Device information when the upstream service provides it.
- Search devices by name or codename and filter firmware by type or version.
- View filename, size, system version, MD5 / SHA1, and download links.
- Responsive layout with keyboard access and explicit loading, error, and empty states.

### Requirements and quick start

Install .NET SDK 8, Node.js 20.19+ or 22.12+, and npm. From the repository root, run the backend and frontend in separate terminals:

```powershell
dotnet restore .\MiRomWeb.sln
dotnet run --project .\MiRomWeb\MiRomWeb.Web.Entry --launch-profile MiRomWeb.Web.Entry
```

```powershell
cd .\Web
npm ci
npm run dev
```

Open `http://localhost:3000`. Vite proxies `/api` to `http://127.0.0.1:5001` by default.

### Credentials

Only the **backend** needs Xiaomi credentials; visitors do not sign in on the frontend.

| Feature | Add an account? | Details |
| --- | --- | --- |
| Browse and search the product catalog | No | Uses the bundled product data |
| Query and filter Fastboot / Recovery ROMs and view download links | No | Uses the upstream ROM service |
| View the changelog | No | Static page content |
| Look up a device by IMEI or SN | Yes | Requires a valid Xiaomi UserId and PassToken with upstream API access |

Set credentials through environment variables before starting the backend only if device lookup is needed:

```powershell
$env:XiaomiAuth__Accounts__0__UserId = 'your-user-id'
$env:XiaomiAuth__Accounts__0__PassToken = 'your-pass-token'
$env:XiaomiAuth__Accounts__0__DisplayName = 'main'
```

Use `Accounts__1` and later indexes for additional accounts. Never commit tokens. Rotate any credentials that appeared in a previous version of the configuration; editing the current file does not remove them from history.

### Build and deployment

```powershell
dotnet build .\MiRomWeb.sln
cd .\Web
npm run build
```

Deploy `Web/dist/` as a static site and reverse proxy `/api` to the backend. Publish the API with `dotnet publish .\MiRomWeb\MiRomWeb.Web.Entry -c Release -o .\publish`. Supply account environment variables only when enabling device lookup. Update the allowed CORS origins in `MiRomWeb.Web.Core/Startup.cs` for your domain.

### API

| Request | Purpose | Backend account required |
| --- | --- | --- |
| `GET /api/system/products` | Product codenames and names | No |
| `POST /api/system/full-rom/{product}` | Fastboot and Recovery firmware | No |
| `GET /api/system/phone-info?keyword=...` | Device information | Yes |

The API uses Furion's unified response wrapper. If a query fails, check the backend logs, proxy target, account permissions, and upstream service availability.

# PCL1-CE

**Plain Craft Launcher 1 社区维护版**

原项目为 [LTCatt/PCL1](https://github.com/LTCatt/PCL1)，即 Minecraft 启动器 PCL2 的前身，
由龙腾猫跃（LTCatt）于 2017 年开发，采用 CC BY-SA 4.0 许可协议发布。

本仓库为第三方社区维护版本，非官方发布，与原作者的 PCL / PCL2 项目无隶属关系。

> 原作者在原版 README 中的说明：
> 「来自 2017 年的远古黑历史，代码质量不堪入目，时至今日毫无维护，Bug 一堆，
> 基本能跑不能用，就别喷了……」

---

## 项目背景

原版启动器已停止维护多年，其所依赖的多数网络服务均已停止运行。本项目旨在恢复其可用性，
并使其能够在较早期的操作系统上正常运行。

## 主要改动

### 一、微软账号登录

原版启动器使用的 Mojang 旧版认证服务（`authserver.mojang.com`）已于 2023 年停止服务，
该域名当前已无法解析。

本版本改用微软账号 OAuth 2.0 设备码流程（Device Code Flow）实现登录：启动器申请并显示
一个短代码，用户可在任意联网设备（例如手机）访问 <https://www.microsoft.com/link>
输入该代码完成授权。

采用设备码流程而非浏览器回调，是因为 Windows XP 自带的浏览器无法打开现代微软登录页面；
设备码流程可将浏览器环节完全移出本机。

完整的认证链路为：

```
设备码 → MSA 访问令牌 → Xbox Live → XSTS → Minecraft 服务 → 正版档案
```

### 二、纯托管 TLS 实现

Windows XP 的 schannel 默认仅支持 TLS 1.0，而微软登录、Xbox Live、Minecraft 服务以及
BMCLAPI 的 https 镜像均要求 TLS 1.2。此外，XP 的受信任根证书库自 2014 年起停止更新，
无法验证 DigiCert Global Root G2、Sectigo R46 等新签发的根证书。

为此新增 `ModTls.vb`，基于 BouncyCastle 实现纯托管 TLS，不依赖操作系统的 TLS 能力与证书
存储。网络请求会自动选择通道：优先使用系统 TLS，失败时切换至托管通道。

---

## 编译

需要 Visual Studio 2015 或更高版本，目标框架为 .NET Framework 4.0。

1. 使用 Visual Studio 打开 `Plain Craft Launcher.sln`
2. 选择 **Debug | x86** 配置，按 F5 生成并运行

生成结果位于解决方案根目录的 `Debug\` 目录（而非项目目录下的 `bin\Debug\`）。
`BouncyCastle.Crypto.dll` 必须与可执行文件位于同一目录。

`tools/` 目录中包含独立的诊断工具 `XPTLSProbe`，用于检测 TLS 能力、证书信任状况，以及
测试完整的微软登录链路。该工具附带 `build.bat`，可使用操作系统自带的 .NET 编译器构建，
无需安装 Visual Studio。

## 许可协议

本项目沿用原项目的 [CC BY-SA 4.0](http://creativecommons.org/licenses/by-sa/4.0/)
许可协议，协议全文见 [LICENSE](./LICENSE)。

- 原作者：龙腾猫跃（LTCatt）
- 原项目：<https://github.com/LTCatt/PCL1>

根据该许可协议的要求，任何衍生版本均须保留原作者署名，并以相同许可协议发布。

## 免责声明

本项目为第三方社区维护版本，与原作者及 Mojang、Microsoft 均无隶属关系。

本项目仅面向已购买 Minecraft: Java Edition 的用户。项目不包含任何破解、伪造访问令牌或
绕过服务端验证的机制。启动器保留的离线模式仅适用于本地单人游戏，该模式下无法加入正版
服务器，亦无法访问任何需要授权的在线服务。

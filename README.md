# PCL1-CE

**Plain Craft Launcher 1 社区维护版**

本项目基于 [LTCatt/PCL1](https://github.com/LTCatt/PCL1)（原作者：龙腾猫跃 / LTCatt，2017 年，
CC BY-SA 4.0）二次开发，为第三方社区维护版本，与原作者的 PCL / PCL2 项目无隶属关系。

原版已多年停止维护，其依赖的多数网络服务均已停止运行。本版本的目标是恢复可用性，
并使其能够在较早期的操作系统上正常运行。

---

## 特性

- 支持在 Windows XP 及更高版本上运行（.NET Framework 4.0）
- 使用微软账号登录，仅支持已拥有 Minecraft: Java Edition 的正版账号
- 内置纯托管 TLS 实现，不依赖操作系统的 TLS 能力与证书存储
- 支持 Minecraft 原版与 Forge / OptiFine / Fabric 等常见加载器的下载与安装
- 下载源支持官方源与镜像源，并可自动测速择优

## 登录

登录通过微软账号完成，完整的认证链路为：

```
OAuth 2.0 设备码 → 微软账号令牌 → Xbox Live → XSTS → Minecraft 服务 → 正版档案
```

启动器会显示一个短代码，用户在任意联网设备访问 <https://www.microsoft.com/link>
输入该代码即可完成授权，因此本机不需要能打开现代登录页面。

启动游戏前会校验该账号名下是否拥有 Minecraft: Java Edition。

## 编译

需要 Visual Studio 2015 或更高版本，目标框架为 .NET Framework 4.0。

1. 用 Visual Studio 打开 `Plain Craft Launcher.sln`
2. 选择 **Debug | x86**，按 F5 生成并运行

生成结果位于解决方案根目录的 `Debug\` 目录（而不是项目目录下的 `bin\Debug\`）。
`BouncyCastle.Crypto.dll` 需要与可执行文件位于同一目录。

## 目录

- `Plain Craft Launcher/` —— 启动器源码
- `Plain Craft Launcher.sln` —— 解决方案

运行时数据存放在可执行文件同级的 `PCL1_CE\` 目录中。

## 许可协议

本项目沿用原项目的 [CC BY-SA 4.0](http://creativecommons.org/licenses/by-sa/4.0/)
许可协议，协议全文见 [LICENSE](./LICENSE)。

- 原作者：龙腾猫跃（LTCatt）
- 原项目：<https://github.com/LTCatt/PCL1>

根据该许可协议的要求，任何衍生版本均须保留原作者署名，并以相同许可协议发布。

## 声明

本项目为第三方社区维护版本，与原作者及 Mojang、Microsoft 均无隶属关系。

Minecraft 是 Mojang Studios 的商标。本项目不是 Minecraft 官方产品，
未获得 Mojang Studios 或 Microsoft 的批准或关联。

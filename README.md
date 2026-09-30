# PCL1-CE

**Plain Craft Launcher 1 的社区维护版**（Community Edition）

原项目：[LTCatt/PCL1](https://github.com/LTCatt/PCL1) —— Minecraft 启动器 PCL2 的前身，
由 **龙腾猫跃（LTCatt）** 于 2017 年开发，采用 CC BY-SA 4.0 许可开源。

本仓库是**第三方社区衍生版本，非官方发布**，与原作者的 PCL / PCL2 无隶属关系。

> 原作者对原版的评价：「来自 2017 年的远古黑历史，代码质量不堪入目，
> 时至今日毫无维护，Bug 一堆，基本能跑不能用，就别喷了……」

---

## 这个版本做了什么

原版启动器停止维护多年，它依赖的服务大多已经下线。本项目的目标是让它重新可用，
重点面向**老旧系统，尤其是 Windows XP**。

### 1. 微软账号登录（替代已停服的 Mojang 认证）

原版使用的 `authserver.mojang.com` 已于 2023 年停止服务（该域名 DNS 现已悬空）。
本版本改用 **OAuth 2.0 设备码流程**：启动器显示一个短代码，用户在任意设备
（手机即可）打开 <https://www.microsoft.com/link> 输入即可完成授权。

选设备码而不是浏览器回调，正是为了绕开「Windows XP 打不开现代登录页」这个问题 ——
浏览器环节被完全移出本机。

完整链路：设备码 → MSA 令牌 → Xbox Live → XSTS → Minecraft 服务 → 正版档案。

### 2. 纯托管 TLS（绕开 Windows XP 的 schannel 限制）

Windows XP 的 schannel 默认只支持 TLS 1.0，而微软登录、Xbox Live、Minecraft 服务
以及 BMCLAPI 的 https 镜像全部要求 TLS 1.2；同时 XP 的受信任根证书库自 2014 年起
停止更新，无法验证 DigiCert Global Root G2、Sectigo R46 这类新根证书。

新增 `ModTls.vb`，使用 BouncyCastle 的**纯托管 TLS 实现**，完全绕开 schannel 与
系统证书库。请求会自动选择通道：优先走系统 TLS，失败则切换到托管通道。

### 3. 其它修复

- **修正正版令牌判断**：原逻辑用「令牌长度是否恰为 32」判断是否有正版登录，
  而微软的 JWT 长度数百，该条件恒为假，会把正版令牌丢掉换成离线 UUID
- **修正下载链路**：`My.Computer.Network.DownloadFile` 遇到 301 不会跟随重定向，
  而是把重定向页面本身当成文件保存（实测是一个 166 字节的 HTML），
  而 BMCLAPI 现在把所有文件请求都 301 到 https 镜像，导致资源文件全部下载失败
- **关闭已失效的服务**：首页推荐源（MCBBS 已关站）与自动更新检查
  （服务器已停止响应），启动耗时降到约 0.7 秒、启动日志零错误
- **字体回退**：13 个窗口加入 `Microsoft YaHei, SimHei, SimSun` 回退链
  （XP 默认不含微软雅黑）
- **修正若干处 `IndexOf` 未检查返回值**导致的失败（皮肤解析等）
- **登录入口可达性**：原版按钮状态机中「没有游戏版本」优先级高于「未登录」，
  导致版本列表为空时按钮显示为「下载游戏」、登录入口完全不可达

---

## 编译

需要 Visual Studio 2015 或更高版本（目标框架 .NET Framework 4.0）。

1. 用 VS 打开 `Plain Craft Launcher.sln`
2. 配置选 **Debug | x86**，按 F5

输出在**解决方案根目录**的 `Debug\`（不是项目下的 `bin\Debug\`）。
注意 `BouncyCastle.Crypto.dll` 必须与可执行文件放在同一目录。

`tools/` 目录下附带一个独立的诊断探针 `XPTLSProbe`，可在 Windows XP 上检测
TLS 能力、证书信任状况，并测试完整的微软登录链路。它自带 `build.bat`，
用系统自带的 .NET 编译器即可构建，不需要 Visual Studio。

---

## 许可

本项目沿用原项目的 **[CC BY-SA 4.0](http://creativecommons.org/licenses/by-sa/4.0/)** 许可。

- 原作者：**龙腾猫跃（LTCatt）**
- 原项目：<https://github.com/LTCatt/PCL1>

按该许可的要求，任何衍生版本都必须保留原作者署名，并以相同许可发布。

---

## 免责声明

本项目为第三方社区维护版本，与原作者及 Mojang / Microsoft 均无隶属关系。

仅面向已购买 Minecraft: Java Edition 的用户，不提供任何盗版、离线绕过或
规避正版验证的功能。

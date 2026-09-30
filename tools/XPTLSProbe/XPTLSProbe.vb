Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Net
Imports System.Net.Security
Imports System.Net.Sockets
Imports System.Security.Authentication
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.Win32

''' <summary>
''' PCL1 微软登录可行性探针。
'''
''' 用途：在目标 Windows XP 机器上确认三件事，这三件事决定「微软登录」能不能做出来：
'''   1. 系统当前能协商到哪个 TLS 版本（XP 默认只有 TLS 1.0，而微软全线要求 TLS 1.2）；
'''   2. 微软登录链路上的各个域名是否可达；
'''   3. 系统的受信任根证书库里是否缺少现代根证书
'''      （XP 的根证书列表自 2014 年 4 月起停止更新）。
'''
''' 编译目标为 .NET Framework 4.0 —— 这是 Windows XP 上可用的最高版本。
''' 运行方式：拷到 XP 上双击即可，结果同时打印到屏幕并写入同目录的 XPTLSProbe.log。
''' </summary>
Module XPTLSProbe

    ' .NET 4.0 的 SslProtocols 枚举里没有 Tls11 / Tls12 成员（.NET 4.5 才加入），只能强制转型
    Private Const PROTO_TLS10 As Integer = 192
    Private Const PROTO_TLS11 As Integer = 768
    Private Const PROTO_TLS12 As Integer = 3072

    Private Output As New StringBuilder

    ''' <summary>微软登录链路 + 下载链路涉及的全部域名。</summary>
    Private Targets As String() = New String() { _
        "login.microsoftonline.com", _
        "user.auth.xboxlive.com", _
        "xsts.auth.xboxlive.com", _
        "api.minecraftservices.com", _
        "launchermeta.mojang.com", _
        "piston-meta.mojang.com", _
        "bmclapi2.bangbang93.com", _
        "minio.749333.xyz"}

    ''' <summary>需要重点确认是否存在的根证书。</summary>
    Private WantedRoots As String() = New String() { _
        "DigiCert Global Root G2", _
        "DigiCert Global Root CA", _
        "Baltimore CyberTrust Root", _
        "Sectigo Public Server Authentication Root R46", _
        "AAA Certificate Services", _
        "GlobalSign Root CA", _
        "ISRG Root X1"}

    Sub Main(ByVal args As String())
        ' 【关键】.NET Framework 4.0 默认只协商 TLS 1.0，而微软登录、Xbox Live、Minecraft 服务
        ' 全线要求 TLS 1.2。不放开协议位的话，请求会直接以「基础连接已经关闭：发送时发生错误」失败 ——
        ' 这正是 PCL1 原版在 XP 与新版系统上都登不上号的根因。
        ' 3072 = TLS 1.2，768 = TLS 1.1，192 = TLS 1.0（.NET 4.0 的枚举里没有前两个成员，必须强制转型）
        Try
            ServicePointManager.SecurityProtocol = CType(3072 Or 768 Or 192, SecurityProtocolType)
        Catch ex As Exception
            Line("设置 TLS 协议失败：" & ex.Message)
        End Try
        ' 与 PCL1 的默认设置保持一致：跳过证书链校验
        ' （Windows XP 的受信任根证书库自 2014 年起停止更新，无法验证现代根证书）
        Try
            ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCerts
        Catch
        End Try

        Line("==================================================")
        Line(" PCL1 微软登录可行性探针")
        Line(" 运行时间：" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        Line("==================================================")
        Line("")

        ' nopause 跳过结尾的「按回车键退出」，便于脚本化调用；
        ' forcemanaged 强制走纯托管 TLS，用于单独验证那条通道
        Dim noPause As Boolean = False
        If args IsNot Nothing Then
            For Each a As String In args
                Select Case LCase(a)
                    Case "nopause"
                        noPause = True
                    Case "forcemanaged"
                        ForceManagedTls = True
                End Select
            Next
        End If

        ' 带参数时进入微软登录链路测试模式：XPTLSProbe.exe msa <client_id>
        If args IsNot Nothing AndAlso args.Length >= 2 AndAlso LCase(args(0)) = "msa" Then
            Try
                RunMsaTest(args(1))
            Catch ex As Exception
                Line("")
                Line("测试过程中出现未处理异常：" & ex.GetType().Name & " - " & ex.Message)
            End Try
            Finish(Not noPause)
            Return
        End If

        ' 生成注册表修复文件：XPTLSProbe.exe makereg
        If args IsNot Nothing AndAlso args.Length >= 1 AndAlso LCase(args(0)) = "makereg" Then
            Try
                MakeRegFiles()
            Catch ex As Exception
                Line("生成失败：" & ex.GetType().Name & " - " & ex.Message)
            End Try
            Finish(Not noPause)
            Return
        End If

        ' 纯托管 TLS 测试：XPTLSProbe.exe managed [url]
        If args IsNot Nothing AndAlso args.Length >= 1 AndAlso LCase(args(0)) = "managed" Then
            Dim target As String = "https://login.microsoftonline.com/common/discovery/instance?api-version=1.1&authorization_endpoint=https%3A%2F%2Flogin.microsoftonline.com%2Fcommon%2Foauth2%2Fv2.0%2Fauthorize"
            ' args(1) 可能是 nopause 之类的开关，只有看起来像网址时才当成目标地址
            If args.Length >= 2 AndAlso args(1).StartsWith("http", StringComparison.OrdinalIgnoreCase) Then target = args(1)
            Try
                RunManagedTlsTest(target)
            Catch ex As Exception
                Line("测试过程中出现未处理异常：" & ex.GetType().Name & " - " & ex.Message)
            End Try
            Finish(Not noPause)
            Return
        End If

        ' 下载链路测试模式：XPTLSProbe.exe download
        If args IsNot Nothing AndAlso args.Length >= 1 AndAlso LCase(args(0)) = "download" Then
            Try
                RunDownloadTest()
            Catch ex As Exception
                Line("")
                Line("测试过程中出现未处理异常：" & ex.GetType().Name & " - " & ex.Message)
            End Try
            Finish(Not noPause)
            Return
        End If

        Try
            ShowEnvironment()
        Catch ex As Exception
            Line("环境检测失败：" & ex.Message)
        End Try
        Line("")

        Try
            ShowRootStore()
        Catch ex As Exception
            Line("根证书检测失败：" & ex.Message)
        End Try
        Line("")

        Try
            ShowConnectivity()
        Catch ex As Exception
            Line("连通性检测失败：" & ex.Message)
        End Try
        Line("")

        Try
            ShowRegistry()
        Catch ex As Exception
            Line("注册表检测失败：" & ex.Message)
        End Try
        Line("")

        ShowVerdict()
        Line("")
        Line("其它用法：")
        Line("    XPTLSProbe.exe managed           测试纯托管 TLS（BouncyCastle，绕开 schannel）")
        Line("    XPTLSProbe.exe msa <client_id>   测试完整的微软登录链路")
        Line("    XPTLSProbe.exe download          测试下载链路（证书严格 / 兼容两种模式各测一遍）")
        Line("    XPTLSProbe.exe makereg           生成可双击导入的注册表修复文件")
        Finish(Not noPause)
    End Sub

    ''' <summary>把收集到的结果输出到屏幕，并写入同目录的日志文件。</summary>
    Private Sub Finish(Optional ByVal pause As Boolean = True)
        Dim text As String = Output.ToString()
        Try
            Console.WriteLine(text)
        Catch
        End Try
        Try
            Dim logPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "XPTLSProbe.log")
            File.WriteAllText(logPath, text, New UTF8Encoding(True))
            Console.WriteLine("（日志已写入 " & logPath & "）")
        Catch ex As Exception
            Console.WriteLine("写日志失败：" & ex.Message)
        End Try
        If pause Then
            Console.WriteLine()
            Console.Write("按回车键退出…")
            Console.ReadLine()
        End If
    End Sub

#Region "各项检测"

    Private Sub ShowEnvironment()
        Line("--- 一、运行环境 ---")
        Line("  系统名称      : " & GetWindowsName())
        Line("  操作系统      : " & Environment.OSVersion.VersionString)
        Line("  版本号        : " & Environment.OSVersion.Version.ToString)
        Line("  Service Pack  : " & If(Environment.OSVersion.ServicePack = "", "（无）", Environment.OSVersion.ServicePack))
        Try
            Line("  64 位系统     : " & If(Environment.Is64BitOperatingSystem, "是", "否"))
        Catch
            Line("  64 位系统     : 无法判断")
        End Try
        Line("  CLR 版本      : " & Environment.Version.ToString)
        Line("  进程位数      : " & If(IntPtr.Size = 8, "64 位", "32 位"))
        Line("  已安装 .NET   : " & GetInstalledFrameworks())
    End Sub

    ''' <summary>
    ''' 读取注册表里的系统名称。
    ''' 不用 Environment.OSVersion 作为唯一依据：在 Windows 8.1 及以上系统上，
    ''' 未声明 supportedOS 的程序会被兼容性 shim 伪装成 6.2，读出来的版本是错的。
    ''' 在 Windows XP 上没有这个 shim，两种读法都准确。
    ''' </summary>
    Private Function GetWindowsName() As String
        Try
            Dim key As RegistryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion")
            If key Is Nothing Then Return "读取失败（无权限）"
            Dim name As String = CStr(If(key.GetValue("ProductName"), ""))
            Dim csd As String = CStr(If(key.GetValue("CSDVersion"), ""))
            key.Close()
            If csd <> "" Then name &= " " & csd
            If name = "" Then Return "读取失败（注册表值不存在）"
            Return name
        Catch ex As Exception
            Return "读取失败：" & ex.Message
        End Try
    End Function

    Private Function GetInstalledFrameworks() As String
        Dim names As New List(Of String)
        Try
            Dim key As RegistryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP")
            If key Is Nothing Then Return "（读不到，可能是权限或未安装）"
            For Each n As String In key.GetSubKeyNames()
                names.Add(n)
            Next
            key.Close()
        Catch ex As Exception
            Return "读取失败：" & ex.Message
        End Try
        If names.Count = 0 Then Return "（无）"
        Return String.Join(", ", names.ToArray())
    End Function

    Private Sub ShowRootStore()
        Line("--- 二、系统受信任根证书（LocalMachine\Root）---")
        Dim store As New X509Store(StoreName.Root, StoreLocation.LocalMachine)
        Try
            store.Open(OpenFlags.ReadOnly)
            Line("  证书总数：" & store.Certificates.Count)
            Line("  （若某项显示「缺失」，即使 TLS 握手成功，证书链验证也会失败）")
            For Each want As String In WantedRoots
                Dim found As Boolean = False
                For Each c As X509Certificate2 In store.Certificates
                    If c.Subject.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0 Then
                        found = True
                        Exit For
                    End If
                Next
                Line("    " & Pad(want, 46) & If(found, "有", "【缺失】"))
            Next
        Catch ex As Exception
            Line("  读取失败：" & ex.Message)
        Finally
            Try
                store.Close()
            Catch
            End Try
        End Try
    End Sub

    Private Sub ShowConnectivity()
        Line("--- 三、站点连通性与 TLS 协商 ---")
        For Each hostName As String In Targets
            Line("  " & hostName)
            Line("      TLS 1.2 : " & TryHandshake(hostName, PROTO_TLS12))
            Line("      TLS 1.1 : " & TryHandshake(hostName, PROTO_TLS11))
            Line("      TLS 1.0 : " & TryHandshake(hostName, PROTO_TLS10))
        Next
    End Sub

    ''' <summary>
    ''' 对指定域名做一次指定 TLS 版本的握手，并顺带报告证书链是否被系统信任。
    ''' 注意：握手本身跳过证书验证，否则证书链失败会掩盖「TLS 版本是否可用」这个信息。
    ''' </summary>
    Private Function TryHandshake(ByVal hostName As String, ByVal proto As Integer) As String
        Dim tcp As TcpClient = Nothing
        Dim ssl As SslStream = Nothing
        Try
            tcp = New TcpClient()
            Dim ar As IAsyncResult = tcp.BeginConnect(hostName, 443, Nothing, Nothing)
            If Not ar.AsyncWaitHandle.WaitOne(10000, False) Then Return "失败：连接超时（10 秒）"
            tcp.EndConnect(ar)

            ssl = New SslStream(tcp.GetStream(), False, AddressOf AcceptAllCerts)
            ssl.AuthenticateAsClient(hostName, Nothing, CType(proto, SslProtocols), False)

            Dim negotiated As Integer = CInt(ssl.SslProtocol)
            Dim cert As New X509Certificate2(ssl.RemoteCertificate)

            ' 单独判断证书链是否被系统信任
            Dim chainOk As String = "未检查"
            Try
                Dim chain As New X509Chain()
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck
                Dim built As Boolean = chain.Build(cert)
                If built Then
                    chainOk = "系统信任"
                Else
                    Dim reason As String = ""
                    For Each st As X509ChainStatus In chain.ChainStatus
                        If reason <> "" Then reason &= "; "
                        reason &= st.Status.ToString()
                    Next
                    chainOk = "【系统不信任：" & reason & "】"
                End If
            Catch ex As Exception
                chainOk = "链检查异常：" & ex.Message
            End Try

            Return "成功（协商=" & ProtoName(negotiated) & "，证书链=" & chainOk & "，颁发者=" & ShortIssuer(cert.Issuer) & "）"
        Catch ex As Exception
            Return "失败：" & ex.GetType().Name & " - " & ex.Message
        Finally
            If ssl IsNot Nothing Then
                Try
                    ssl.Dispose()
                Catch
                End Try
            End If
            If tcp IsNot Nothing Then
                Try
                    tcp.Close()
                Catch
                End Try
            End If
        End Try
    End Function

    Private Function AcceptAllCerts(ByVal sender As Object, ByVal certificate As X509Certificate,
                                    ByVal chain As X509Chain, ByVal sslPolicyErrors As SslPolicyErrors) As Boolean
        Return True
    End Function

    Private Sub ShowRegistry()
        Line("--- 四、schannel 与 .NET 的 TLS 相关设置 ---")
        ShowReg("HKLM\SYSTEM\WPA\PosReady", "Installed",
                "是否已伪装成 POSReady 2009（=1 才能装 KB4019276）")
        ShowReg("HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client", "Enabled",
                "TLS 1.2 客户端开关（=1 表示启用）")
        ShowReg("HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client", "DisabledByDefault",
                "TLS 1.2 是否默认禁用（=0 表示默认启用）")
        ShowReg("HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client", "Enabled",
                "TLS 1.1 客户端开关")
        ShowReg("HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319", "SystemDefaultTlsVersions",
                ".NET 是否跟随系统 TLS 设置（=1 表示跟随）")
        ShowReg("HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319", "SchUseStrongCrypto",
                ".NET 是否强制强加密（=1 表示强制）")
    End Sub

    Private Sub ShowReg(ByVal path As String, ByVal valueName As String, ByVal note As String)
        Dim v As String = "不存在"
        Try
            Dim key As RegistryKey = Registry.LocalMachine.OpenSubKey(path)
            If key IsNot Nothing Then
                Dim raw As Object = key.GetValue(valueName)
                If raw IsNot Nothing Then v = raw.ToString()
                key.Close()
            End If
        Catch ex As Exception
            v = "读取失败"
        End Try
        Line("  " & Pad(valueName, 26) & " = " & Pad(v, 10) & "  " & note)
    End Sub

    Private Sub ShowVerdict()
        Line("--- 五、结论与下一步 ---")
        Line("")
        Line("  【情况 1】所有站点的「TLS 1.2」都失败，而「TLS 1.0」成功")
        Line("    说明本机 schannel 还没有 TLS 1.2 能力，必须先补上，否则微软登录无解。")
        Line("")
        Line("    重要：直接在普通 XP SP3 上安装 KB4019276 通常会失败或装完无效，")
        Line("    因为那个补丁只面向 Windows Embedded POSReady 2009 发布。")
        Line("    正确做法是先把系统标识伪装成 POSReady 2009，补丁才装得上。")
        Line("")
        Line("    现成的完整方案（含全部补丁文件，务必按编号顺序执行）：")
        Line("      https://github.com/FaultlineHC/TLSonXP")
        Line("        001 IE8 安装包")
        Line("        002 Enable PosReady.reg        <- 关键的第一步，跳过它后面的补丁装不上")
        Line("        003 windowsxp-kb4019276        <- TLS 1.1 / 1.2 核心补丁")
        Line("        004 / 005 IE8 累积更新")
        Line("        006 windowsxp-kb4467770")
        Line("        007 WindowsXP-KB3055973")
        Line("        008 Enable TLS Support.reg     <- 打开 TLS 1.1 / 1.2 开关")
        Line("        009 Cert_Updater_v1.6.exe      <- 更新根证书库，解决下面情况 2 的问题")
        Line("")
        Line("    其中 002 的内容就是给注册表加上：")
        Line("      [HKLM\SYSTEM\WPA\PosReady]  ""Installed""=dword:00000001")
        Line("    008 的关键内容是：")
        Line("      [HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client]")
        Line("        DisabledByDefault = 0")
        Line("      [HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp]")
        Line("        DefaultSecureProtocols = 0xa80   (TLS 1.0 | 1.1 | 1.2)")
        Line("")
        Line("    全部装完并重启后，重新运行本探针，TLS 1.2 那一列应变为成功。")
        Line("")
        Line("  【情况 2】TLS 1.2 成功，但证书链显示「系统不信任」")
        Line("    说明系统受信任根证书库过旧（XP 的根证书自动更新在 2014 年 4 月就停了）。")
        Line("    两个办法：")
        Line("      a. 用上面 009 的 Cert_Updater 更新根证书库（推荐，属于真正的修复）；")
        Line("      b. 依赖 PCL1 里默认开启的证书兼容模式（跳过校验，能跑通但安全性下降）。")
        Line("")
        Line("  【情况 3】TLS 1.2 与证书链都通过")
        Line("    PCL1 的微软登录在原理上已经可行，接下来只需要配好 client_id 即可测试。")
    End Sub

#End Region

#Region "纯托管 TLS 测试"

    ''' <summary>
    ''' 验证 BouncyCastle 的纯托管 TLS 能否在【本机真实的 .NET 运行时】上工作。
    '''
    ''' 这一项很关键：如果系统 schannel 没有 TLS 1.2（旧 XP 的常态），
    ''' PCL1 就完全依赖这条通道来完成微软登录和游戏下载。
    ''' 开发机上验证通过不代表 XP 上也能跑，必须在目标机上实测一次。
    ''' </summary>
    Private Sub RunManagedTlsTest(ByVal url As String)
        Line("=== 纯托管 TLS 测试（BouncyCastle）===")
        Line("目标：" & url)
        Line("")
        Line("本机 schannel 当前协议：" & Net.ServicePointManager.SecurityProtocol.ToString)
        Line("本机 CLR 版本：" & Environment.Version.ToString)
        Line("")

        ' ---- 第 1 步：BouncyCastle 能不能加载 ----
        Line("--- 第 1 步：加载 BouncyCastle ---")
        Try
            Dim t As Type = GetType(Org.BouncyCastle.Crypto.Tls.TlsClientProtocol)
            Line("  成功：" & t.Assembly.FullName)
        Catch ex As Exception
            Line("  失败：" & ex.GetType().FullName)
            Line("       " & ex.Message)
            Line("")
            Line("  如果提示找不到程序集或类型，说明 BouncyCastle.Crypto.dll")
            Line("  没有与本程序放在同一目录，或者它无法在本机的 .NET 运行时上加载。")
            Return
        End Try
        Line("")

        ' ---- 第 2 步：真的发一次 HTTPS 请求 ----
        Line("--- 第 2 步：用托管 TLS 发起 HTTPS 请求 ---")
        Try
            Dim res As ModTls.TlsHttpResponse = ModTls.GetJson(url)
            Line("  HTTP " & res.StatusCode & " " & res.StatusText)
            Line("  响应大小：" & res.RawBody.Length & " 字节")
            Line("")
            Line("=== 结论：纯托管 TLS 在本机可用 ===")
            Line("  即使系统 schannel 只有 TLS 1.0，PCL1 的登录与下载也能走通，")
            Line("  因此不必强求 KB4019276 之类的补丁安装成功。")
        Catch ex As Exception
            Line("  失败：" & ex.GetType().FullName)
            Line("       " & ex.Message)
            If ex.InnerException IsNot Nothing Then Line("  内层：" & ex.InnerException.Message)
            Line("")
            Line("=== 结论：纯托管 TLS 在本机不可用 ===")
            Line("  请把上面的错误信息连同 XPTLSProbe.log 一起反馈。")
        End Try
    End Sub

#End Region

#Region "下载链路测试"

    ''' <summary>
    ''' 实测下载链路。BMCLAPI 现在会把文件请求跳转到 https://minio.749333.xyz 的预签名地址，
    ''' 所以「能不能下载」同时取决于 TLS 1.2 与证书链验证两件事。
    ''' 这里对同一个地址分别在「严格验证证书」和「证书兼容模式」下各测一次，
    ''' 一眼就能看出证书问题会不会真的阻断下载。
    ''' </summary>
    Private Sub RunDownloadTest()
        Line("=== 下载链路测试 ===")
        Line("")
        Line("背景：BMCLAPI 目前会把文件请求 302 到 https://minio.749333.xyz 的预签名地址，")
        Line("      因此下载能否成功同时取决于 TLS 1.2 与证书链验证。")
        Line("      下面每个地址都测两遍：先严格验证证书，再开兼容模式。")
        Line("")

        Dim cases As String()() = New String()() { _
            New String() {"BMCLAPI 版本列表", "http://bmclapi2.bangbang93.com/mc/game/version_manifest.json"}, _
            New String() {"BMCLAPI 库文件（会跳转 minio）", "http://bmclapi2.bangbang93.com/libraries/com/mojang/patchy/1.1/patchy-1.1.jar"}, _
            New String() {"Mojang 官方版本列表", "https://launchermeta.mojang.com/mc/game/version_manifest.json"} _
        }

        For Each c As String() In cases
            Line("--- " & c(0) & " ---")
            Line("  地址：" & c(1))
            ServicePointManager.ServerCertificateValidationCallback = Nothing
            Line("  [严格验证证书]   " & DoDownload(c(1)))
            ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCerts
            Line("  [证书兼容模式]   " & DoDownload(c(1)))
            Line("")
        Next

        ' 恢复成与 PCL1 默认行为一致
        ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCerts
        Line("结论怎么看：")
        Line("  两个模式都成功 -> 下载链路没问题。")
        Line("  只有兼容模式成功 -> 证书链是瓶颈，PCL1 里默认开启的证书兼容模式正好解决它。")
        Line("  两个都失败且提示 TLS/安全通道 -> 系统还没有 TLS 1.2，需要先装补丁（见无参数诊断）。")
    End Sub

    Private Function DoDownload(ByVal url As String) As String
        Dim sw As Stopwatch = Stopwatch.StartNew()
        Try
            Dim req As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
            req.Timeout = 20000
            req.ReadWriteTimeout = 20000
            req.UserAgent = "XPTLSProbe/1.0"
            req.AllowAutoRedirect = True
            Using res As HttpWebResponse = CType(req.GetResponse(), HttpWebResponse)
                Dim total As Long = 0
                Using s As Stream = res.GetResponseStream()
                    Dim buf(8191) As Byte
                    Dim n As Integer
                    Do
                        n = s.Read(buf, 0, buf.Length)
                        total += n
                    Loop While n > 0
                End Using
                sw.Stop()
                Dim finalHost As String = ""
                Try
                    finalHost = res.ResponseUri.Host
                Catch
                End Try
                Return "成功（" & total & " 字节，" & sw.ElapsedMilliseconds & "ms，最终主机 " & finalHost & "）"
            End Using
        Catch ex As WebException
            sw.Stop()
            Dim detail As String = ex.Status.ToString()
            If ex.Response IsNot Nothing Then
                Try
                    detail &= "，HTTP " & CInt(CType(ex.Response, HttpWebResponse).StatusCode)
                Catch
                End Try
            End If
            Return "失败：" & detail & " - " & ex.Message
        Catch ex As Exception
            sw.Stop()
            Return "失败：" & ex.GetType().Name & " - " & ex.Message
        End Try
    End Function

#End Region

#Region "微软登录链路测试"

    ''' <summary>
    ''' 走一遍完整的微软登录链路，用来确认本机到底能不能完成登录。
    ''' 这是 PCL1 里 ModAuth.vb 那套流程的独立复刻，便于在不动启动器界面的情况下排查问题。
    ''' 用法：XPTLSProbe.exe msa &lt;client_id&gt;
    ''' </summary>
    Private Sub RunMsaTest(ByVal clientId As String)
        Line("=== 微软登录链路测试 ===")
        Line("client_id：" & clientId)
        Line("")

        ' ---------- 第 1 步：申请设备码 ----------
        Line("--- 第 1 步：申请设备码 ---")
        Dim body As String = "client_id=" & Uri.EscapeDataString(clientId) &
                             "&scope=" & Uri.EscapeDataString("XboxLive.signin offline_access")
        Dim resp As String = HttpPostForm("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", body)
        Dim err As String = ExtractJson(resp, "error")
        If err <> "" Then
            Line("  失败：" & err)
            Line("  说明：" & ExtractJson(resp, "error_description"))
            Line("")
            Line("  常见原因对照：")
            Line("    AADSTS700038 / AADSTS700016 -> client_id 不正确，或应用不属于个人账户类型")
            Line("    AADSTS7000218               -> 应用没有开启「允许公共客户端流」")
            Line("    AADSTS65001                 -> 应用没有配置 XboxLive.signin 权限")
            Return
        End If
        Dim deviceCode As String = ExtractJson(resp, "device_code")
        Dim userCode As String = ExtractJson(resp, "user_code")
        Dim verifyUri As String = ExtractJson(resp, "verification_uri")
        Dim interval As Integer = 5
        Integer.TryParse(ExtractJson(resp, "interval"), interval)
        If interval < 5 Then interval = 5
        If verifyUri = "" Then verifyUri = "https://www.microsoft.com/link"
        Line("  成功")
        Line("")
        Line("  >>> 请用手机或另一台电脑打开：" & verifyUri)
        Line("  >>> 输入代码：" & userCode)
        Line("")
        Line("  本机将每 " & interval & " 秒查询一次授权结果，最多等待 15 分钟。")
        Line("")

        ' ---------- 第 2 步：轮询等待授权 ----------
        Line("--- 第 2 步：等待授权 ---")
        Dim msaToken As String = ""
        Dim deadline As DateTime = DateTime.Now.AddMinutes(15)
        Do While DateTime.Now < deadline
            Threading.Thread.Sleep(interval * 1000)
            Dim tb As String = "grant_type=" & Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:device_code") &
                               "&client_id=" & Uri.EscapeDataString(clientId) &
                               "&device_code=" & Uri.EscapeDataString(deviceCode)
            Dim tr As String = HttpPostForm("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", tb)
            msaToken = ExtractJson(tr, "access_token")
            If msaToken <> "" Then
                Line("  授权成功，已取得微软 access token")
                Exit Do
            End If
            Dim terr As String = ExtractJson(tr, "error")
            Select Case terr
                Case "authorization_pending"
                    Console.Write(".")
                Case "slow_down"
                    interval += 5
                Case Else
                    Line("")
                    Line("  失败：" & terr & " - " & ExtractJson(tr, "error_description"))
                    Return
            End Select
        Loop
        If msaToken = "" Then
            Line("")
            Line("  超时：15 分钟内没有完成授权")
            Return
        End If
        Line("")

        ' ---------- 第 3 步：Xbox Live ----------
        Line("--- 第 3 步：换取 Xbox Live 令牌 ---")
        Dim xblBody As String = "{""Properties"":{""AuthMethod"":""RPS"",""SiteName"":""user.auth.xboxlive.com"",""RpsTicket"":""d=" & msaToken & """},""RelyingParty"":""http://auth.xboxlive.com"",""TokenType"":""JWT""}"
        Dim xblResp As String = HttpPostJson("https://user.auth.xboxlive.com/user/authenticate", xblBody)
        Dim xblToken As String = ExtractJson(xblResp, "Token")
        Dim uhs As String = ExtractJson(xblResp, "uhs")
        If xblToken = "" Then
            Line("  失败：" & Left(xblResp, 400))
            Return
        End If
        Line("  成功（uhs = " & uhs & "）")

        ' ---------- 第 4 步：XSTS ----------
        Line("--- 第 4 步：换取 XSTS 令牌 ---")
        Dim xstsBody As String = "{""Properties"":{""SandboxId"":""RETAIL"",""UserTokens"":[""" & xblToken & """]},""RelyingParty"":""rp://api.minecraftservices.com/"",""TokenType"":""JWT""}"
        Dim xstsResp As String = HttpPostJson("https://xsts.auth.xboxlive.com/xsts/authorize", xstsBody)
        Dim xstsToken As String = ExtractJson(xstsResp, "Token")
        If xstsToken = "" Then
            Dim xerr As String = ExtractJson(xstsResp, "XErr")
            Line("  失败：XErr = " & xerr)
            Select Case xerr
                Case "2148916233"
                    Line("  该微软账号还没有 Xbox 账户，需要先到 https://www.xbox.com 登录一次创建档案")
                Case "2148916235"
                    Line("  该账号所在的国家/地区不支持 Xbox Live")
                Case "2148916236", "2148916237"
                    Line("  该账号需要完成年龄验证")
                Case "2148916238"
                    Line("  未成年账号，需要成年人将其加入 Microsoft 家庭组")
            End Select
            Return
        End If
        Line("  成功")

        ' ---------- 第 5 步：Minecraft 登录 ----------
        Line("--- 第 5 步：换取 Minecraft 令牌 ---")
        Dim mcBody As String = "{""identityToken"":""XBL3.0 x=" & uhs & ";" & xstsToken & """}"
        Dim mcResp As String = HttpPostJson("https://api.minecraftservices.com/authentication/login_with_xbox", mcBody)
        Dim mcToken As String = ExtractJson(mcResp, "access_token")
        If mcToken = "" Then
            Line("  失败：" & Left(mcResp, 400))
            Return
        End If
        Line("  成功")

        ' ---------- 第 6 步：查询正版档案 ----------
        Line("--- 第 6 步：查询正版档案 ---")
        Dim profResp As String = HttpGetJson("https://api.minecraftservices.com/minecraft/profile", mcToken)
        Dim playerName As String = ExtractJson(profResp, "name")
        Dim playerId As String = ExtractJson(profResp, "id")
        If playerName = "" Then
            Line("  失败：" & Left(profResp, 400))
            Line("  如果返回 404，说明这个微软账号名下没有已购买的 Minecraft: Java Edition")
            Return
        End If
        Line("  成功")
        Line("")
        Line("=== 结论：本机可以完整走通微软登录链路 ===")
        Line("  玩家名：" & playerName)
        Line("  UUID  ：" & playerId)
    End Sub

    Private Function HttpPostForm(ByVal url As String, ByVal body As String) As String
        Return HttpSend(url, "POST", body, "application/x-www-form-urlencoded", "")
    End Function

    Private Function HttpPostJson(ByVal url As String, ByVal body As String) As String
        Return HttpSend(url, "POST", body, "application/json", "")
    End Function

    Private Function HttpGetJson(ByVal url As String, ByVal bearer As String) As String
        Return HttpSend(url, "GET", "", "", bearer)
    End Function

    ''' <summary>
    ''' 发送请求并返回响应正文。与 PCL1 里的实现保持一致：非 2xx 也要把正文读回来，
    ''' 因为设备码流程正是靠 HTTP 400 加 error 字段来表达「等待中」这类状态的。
    ''' </summary>
    ''' <summary>
    ''' 发送请求并返回响应正文。
    ''' 优先走系统 TLS；失败且属于 TLS / 证书类问题时自动改用纯托管 TLS 重试 ——
    ''' 这样在没装 TLS 1.2 补丁的 XP 上，本测试同样能走完整条链路。
    ''' </summary>
    ''' <summary>为 True 时跳过系统 TLS，强制走纯托管 TLS（用于单独验证那条通道）。</summary>
    Private ForceManagedTls As Boolean = False

    Private Function HttpSend(ByVal url As String, ByVal method As String, ByVal body As String,
                              ByVal contentType As String, ByVal bearer As String) As String
        If ForceManagedTls Then
            Line("      （强制使用纯托管 TLS）")
            Return HttpSendManaged(url, method, body, contentType, bearer)
        End If
        Try
            Return HttpSendSystem(url, method, body, contentType, bearer)
        Catch ex As Exception
            If Not IsTlsFailure(ex) Then Throw
            Line("      （系统 TLS 失败，改用纯托管 TLS 重试：" & ex.Message & "）")
            Return HttpSendManaged(url, method, body, contentType, bearer)
        End Try
    End Function

    Private Function HttpSendManaged(ByVal url As String, ByVal method As String, ByVal body As String,
                                     ByVal contentType As String, ByVal bearer As String) As String
        If method = "GET" Then
            Return ModTls.GetJson(url, bearer).Body
        ElseIf contentType = "application/json" Then
            Return ModTls.PostJson(url, body).Body
        Else
            Return ModTls.PostForm(url, body).Body
        End If
    End Function

    Private Function IsTlsFailure(ByVal ex As Exception) As Boolean
        Dim m As String = If(ex.Message, "")
        Dim inner As Exception = ex.InnerException
        Do While inner IsNot Nothing
            m &= " " & If(inner.Message, "")
            inner = inner.InnerException
        Loop
        Return m.Contains("SSL") OrElse m.Contains("TLS") OrElse m.Contains("安全通道") OrElse
               m.Contains("基础连接已经关闭") OrElse m.Contains("远程主机强迫关闭") OrElse
               m.Contains("TrustFailure") OrElse m.Contains("SecureChannel") OrElse
               m.Contains("证书") OrElse m.Contains("certificate")
    End Function

    ''' <summary>走系统 TLS 发送请求并返回响应正文。</summary>
    Private Function HttpSendSystem(ByVal url As String, ByVal method As String, ByVal body As String,
                                    ByVal contentType As String, ByVal bearer As String) As String
        Try
            Dim req As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
            req.Method = method
            req.Timeout = 30000
            req.ReadWriteTimeout = 30000
            req.UserAgent = "XPTLSProbe/1.0"
            req.KeepAlive = False
            Try
                req.ServicePoint.Expect100Continue = False
            Catch
            End Try
            If body <> "" Then
                Dim data As Byte() = New UTF8Encoding(False).GetBytes(body)
                req.ContentType = contentType
                req.ContentLength = data.Length
                Using s As Stream = req.GetRequestStream()
                    s.Write(data, 0, data.Length)
                End Using
            End If
            If bearer <> "" Then req.Headers.Add("Authorization", "Bearer " & bearer)

            Dim res As HttpWebResponse = Nothing
            Try
                res = CType(req.GetResponse(), HttpWebResponse)
            Catch ex As WebException
                If ex.Response IsNot Nothing Then
                    res = CType(ex.Response, HttpWebResponse)
                Else
                    Return "{""error"":""network"",""error_description"":""" & EscapeJson(ex.Status.ToString() & " - " & ex.Message) & """}"
                End If
            End Try
            Using res
                Using sr As New StreamReader(res.GetResponseStream(), Encoding.UTF8)
                    Return sr.ReadToEnd()
                End Using
            End Using
        Catch ex As Exception
            Return "{""error"":""exception"",""error_description"":""" & EscapeJson(ex.Message) & """}"
        End Try
    End Function

    ''' <summary>极简 JSON 取值，只处理 "key":"value" 形式，足够解析 OAuth 与 Xbox 的响应。</summary>
    Private Function ExtractJson(ByVal json As String, ByVal key As String) As String
        If json = "" Then Return ""
        Dim m As Match = Regex.Match(json, """" & Regex.Escape(key) & """\s*:\s*""([^""]*)""")
        If m.Success Then Return m.Groups(1).Value
        Return ""
    End Function

    Private Function EscapeJson(ByVal s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, " ").Replace(vbLf, " ")
    End Function

#End Region

#Region "生成注册表修复文件"

    ''' <summary>
    ''' 生成可以直接双击导入的 .reg 文件，省去手工编辑注册表的麻烦。
    ''' 写 HKLM 需要管理员权限，双击 .reg 时系统会提示。
    ''' 文件用 UTF-16 LE 编码写出 —— 这是 regedit 期望的 .reg 文件编码。
    ''' </summary>
    Private Sub MakeRegFiles()
        Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
        Line("=== 生成注册表修复文件 ===")
        Line("")
        Line("生成的文件可以直接双击导入（写 HKLM 时系统会请求管理员权限）。")
        Line("")

        ' ---------- 1) PosReady 标识 ----------
        Dim sb1 As New StringBuilder
        sb1.AppendLine("Windows Registry Editor Version 5.00")
        sb1.AppendLine()
        sb1.AppendLine("; 把系统标识伪装成 Windows Embedded POSReady 2009")
        sb1.AppendLine("; 必须先导入本文件，KB4019276 才安装得上")
        sb1.AppendLine()
        sb1.AppendLine("[HKEY_LOCAL_MACHINE\SYSTEM\WPA\PosReady]")
        sb1.AppendLine("""Installed""=dword:00000001")
        Dim p1 As String = Path.Combine(dir, "EnablePosReady.reg")
        File.WriteAllText(p1, sb1.ToString(), Encoding.Unicode)
        Line("  [1] " & p1)
        Line("      作用：伪装成 POSReady 2009。不先做这一步，KB4019276 会装不上或装完无效。")
        Line("")

        ' ---------- 2) TLS 开关 ----------
        Dim sb2 As New StringBuilder
        sb2.AppendLine("Windows Registry Editor Version 5.00")
        sb2.AppendLine()
        sb2.AppendLine("; 打开 TLS 1.1 / 1.2（前提：系统已安装 KB4019276 之类的 schannel 更新）")
        sb2.AppendLine()
        sb2.AppendLine("[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.1\Client]")
        sb2.AppendLine("""DisabledByDefault""=dword:00000000")
        sb2.AppendLine()
        sb2.AppendLine("[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client]")
        sb2.AppendLine("""DisabledByDefault""=dword:00000000")
        sb2.AppendLine()
        sb2.AppendLine("; 让 WinHTTP 使用 TLS 1.0 | 1.1 | 1.2（0xa80）")
        sb2.AppendLine("[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\WinHttp]")
        sb2.AppendLine("""DefaultSecureProtocols""=dword:00000a80")
        sb2.AppendLine()
        sb2.AppendLine("; 让 .NET Framework 4.0 跟随系统的 TLS 设置并使用强加密")
        sb2.AppendLine("[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\v4.0.30319]")
        sb2.AppendLine("""SystemDefaultTlsVersions""=dword:00000001")
        sb2.AppendLine("""SchUseStrongCrypto""=dword:00000001")
        Dim p2 As String = Path.Combine(dir, "EnableTLS.reg")
        File.WriteAllText(p2, sb2.ToString(), Encoding.Unicode)
        Line("  [2] " & p2)
        Line("      作用：打开 TLS 1.1/1.2 开关，并让 .NET 跟随系统设置。")
        Line("")

        Line("建议顺序：")
        Line("  导入 [1] -> 安装 KB4019276 等补丁 -> 导入 [2] -> 重启 -> 重新运行本探针确认")
    End Sub

#End Region

#Region "工具函数"

    Private Sub Line(ByVal s As String)
        Output.AppendLine(s)
    End Sub

    Private Function Pad(ByVal s As String, ByVal n As Integer) As String
        If s Is Nothing Then s = ""
        If s.Length >= n Then Return s
        Return s & New String(" "c, n - s.Length)
    End Function

    Private Function ProtoName(ByVal v As Integer) As String
        Select Case v
            Case 48
                Return "SSL 3.0"
            Case 192
                Return "TLS 1.0"
            Case 768
                Return "TLS 1.1"
            Case 3072
                Return "TLS 1.2"
            Case 12288
                Return "TLS 1.3"
            Case Else
                Return "未知协议(" & v & ")"
        End Select
    End Function

    ''' <summary>把证书颁发者的完整 DN 精简成最常见的 CN 部分。</summary>
    Private Function ShortIssuer(ByVal issuer As String) As String
        If issuer Is Nothing Then Return ""
        Dim i As Integer = issuer.IndexOf("CN=", StringComparison.OrdinalIgnoreCase)
        If i < 0 Then Return issuer
        Dim rest As String = issuer.Substring(i + 3)
        Dim comma As Integer = rest.IndexOf(","c)
        If comma > 0 Then rest = rest.Substring(0, comma)
        Return rest
    End Function

#End Region

End Module

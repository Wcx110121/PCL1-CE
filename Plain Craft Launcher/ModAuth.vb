Imports System.IO
Imports System.Net
Imports System.Net.Security
Imports System.Security.Cryptography.X509Certificates
Imports System.Text
Imports Newtonsoft.Json.Linq

''' <summary>
''' 微软账号（MSA）登录模块，负责启动器的正版登录。
'''
''' 正版登录必须走「微软账号 → Xbox Live → XSTS → Minecraft 服务」这条链路，
''' 旧的账号密码接口早已随 Mojang 账号体系迁移而下线。
'''
''' 采用 OAuth 2.0 设备码流程（Device Code Flow），而不是浏览器回调或内嵌网页：
''' 启动器只负责向微软申请一个 user_code 并显示出来，用户拿手机或另一台电脑
''' 打开 https://www.microsoft.com/link 输入该代码即可完成授权。
''' 这是 Windows XP 上唯一可行的方案 —— XP 自带的 IE 打不开现代微软登录页
''' （TLS 版本、根证书、页面脚本三重不满足），而设备码流程把浏览器环节完全移出了本机。
''' </summary>
Public Module ModAuth

#Region "配置"

    ''' <summary>
    ''' 内置的 Azure 应用（客户端）ID —— PCL1-CE 官方发布版使用的身份标识。
    '''
    ''' 内置之后使用者就不需要自己去注册 Azure 应用了，和 PCL2 / HMCL 等启动器的做法一致。
    ''' 该 ID 不是密钥，可以随程序一起分发。
    ''' 若它将来失效（被撤销、限流等），使用者可以在 PCL1_CE\msa_client_id.txt 里
    ''' 填一个自己的 ID 来覆盖 —— 配置文件的优先级高于这里。
    ''' </summary>
    Private Const BUILTIN_CLIENT_ID As String = "04667268-28eb-44f7-9f83-c257b1aa956e"

    ''' <summary>
    ''' client_id 的配置文件路径（相对于 PCL 程序目录）。
    ''' 内容为一行纯文本的 client_id，不要加引号或其它内容。
    ''' 放在文件里而不是源码里，是为了让你拿到编译好的程序后无需重新编译即可使用。
    ''' </summary>
    Public Const CLIENT_ID_FILE As String = "PCL1_CE\msa_client_id.txt"

    Private _ClientId As String = ""

    ''' <summary>
    ''' 当前生效的 Azure 应用（客户端）ID。取值优先级：
    '''   1. 源码里的 BUILTIN_CLIENT_ID 常量（如果你改过源码并重新编译）；
    '''   2. 程序目录下的 PCL1_CE\msa_client_id.txt 文件内容（推荐，免重新编译）；
    '''   3. 空字符串 —— 此时登录会给出明确的配置指引。
    '''
    ''' 如何获得这个 ID（免费，5 分钟）：
    '''   1. 打开 https://portal.azure.com → 「应用注册」→「新注册」；
    '''   2. 受支持的账户类型选「任何组织目录中的账户和个人 Microsoft 账户」
    '''      （必须是这一项，个人微软账号才登得进来）；
    '''   3. 「身份验证」→「添加平台」→「移动和桌面应用程序」，
    '''      勾选 https://www.microsoft.com/link 作为重定向 URI；
    '''   4. 「身份验证」→「高级设置」→「允许公共客户端流」必须设为【是】，
    '''      否则 devicecode 接口会直接返回 AADSTS7000218；
    '''   5. 复制「应用程序(客户端) ID」，粘贴到 PCL1_CE\msa_client_id.txt。
    ''' 该 ID 不是密钥，可以随程序一起分发。
    ''' </summary>
    Public ReadOnly Property ClientId As String
        Get
            If _ClientId = "" Then _ClientId = ResolveClientId()
            Return _ClientId
        End Get
    End Property

    ''' <summary>清掉缓存，让下次读取重新去查配置文件（保存了新的 client_id 之后调用）。</summary>
    Public Sub ReloadClientId()
        _ClientId = ""
    End Sub

    Private Function ResolveClientId() As String
        ' 1) 配置文件 —— 优先，让使用者能覆盖内置的 ID
        ' 注意：局部变量名不能叫 path —— VB 不区分大小写，那样会和全局的 PATH 撞名，
        ' 变成一个「用自己初始化自己」的空变量（编译器会报 BC42104）。
        Try
            Dim clientIdPath As String = PATH & CLIENT_ID_FILE
            If File.Exists(clientIdPath) Then
                Dim text As String = File.ReadAllText(clientIdPath).Trim()
                ' 去掉可能被一起复制进来的 BOM 和引号
                text = text.Trim(""""c, " "c, ControlChars.Cr, ControlChars.Lf, ControlChars.Tab)
                If text <> "" Then Return text
            End If
        Catch ex As Exception
            log("[Auth] 读取 client_id 配置文件失败：" & ex.Message)
        End Try
        ' 2) 源码内置的默认值（发布版靠这个做到开箱即用）
        If BUILTIN_CLIENT_ID <> "" AndAlso BUILTIN_CLIENT_ID <> "REPLACE_WITH_YOUR_AZURE_CLIENT_ID" Then
            Return BUILTIN_CLIENT_ID.Trim()
        End If
        Return ""
    End Function

    ''' <summary>
    ''' 配置指引文本，供界面在未配置时展示。
    ''' </summary>
    Public Function ClientIdHelpText() As String
        Return "尚未配置 Azure 应用（客户端）ID，微软登录无法进行。" & vbCrLf & vbCrLf &
               "请按以下步骤操作：" & vbCrLf &
               "1. 打开 https://portal.azure.com 并登录你的微软账号；" & vbCrLf &
               "2. 进入「应用注册」→「新注册」；" & vbCrLf &
               "3. 受支持的账户类型选「任何组织目录中的账户和个人 Microsoft 账户」；" & vbCrLf &
               "4. 注册后在「身份验证」里添加平台「移动和桌面应用程序」，" & vbCrLf &
               "   勾选 https://www.microsoft.com/link；" & vbCrLf &
               "5. 在「身份验证」→「高级设置」中把「允许公共客户端流」设为【是】；" & vbCrLf &
               "6. 复制「应用程序(客户端) ID」，粘贴到下面这个文件里（一行纯文本）：" & vbCrLf & vbCrLf &
               "    " & PATH & CLIENT_ID_FILE & vbCrLf
    End Function

    Private Const MSA_AUTHORITY As String = "https://login.microsoftonline.com/consumers/oauth2/v2.0"
    Private Const MSA_SCOPE As String = "XboxLive.signin offline_access"
    Private Const URL_DEVICECODE As String = MSA_AUTHORITY & "/devicecode"
    Private Const URL_TOKEN As String = MSA_AUTHORITY & "/token"
    Private Const URL_XBL_AUTH As String = "https://user.auth.xboxlive.com/user/authenticate"
    Private Const URL_XSTS_AUTH As String = "https://xsts.auth.xboxlive.com/xsts/authorize"
    Private Const URL_MC_LOGIN As String = "https://api.minecraftservices.com/authentication/login_with_xbox"
    Private Const URL_MC_PROFILE As String = "https://api.minecraftservices.com/minecraft/profile"
    Private Const URL_MC_ENTITLEMENTS As String = "https://api.minecraftservices.com/entitlements/mcstore"

    ''' <summary>
    ''' 用户需要访问的授权页面。
    ''' </summary>
    Public Const URL_MSA_LINK As String = "https://www.microsoft.com/link"

    Private Const HTTP_USER_AGENT As String = "PCL1-MSA/1.0"

#End Region

#Region "TLS / Windows XP 兼容"

    ' .NET Framework 4.0 的 SecurityProtocolType 枚举里【没有】Tls11 / Tls12 成员
    ' （它们是 .NET 4.5 才加进去的），所以只能强制转型成枚举值使用。
    Public Const PROTO_TLS10 As Integer = 192
    Public Const PROTO_TLS11 As Integer = 768
    Public Const PROTO_TLS12 As Integer = 3072

    Private TLSReady As Boolean = False
    Private CertCompatOn As Boolean = False

    ''' <summary>
    ''' 启用现代 TLS 协商。必须在任何 HTTPS 请求之前调用一次。
    '''
    ''' Windows XP 的 schannel 默认只支持到 TLS 1.0，而微软登录、Xbox Live、
    ''' Minecraft 服务全部要求 TLS 1.2。XP 需要安装 KB4019276（官方只对
    ''' Windows Embedded POSReady 2009 发布，普通 XP SP3 可安装但不受支持）
    ''' 并设置注册表后，schannel 才会提供 TLS 1.2。
    '''
    ''' 这里同时保留 TLS 1.0 作为回退，避免在尚未打补丁的机器上把 HTTP 层
    ''' 彻底锁死（届时表现为连接被服务器拒绝，而不是本地直接抛异常）。
    ''' </summary>
    Public Sub EnableModernTLS()
        If TLSReady Then Exit Sub
        Try
            Dim protos As Integer = PROTO_TLS12 Or PROTO_TLS11 Or PROTO_TLS10
            ServicePointManager.SecurityProtocol = CType(protos, SecurityProtocolType)
            TLSReady = True
            log("[Auth] 已启用 TLS 协商：" & protos & "（含 TLS 1.2）")
        Catch ex As Exception
            log("[Auth] 启用 TLS 1.2 失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 打开「XP 证书兼容模式」：跳过服务器证书链验证。
    '''
    ''' 为什么需要：Windows XP 的受信任根证书列表随 2014 年 4 月系统支持终止而
    ''' 停止更新，而本模块要访问的站点使用的都是 2013 年之后签发的新根证书 ——
    '''   login.microsoftonline.com -> DigiCert Global Root G2
    '''   bmclapi2.bangbang93.com  -> Sectigo Public Server Auth Root R46
    ''' 这些根在 XP 上默认都不存在，会导致「基础连接已经关闭：无法建立 SSL/TLS
    ''' 的安全通道」之外的另一类失败：证书链验证不通过。
    '''
    ''' 取舍：跳过验证意味着理论上可被中间人攻击，凭据有泄露风险。
    ''' 更正规的做法是把根证书导入 XP 的「受信任的根证书颁发机构」存储，
    ''' 在那之前，这个开关是让登录能跑起来的现实手段。
    ''' </summary>
    Public Sub EnableCertificateCompat()
        Try
            ServicePointManager.ServerCertificateValidationCallback =
                Function(sender As Object, cert As X509Certificate, chain As X509Chain, errors As SslPolicyErrors) As Boolean
                    Return True
                End Function
            CertCompatOn = True
            log("[Auth] 已开启证书验证兼容模式（跳过证书链校验）")
        Catch ex As Exception
            log("[Auth] 设置证书兼容模式失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 当前是否处于证书兼容模式。
    ''' </summary>
    Public ReadOnly Property CertificateCompatEnabled As Boolean
        Get
            Return CertCompatOn
        End Get
    End Property

    ''' <summary>
    ''' 只报告本机与 TLS 相关的配置，不做任何网络请求，可以安全地用在错误提示里。
    ''' </summary>
    Public Function DescribeLocalTls() As String
        Dim sb As New StringBuilder
        sb.AppendLine("操作系统：" & Environment.OSVersion.VersionString)
        sb.AppendLine("CLR 版本：" & Environment.Version.ToString)
        sb.AppendLine("已安装 .NET：" & GetInstalledDotNetFrameworks())
        sb.AppendLine("当前安全协议：" & ServicePointManager.SecurityProtocol.ToString)
        sb.AppendLine("证书兼容模式：" & If(CertCompatOn, "已开启", "已关闭"))
        sb.AppendLine("TLS 通道：" & If(UseManagedTls, "纯托管 TLS（BouncyCastle，已绕开 schannel）", "系统 schannel"))
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' 诊断当前环境的 TLS 能力，返回可读的结论文本。
    ''' 在 XP 上排查「连不上」时先调用这个。
    ''' </summary>
    Public Function DiagnoseTLS() As String
        Dim sb As New StringBuilder
        sb.AppendLine("操作系统：" & Environment.OSVersion.VersionString)
        sb.AppendLine("CLR 版本：" & Environment.Version.ToString)
        sb.AppendLine("已安装 .NET：" & GetInstalledDotNetFrameworks())
        sb.AppendLine("当前 SecurityProtocol：" & ServicePointManager.SecurityProtocol.ToString)
        sb.AppendLine("证书兼容模式：" & If(CertCompatOn, "开", "关"))
        sb.AppendLine()
        sb.AppendLine("--- 逐站点 TLS 连通性 ---")
        For Each url In New String() {URL_DEVICECODE, URL_XBL_AUTH, URL_XSTS_AUTH, URL_MC_LOGIN, URL_MC_ENTITLEMENTS, URL_MC_PROFILE}
            Dim host As String = New Uri(url).Host
            Dim result As String
            Try
                Dim req As HttpWebRequest = CType(WebRequest.Create("https://" & host & "/"), HttpWebRequest)
                req.Timeout = 10000
                req.UserAgent = HTTP_USER_AGENT
                Using res As HttpWebResponse = CType(req.GetResponse(), HttpWebResponse)
                    result = "OK（HTTP " & CInt(res.StatusCode) & "，TLS 握手成功）"
                End Using
            Catch ex As WebException
                If ex.Response IsNot Nothing Then
                    result = "OK（HTTP " & CInt(CType(ex.Response, HttpWebResponse).StatusCode) & "，TLS 握手成功）"
                Else
                    result = "失败：" & ex.Status.ToString & " / " & ex.Message
                End If
            Catch ex As Exception
                result = "失败：" & ex.Message
            End Try
            sb.AppendLine("  " & host.PadRight(34) & result)
        Next
        Return sb.ToString()
    End Function

    Private Function GetInstalledDotNetFrameworks() As String
        Try
            Using key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP")
                If key Is Nothing Then Return "未知"
                Dim names As New List(Of String)
                For Each subName In key.GetSubKeyNames()
                    names.Add(subName)
                Next
                Return String.Join(", ", names.ToArray())
            End Using
        Catch ex As Exception
            Return "读取失败：" & ex.Message
        End Try
    End Function

#End Region

#Region "数据结构"

    ''' <summary>
    ''' 设备码流程第一步返回的信息，需要展示给用户。
    ''' </summary>
    Public Structure MSADeviceCodeInfo
        ''' <summary>启动器内部轮询用的长串，不需要给用户看。</summary>
        Public DeviceCode As String
        ''' <summary>需要用户输入到微软页面的短代码，形如 ABCD-EFGH。</summary>
        Public UserCode As String
        ''' <summary>用户应访问的网址。</summary>
        Public VerificationUri As String
        ''' <summary>建议的轮询间隔（秒）。</summary>
        Public Interval As Integer
        ''' <summary>代码有效期（秒）。</summary>
        Public ExpiresIn As Integer
    End Structure

    ''' <summary>
    ''' 一次登录的最终结果。
    ''' </summary>
    Public Structure MSALoginResult
        ''' <summary>是否整条链路都成功。</summary>
        Public Success As Boolean
        ''' <summary>微软账号的 access token（一般不需要用到）。</summary>
        Public MsaAccessToken As String
        ''' <summary>微软账号的 refresh token，用于下次免交互登录，必须持久化。</summary>
        Public MsaRefreshToken As String
        ''' <summary>Minecraft 服务的 access token（JWT，约 24 小时有效），即要传给游戏的 accessToken。</summary>
        Public MinecraftToken As String
        ''' <summary>正版玩家名。</summary>
        Public PlayerName As String
        ''' <summary>正版 UUID（32 位无横线小写十六进制），与 PCL1 现有格式一致。</summary>
        Public PlayerUUID As String
        ''' <summary>失败时的可读原因（已翻译成中文）。</summary>
        Public ErrorMessage As String
    End Structure

#End Region

#Region "HTTP 基础设施"

    ''' <summary>
    ''' 统一的 JSON 请求封装。
    ''' 关键点：设备码流程的「等待用户授权」是通过 HTTP 400 + error 字段表达的，
    ''' 所以这里不能把非 2xx 直接当异常抛出，必须把响应体读回来交给上层判断。
    '''
    ''' 通道选择：优先用系统 TLS（走 schannel，速度快、无额外依赖），
    ''' 一旦发现系统 TLS 不可用（XP 上没装 TLS 1.2 补丁，或根证书过旧），
    ''' 就永久切换到纯托管 TLS（BouncyCastle），不再回头重试。
    ''' </summary>
    Private Function HttpJson(ByVal Url As String, ByVal Method As String, ByVal Body As String,
                              ByVal ContentType As String, ByVal Bearer As String) As JObject
        If ClientId = "" Then
            Throw New Exception(ClientIdHelpText())
        End If
        If Not UseManagedTls Then
            Try
                Return HttpJsonSystem(Url, Method, Body, ContentType, Bearer)
            Catch ex As Exception
                If Not IsTlsRelated(ex) Then Throw
                UseManagedTls = True
                log("[Auth] 系统 TLS 不可用，切换到纯托管 TLS：" & ex.Message)
            End Try
        End If
        Return HttpJsonManaged(Url, Method, Body, ContentType, Bearer)
    End Function

    ' ================= 纯托管 TLS =================

    ''' <summary>是否已经切换到纯托管 TLS。</summary>
    Private UseManagedTls As Boolean = False

    ''' <summary>当前是否正在使用纯托管 TLS。</summary>
    Public ReadOnly Property UsingManagedTls As Boolean
        Get
            Return UseManagedTls
        End Get
    End Property

    ''' <summary>判断异常是否属于 TLS / 证书类问题，用来决定要不要回退到托管 TLS。</summary>
    Private Function IsTlsRelated(ByVal ex As Exception) As Boolean
        Dim m As String = If(ex.Message, "")
        Dim inner As Exception = ex.InnerException
        Do While inner IsNot Nothing
            m &= " " & If(inner.Message, "")
            inner = inner.InnerException
        Loop
        Return m.Contains("SSL") OrElse m.Contains("TLS") OrElse m.Contains("安全通道") OrElse
               m.Contains("基础连接已经关闭") OrElse m.Contains("远程主机强迫关闭") OrElse
               m.Contains("TrustFailure") OrElse m.Contains("SecureChannel") OrElse
               m.Contains("证书") OrElse m.Contains("certificate") OrElse m.Contains("信任")
    End Function

    ''' <summary>走纯托管 TLS（BouncyCastle），完全不经过 schannel。</summary>
    Private Function HttpJsonManaged(ByVal Url As String, ByVal Method As String, ByVal Body As String,
                                     ByVal ContentType As String, ByVal Bearer As String) As JObject
        Dim res As ModTls.TlsHttpResponse
        Try
            If Method = "GET" Then
                res = ModTls.GetJson(Url, Bearer)
            ElseIf ContentType = "application/json" Then
                res = ModTls.PostJson(Url, Body)
            Else
                res = ModTls.PostForm(Url, Body)
            End If
        Catch ex As Exception
            If TypeOf ex Is FileNotFoundException OrElse TypeOf ex Is TypeLoadException Then
                Throw New Exception("纯托管 TLS 不可用：找不到 BouncyCastle.Crypto.dll。" & vbCrLf &
                                    "请确认它与 PCL.exe 放在同一个目录下。")
            End If
            Throw New Exception("纯托管 TLS 请求失败：" & ex.Message)
        End Try

        log("[Auth] " & Method & " " & Url & " -> HTTP " & res.StatusCode & "（托管 TLS）")
        Dim text As String = res.Body
        If text IsNot Nothing AndAlso text.TrimStart().StartsWith("{") Then
            Return JObject.Parse(text)
        End If
        Throw New Exception("服务器返回了非 JSON 内容（HTTP " & res.StatusCode & "）：" & Left(text, 200))
    End Function

    ''' <summary>走系统 TLS（HttpWebRequest → schannel）发请求。</summary>
    Private Function HttpJsonSystem(ByVal Url As String, ByVal Method As String, ByVal Body As String,
                                    ByVal ContentType As String, ByVal Bearer As String) As JObject
        Dim req As HttpWebRequest = CType(WebRequest.Create(Url), HttpWebRequest)
        req.Method = Method
        req.Timeout = 30000
        req.ReadWriteTimeout = 30000
        req.UserAgent = HTTP_USER_AGENT
        req.KeepAlive = False
        req.AllowAutoRedirect = True
        ' XP 上部分 schannel 配置对 Expect: 100-continue 处理有问题，直接关掉
        Try
            req.ServicePoint.Expect100Continue = False
        Catch
        End Try

        If Body <> "" Then
            Dim data As Byte() = New UTF8Encoding(False).GetBytes(Body)
            req.ContentType = ContentType
            req.ContentLength = data.Length
            Using s As Stream = req.GetRequestStream()
                s.Write(data, 0, data.Length)
            End Using
        End If
        If Bearer <> "" Then req.Headers.Add("Authorization", "Bearer " & Bearer)

        Dim res As HttpWebResponse = Nothing
        Try
            res = CType(req.GetResponse(), HttpWebResponse)
        Catch ex As WebException
            ' 400/401 等错误响应同样带着我们需要的 JSON 正文
            If ex.Response IsNot Nothing Then
                res = CType(ex.Response, HttpWebResponse)
            Else
                Throw New Exception(ExplainWebException(ex, Url))
            End If
        End Try

        Using res
            Dim text As String
            Using sr As New StreamReader(res.GetResponseStream(), Encoding.UTF8)
                text = sr.ReadToEnd()
            End Using
            log("[Auth] " & Method & " " & Url & " -> HTTP " & CInt(res.StatusCode))
            If text.TrimStart().StartsWith("{") Then
                Return JObject.Parse(text)
            End If
            Throw New Exception("服务器返回了非 JSON 内容（HTTP " & CInt(res.StatusCode) & "）：" & Left(text, 200))
        End Using
    End Function

    ''' <summary>
    ''' 把 .NET 的网络异常翻译成对 XP 用户有意义的中文提示。
    ''' </summary>
    Private Function ExplainWebException(ByVal ex As WebException, ByVal Url As String) As String
        Select Case ex.Status
            Case WebExceptionStatus.TrustFailure
                Return "无法与 " & New Uri(Url).Host & " 建立受信任的连接。" & vbCrLf &
                       "这通常是 Windows XP 的受信任根证书列表过旧导致的（系统已在 2014 年停止更新根证书）。" & vbCrLf &
                       "解决方式：开启证书兼容模式，或手动导入服务器的根证书。"
            Case WebExceptionStatus.SecureChannelFailure, WebExceptionStatus.ConnectFailure
                Return "无法与 " & New Uri(Url).Host & " 建立 TLS 连接（" & ex.Status.ToString & "）。" & vbCrLf &
                       "这通常是 Windows XP 未启用 TLS 1.2 导致的。" & vbCrLf &
                       "解决方式：安装 KB4019276（POSReady 2009 的 TLS 1.2 更新）并设置注册表项：" & vbCrLf &
                       "  HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client\Enabled = 1" & vbCrLf &
                       "  HKLM\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client\DisabledByDefault = 0" & vbCrLf &
                       "  HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\SystemDefaultTlsVersions = 1" & vbCrLf &
                       "  HKLM\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\SchUseStrongCrypto = 1"
            Case WebExceptionStatus.NameResolutionFailure
                Return "无法解析域名 " & New Uri(Url).Host & "，请检查网络连接与 DNS 设置。"
            Case WebExceptionStatus.Timeout
                Return "连接 " & New Uri(Url).Host & " 超时。"
            Case Else
                Return "网络错误（" & ex.Status.ToString & "）：" & ex.Message
        End Select
    End Function

    Private Function FormEncode(ByVal pairs As Dictionary(Of String, String)) As String
        Dim sb As New StringBuilder
        For Each kv In pairs
            If sb.Length > 0 Then sb.Append("&")
            sb.Append(Uri.EscapeDataString(kv.Key)).Append("=").Append(Uri.EscapeDataString(kv.Value))
        Next
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' 从微软的错误响应里取出可读信息。
    ''' </summary>
    ''' <summary>
    ''' 从错误响应里取出可读信息。
    '''
    ''' 各服务的字段名并不统一：
    '''   微软身份平台  -> error / error_description
    '''   Minecraft 服务 -> errorType / error / errorMessage
    ''' 以前只认前两个，导致 Minecraft 服务报错时用户只看到一个空白的失败提示。
    ''' 现在全都取，实在取不到就把整个响应吐出来一段，总比空白强。
    ''' </summary>
    Private Function TakeError(ByVal json As JObject) As String
        If json Is Nothing Then Return ""
        ' 微软身份平台
        If json("error_description") IsNot Nothing Then
            Dim d As String = json("error_description").ToString
            Dim nl As Integer = d.IndexOf(vbLf)
            If nl > 0 Then d = d.Substring(0, nl).Trim()
            Return d
        End If
        ' Minecraft 服务
        Dim parts As New List(Of String)
        If json("error") IsNot Nothing Then parts.Add(json("error").ToString)
        If json("errorType") IsNot Nothing Then parts.Add(json("errorType").ToString)
        If json("errorMessage") IsNot Nothing Then parts.Add(json("errorMessage").ToString)
        If parts.Count > 0 Then Return String.Join(" / ", parts.ToArray())
        ' 兜底
        Dim raw As String = json.ToString(Newtonsoft.Json.Formatting.None)
        If raw.Length > 300 Then raw = raw.Substring(0, 300) & "…"
        Return raw
    End Function

#End Region

#Region "第一步：微软账号"

    ''' <summary>
    ''' 申请设备码。调用后应把返回的 UserCode 与网址展示给用户。
    ''' </summary>
    Public Function MSARequestDeviceCode() As MSADeviceCodeInfo
        EnableModernTLS()
        Dim body As String = FormEncode(New Dictionary(Of String, String) From {
            {"client_id", ClientId},
            {"scope", MSA_SCOPE}
        })
        Dim json As JObject = HttpJson(URL_DEVICECODE, "POST", body, "application/x-www-form-urlencoded", "")
        If json("user_code") Is Nothing Then
            Throw New Exception("申请设备码失败：" & TakeError(json))
        End If
        Dim info As New MSADeviceCodeInfo
        info.DeviceCode = json("device_code").ToString
        info.UserCode = json("user_code").ToString
        info.VerificationUri = If(json("verification_uri") IsNot Nothing, json("verification_uri").ToString, URL_MSA_LINK)
        info.Interval = If(json("interval") IsNot Nothing, CInt(json("interval")), 5)
        info.ExpiresIn = If(json("expires_in") IsNot Nothing, CInt(json("expires_in")), 900)
        log("[Auth] 设备码申请成功，有效期 " & info.ExpiresIn & " 秒")
        Return info
    End Function

    ''' <summary>
    ''' 轮询等待用户在网页上完成授权。
    ''' 这是阻塞调用，必须放在后台线程里执行。
    ''' </summary>
    ''' <param name="Info">MSARequestDeviceCode 的返回值。</param>
    ''' <param name="OnStatus">每次轮询时回调，用于向界面反馈「还在等待」。</param>
    ''' <param name="CancelFlag">返回 True 时中止等待。</param>
    Public Function MSAWaitForToken(ByVal Info As MSADeviceCodeInfo, ByVal OnStatus As Action(Of String),
                                    ByVal CancelFlag As Func(Of Boolean)) As JObject
        EnableModernTLS()
        Dim deadline As Integer = Environment.TickCount + Info.ExpiresIn * 1000
        Dim interval As Integer = Math.Max(Info.Interval, 5)
        Dim round As Integer = 0

        Do While Environment.TickCount < deadline
            If CancelFlag IsNot Nothing AndAlso CancelFlag() Then
                Throw New Exception("已取消登录。")
            End If
            Threading.Thread.Sleep(interval * 1000)
            round += 1
            If OnStatus IsNot Nothing Then
                OnStatus("正在等待授权确认…（第 " & round & " 次查询，请勿关闭窗口）")
            End If

            Dim body As String = FormEncode(New Dictionary(Of String, String) From {
                {"grant_type", "urn:ietf:params:oauth:grant-type:device_code"},
                {"client_id", ClientId},
                {"device_code", Info.DeviceCode}
            })
            Dim json As JObject = HttpJson(URL_TOKEN, "POST", body, "application/x-www-form-urlencoded", "")
            If json("access_token") IsNot Nothing Then
                log("[Auth] 用户授权完成")
                Return json
            End If

            Dim err As String = If(json("error") IsNot Nothing, json("error").ToString, "")
            Select Case err
                Case "authorization_pending"
                    ' 正常状态，继续轮询
                Case "slow_down"
                    interval += 5
                Case "expired_token"
                    Throw New Exception("设备码已过期，请重新开始登录。")
                Case "authorization_declined"
                    Throw New Exception("用户在授权页面拒绝了本次登录。")
                Case "bad_verification_code"
                    Throw New Exception("设备码无效，请重新开始登录。")
                Case Else
                    Throw New Exception("等待授权失败：" & TakeError(json))
            End Select
        Loop
        Throw New Exception("等待授权超时（" & Info.ExpiresIn & " 秒），请重新开始登录。")
    End Function

    ''' <summary>
    ''' 用 refresh token 免交互刷新微软账号令牌。
    ''' </summary>
    Public Function MSARefresh(ByVal RefreshToken As String) As JObject
        EnableModernTLS()
        Dim body As String = FormEncode(New Dictionary(Of String, String) From {
            {"grant_type", "refresh_token"},
            {"client_id", ClientId},
            {"refresh_token", RefreshToken},
            {"scope", MSA_SCOPE}
        })
        Dim json As JObject = HttpJson(URL_TOKEN, "POST", body, "application/x-www-form-urlencoded", "")
        If json("access_token") Is Nothing Then
            Throw New Exception("刷新微软账号令牌失败：" & TakeError(json))
        End If
        Return json
    End Function

#End Region

#Region "第二步：Xbox Live 与 XSTS"

    ''' <summary>
    ''' 拿微软账号的 access token 换 Xbox Live 令牌。
    ''' 返回 (Token, Uhs) 两项，Uhs 是用户哈希，后面拼 identityToken 要用。
    ''' </summary>
    Public Function XBLAuthenticate(ByVal MsaAccessToken As String) As String()
        Dim body As String = "{""Properties"":{""AuthMethod"":""RPS"",""SiteName"":""user.auth.xboxlive.com"",""RpsTicket"":""d=" & MsaAccessToken & """},""RelyingParty"":""http://auth.xboxlive.com"",""TokenType"":""JWT""}"
        Dim json As JObject = HttpJson(URL_XBL_AUTH, "POST", body, "application/json", "")
        If json("Token") Is Nothing Then
            Throw New Exception("获取 Xbox Live 令牌失败：" & TakeError(json))
        End If
        Dim uhs As String = json("DisplayClaims")("xui")(0)("uhs").ToString
        Return New String() {json("Token").ToString, uhs}
    End Function

    ''' <summary>
    ''' 用 Xbox Live 令牌换 XSTS 授权令牌。
    ''' XSTS 的报错码（XErr）含义特殊，这里翻译成中文，否则用户只会看到一串数字。
    ''' </summary>
    Public Function XSTSAuthorize(ByVal XblToken As String) As String
        Dim body As String = "{""Properties"":{""SandboxId"":""RETAIL"",""UserTokens"":[""" & XblToken & """]},""RelyingParty"":""rp://api.minecraftservices.com/"",""TokenType"":""JWT""}"
        Dim json As JObject = HttpJson(URL_XSTS_AUTH, "POST", body, "application/json", "")

        If json("Token") Is Nothing Then
            Dim xerr As String = ""
            If json("XErr") IsNot Nothing Then xerr = json("XErr").ToString
            Select Case xerr
                Case "2148916233"
                    Throw New Exception("该微软账号还没有 Xbox 账户。" & vbCrLf &
                                        "请先到 https://www.xbox.com 登录一次以创建 Xbox 档案，然后重试。")
                Case "2148916235"
                    Throw New Exception("该账号所在的国家/地区不支持 Xbox Live。")
                Case "2148916236", "2148916237"
                    Throw New Exception("该账号需要完成年龄验证（通常是未成年人账号）。")
                Case "2148916238"
                    Throw New Exception("该账号是未成年人账号，需要由成年人加入 Microsoft 家庭组后才能登录 Minecraft。")
                Case Else
                    Throw New Exception("获取 XSTS 令牌失败（XErr=" & xerr & "）：" & TakeError(json))
            End Select
        End If
        Return json("Token").ToString
    End Function

#End Region

#Region "第三步：Minecraft 服务"

    ''' <summary>
    ''' 用 XSTS 令牌换取 Minecraft 服务的 access token。
    ''' </summary>
    Public Function MCLoginWithXbox(ByVal Uhs As String, ByVal XstsToken As String) As String
        Dim body As String = "{""identityToken"":""XBL3.0 x=" & Uhs & ";" & XstsToken & """}"
        Dim json As JObject = HttpJson(URL_MC_LOGIN, "POST", body, "application/json", "")
        If json("access_token") Is Nothing Then
            ' 把完整响应写进日志：各服务错误字段名不统一，光靠提示文字可能定位不了问题
            log("[Auth] login_with_xbox 未返回 access_token，完整响应：" &
                json.ToString(Newtonsoft.Json.Formatting.None))
            Throw New Exception("登录 Minecraft 服务失败：" & TakeError(json))
        End If
        Return json("access_token").ToString
    End Function

    ''' <summary>
    ''' 校验该账号是否拥有 Minecraft: Java Edition。
    ''' 调用官方所有权接口 /entitlements/mcstore，items 为空即表示未购买。
    ''' 这是启动前的硬性门槛：没有所有权的账号不允许启动游戏。
    ''' </summary>
    Public Sub MCCheckEntitlements(ByVal MinecraftToken As String)
        Dim json As JObject
        Try
            json = HttpJson(URL_MC_ENTITLEMENTS, "GET", "", "", MinecraftToken)
        Catch ex As Exception
            Throw New Exception("检查 Minecraft: Java Edition 所有权失败：" & ex.Message)
        End Try
        Dim Items As JToken = json("items")
        If Items Is Nothing OrElse Items.Type <> JTokenType.Array OrElse CType(Items, JArray).Count = 0 Then
            Throw New Exception("该微软账号名下没有 Minecraft: Java Edition。" & vbCrLf &
                                "本启动器只允许已购买游戏的账号启动游戏。")
        End If
    End Sub

    ''' <summary>
    ''' 查询正版档案（玩家名与 UUID）。
    ''' 注意：这里返回 404 意味着该微软账号名下没有 Minecraft: Java Edition，
    ''' 这是最常见的失败原因，必须给出明确提示而不是笼统报错。
    ''' </summary>
    Public Function MCGetProfile(ByVal MinecraftToken As String) As JObject
        Dim json As JObject
        Try
            json = HttpJson(URL_MC_PROFILE, "GET", "", "", MinecraftToken)
        Catch ex As Exception
            Throw New Exception("查询 Minecraft 档案失败：" & ex.Message & vbCrLf &
                                "如果提示 404，说明这个微软账号名下没有已购买的 Minecraft: Java Edition。")
        End Try
        If json("id") Is Nothing OrElse json("name") Is Nothing Then
            Throw New Exception("该微软账号名下没有 Minecraft: Java Edition（未购买或尚未创建档案）。")
        End If
        Return json
    End Function

#End Region

#Region "对外的完整登录入口"

    ''' <summary>
    ''' 从「已经拿到的微软账号令牌」一路走到 Minecraft 档案。
    ''' 交互式登录和刷新登录都会汇合到这里。
    ''' </summary>
    Private Function CompleteFromMsaToken(ByVal MsaTokenJson As JObject) As MSALoginResult
        Dim result As New MSALoginResult
        Try
            Dim msaAccess As String = MsaTokenJson("access_token").ToString
            result.MsaAccessToken = msaAccess
            If MsaTokenJson("refresh_token") IsNot Nothing Then
                result.MsaRefreshToken = MsaTokenJson("refresh_token").ToString
            End If

            Dim xbl As String() = XBLAuthenticate(msaAccess)
            Dim xsts As String = XSTSAuthorize(xbl(0))
            result.MinecraftToken = MCLoginWithXbox(xbl(1), xsts)

            '先显式校验所有权，再取档案。
            '这一步是有意为之：启动器只允许已购买 Minecraft: Java Edition 的账号进入游戏，
            '不能只依赖档案接口的 404 来判断（那属于间接推断）。
            MCCheckEntitlements(result.MinecraftToken)

            Dim profile As JObject = MCGetProfile(result.MinecraftToken)
            result.PlayerName = profile("name").ToString
            result.PlayerUUID = profile("id").ToString

            result.Success = True
            log("[Auth] 登录成功：" & result.PlayerName & " / " & result.PlayerUUID)
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            log("[Auth] 登录失败：" & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>
    ''' 交互式微软登录（设备码流程）。
    ''' </summary>
    ''' <param name="ShowCode">拿到设备码后回调，用于在界面上显示 user_code 与网址。</param>
    ''' <param name="OnStatus">轮询过程中回调，用于更新状态文字。</param>
    ''' <param name="CancelFlag">返回 True 时中止等待。</param>
    Public Function MSALoginInteractive(ByVal ShowCode As Action(Of MSADeviceCodeInfo),
                                        ByVal OnStatus As Action(Of String),
                                        ByVal CancelFlag As Func(Of Boolean)) As MSALoginResult
        Dim result As New MSALoginResult
        Try
            EnableModernTLS()
            Dim info As MSADeviceCodeInfo = MSARequestDeviceCode()
            If ShowCode IsNot Nothing Then ShowCode(info)
            Dim tokenJson As JObject = MSAWaitForToken(info, OnStatus, CancelFlag)
            Return CompleteFromMsaToken(tokenJson)
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            log("[Auth] 交互式登录失败：" & ex.Message)
            Return result
        End Try
    End Function

    ''' <summary>
    ''' 用已经申请好的设备码继续完成登录：等待用户在网页上授权，再一路换到 Minecraft 档案。
    ''' 界面层可以先展示 Info.UserCode，用户确认已授权后再调用本函数。
    ''' </summary>
    Public Function MSALoginWithDeviceCode(ByVal Info As MSADeviceCodeInfo, ByVal OnStatus As Action(Of String)) As MSALoginResult
        Dim result As New MSALoginResult
        Try
            EnableModernTLS()
            Dim tokenJson As JObject = MSAWaitForToken(Info, OnStatus, Nothing)
            Return CompleteFromMsaToken(tokenJson)
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            log("[Auth] 登录失败：" & ex.Message)
            Return result
        End Try
    End Function

    ''' <summary>
    ''' 用已保存的 refresh token 免交互登录。
    ''' 微软的 refresh token 会滚动更新，所以成功后必须把新的 refresh token 存回去。
    ''' </summary>
    Public Function MSALoginByRefresh(ByVal RefreshToken As String) As MSALoginResult
        Dim result As New MSALoginResult
        If RefreshToken = "" Then
            result.Success = False
            result.ErrorMessage = "没有可用的微软账号凭据，需要重新登录。"
            Return result
        End If
        Try
            EnableModernTLS()
            Dim tokenJson As JObject = MSARefresh(RefreshToken)
            result = CompleteFromMsaToken(tokenJson)
            ' 微软不一定每次都返回新的 refresh_token，没返回就沿用旧的
            If result.Success AndAlso result.MsaRefreshToken = "" Then result.MsaRefreshToken = RefreshToken
            Return result
        Catch ex As Exception
            result.Success = False
            result.ErrorMessage = ex.Message
            log("[Auth] 刷新登录失败：" & ex.Message)
            Return result
        End Try
    End Function

#End Region

End Module

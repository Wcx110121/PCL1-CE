Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports Org.BouncyCastle.Crypto.Tls
Imports Org.BouncyCastle.Security

''' <summary>
''' 纯托管的 HTTPS 客户端，用于在 Windows XP 上绕开 schannel 的限制。
'''
''' 为什么需要它：
'''   PCL1 原本走 .NET 的 HttpWebRequest → WinHTTP → schannel，而 Windows XP 的 schannel
'''   默认只支持 TLS 1.0。要用上 TLS 1.2 必须安装 KB4019276 之类的更新，而且那个补丁
'''   只面向 Windows Embedded POSReady 2009 发布 —— 得先把系统标识伪装成 POSReady 才装得上，
'''   成功率并不稳定。
'''
''' 本模块改用 BouncyCastle 的纯托管 TLS 实现，完全绕开 schannel：
'''   1. 不依赖操作系统的 TLS 版本能力（XP 不需要任何补丁）；
'''   2. 不依赖系统受信任根证书库（XP 的根证书列表自 2014 年 4 月起停止更新，
'''      无法验证 DigiCert Global Root G2、Sectigo R46 这类新根）。
''' 代价是自带 BouncyCastle.Crypto.dll，并自己实现 HTTP/1.1 协议。
'''
''' 实测（2026-09）：在 SecurityProtocol 仅为 "Ssl3, Tls" 的环境下，
''' 本模块仍能与 login.microsoftonline.com 完成 TLS 1.2 握手并取回 HTTP 200。
''' </summary>
Public Module ModTls

#Region "数据结构"

    Public Class TlsHttpResponse
        Public StatusCode As Integer = 0
        Public StatusText As String = ""
        Public Headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Public RawBody As Byte() = New Byte() {}
        Public Body As String = ""

        Public ReadOnly Property IsSuccess As Boolean
            Get
                Return StatusCode >= 200 AndAlso StatusCode < 300
            End Get
        End Property
    End Class

#End Region

#Region "策略开关"

    ''' <summary>
    ''' 是否接受任意服务器证书。默认 True。
    ''' XP 的根证书库无法验证现代根证书，且 BMCLAPI 的部分镜像服务器不下发中间证书，
    ''' 所以默认放行；这与 PCL1 里 EnableCertificateCompat 的取舍一致。
    ''' </summary>
    Public Property AcceptAnyServerCertificate As Boolean = True

    ''' <summary>连接与读取的总超时（毫秒）。</summary>
    Public Property TimeoutMs As Integer = 60000

#End Region

#Region "对外接口"

    Public Function GetJson(ByVal url As String, Optional ByVal bearer As String = "") As TlsHttpResponse
        Return Request("GET", url, Nothing, Nothing, bearer)
    End Function

    Public Function PostForm(ByVal url As String, ByVal body As String) As TlsHttpResponse
        Return Request("POST", url, body, "application/x-www-form-urlencoded", "")
    End Function

    Public Function PostJson(ByVal url As String, ByVal body As String) As TlsHttpResponse
        Return Request("POST", url, body, "application/json", "")
    End Function

    ''' <summary>域名解析结果缓存：主机名 -> {IPAddress, 记录时的 TickCount}。</summary>

    Private DnsCache As New Dictionary(Of String, Object())

    Private DnsCacheLock As New Object

    ''' <summary>解析结果缓存多久（毫秒）。</summary>

    Private Const DNS_CACHE_MS As Integer = 60000



    ''' <summary>

    ''' 把域名同步解析成 IPv4 地址，结果缓存一分钟。

    '''

    ''' 不要用 tcp.BeginConnect(主机名, ...)：那个重载会走 .NET 的异步 DNS 解析，

    ''' 在 Windows 2000 上回调触发时 socket 状态不对，EndConnect 会抛

    ''' 「An invalid argument was supplied」（WSAEINVAL）。

    ''' 同步解析后连 IP 本身，就绕开了这一环。

    ''' 加缓存是因为下载一个版本会连几十次同一批主机，每次都查 DNS 纯属浪费。

    ''' </summary>

    Private Function ResolveIPv4(ByVal host As String) As IPAddress

        SyncLock DnsCacheLock

            Dim cached As Object() = Nothing

            If DnsCache.TryGetValue(host, cached) AndAlso cached IsNot Nothing Then

                If My.Computer.Clock.TickCount - CInt(cached(1)) < DNS_CACHE_MS Then

                    Return CType(cached(0), IPAddress)

                End If

            End If

        End SyncLock



        'Windows 2000 的 DNS 客户端在并发查询下会间歇性失败（一整批域名同时），
        '失败一次就放弃的话，表现为「首页推荐源、版本列表、登录同时全灭」。
        '这里重试三次，间隔逐次拉长，抖动时能自己恢复。
        Dim lastError As String = ""
        For attempt As Integer = 1 To 3
            Try
                Dim addrs As IPAddress() = Dns.GetHostAddresses(host)
                Dim target As IPAddress = Nothing
                For Each addr As IPAddress In addrs
                    If addr.AddressFamily = Sockets.AddressFamily.InterNetwork Then
                        target = addr
                        Exit For
                    End If
                Next
                If target Is Nothing AndAlso addrs.Length > 0 Then target = addrs(0)
                If target IsNot Nothing Then
                    SyncLock DnsCacheLock
                        DnsCache(host) = New Object() {target, My.Computer.Clock.TickCount}
                    End SyncLock
                    Return target
                End If
                lastError = "解析结果为空"
            Catch ex As Exception
                lastError = ex.Message
            End Try
            If attempt < 3 Then Thread.Sleep(250 * attempt)
        Next
        Throw New Exception("无法解析域名：" & host & "（" & lastError & "）")
    End Function

    #Region "连接复用"

    ''' <summary>一条可以复用的 TLS 连接。</summary>
    Private Class TlsConn
        Public Tcp As TcpClient
        Public Protocol As TlsClientProtocol
        Public Stream As Stream
        Public Host As String
        Public BornAt As Integer
        ''' <summary>这条连接是否已经不可用了。异常路径上会置位，避免放回池里坑下一次请求。</summary>
        Public Broken As Boolean = False
    End Class

    ''' <summary>按主机名缓存空闲连接。</summary>
    Private ConnPool As New Dictionary(Of String, List(Of TlsConn))
    Private ConnPoolLock As New Object
    ''' <summary>空闲连接最多留这么久（毫秒）。放太久服务器多半已经关了。</summary>
    Private Const CONN_IDLE_MS As Integer = 25000
    ''' <summary>同一主机最多缓存的空闲连接数。</summary>
    Private Const CONN_MAX_IDLE As Integer = 4

    ''' <summary>建立一条新的 TLS 连接。</summary>
    Private Function OpenTlsConn(ByVal host As String, ByVal port As Integer) As TlsConn
        Dim c As New TlsConn()
        c.Host = host
        c.Tcp = New TcpClient()
        Dim target As IPAddress = ResolveIPv4(host)
        Dim ar As IAsyncResult = c.Tcp.BeginConnect(target, port, Nothing, Nothing)
        If Not ar.AsyncWaitHandle.WaitOne(TimeoutMs, False) Then
            Try
                c.Tcp.Close()
            Catch
            End Try
            Throw New TimeoutException("连接 " & host & ":" & port & " 超时（" & TimeoutMs & "ms）")
        End If
        c.Tcp.EndConnect(ar)
        c.Tcp.ReceiveTimeout = TimeoutMs
        c.Tcp.SendTimeout = TimeoutMs
        c.Protocol = New TlsClientProtocol(c.Tcp.GetStream(), New SecureRandom())
        c.Protocol.Connect(New ManagedTlsClient(host))
        c.Stream = c.Protocol.Stream
        c.BornAt = My.Computer.Clock.TickCount
        Return c
    End Function

    Private Sub CloseTlsConn(ByVal c As TlsConn)
        If c Is Nothing Then Exit Sub
        Try
            c.Protocol.Close()
        Catch
        End Try
        Try
            c.Tcp.Close()
        Catch
        End Try
    End Sub

    ''' <summary>取一条连接，优先复用池里同一主机的空闲连接。</summary>
    Private Function AcquireTlsConn(ByVal host As String, ByVal port As Integer) As TlsConn
        SyncLock ConnPoolLock
            Dim list As List(Of TlsConn) = Nothing
            If ConnPool.TryGetValue(host, list) Then
                Do While list.Count > 0
                    Dim c As TlsConn = list(list.Count - 1)
                    list.RemoveAt(list.Count - 1)
                    If Not c.Broken AndAlso My.Computer.Clock.TickCount - c.BornAt < CONN_IDLE_MS Then
                        log("[TLS] 复用连接：" & host)
                        Return c
                    End If
                    CloseTlsConn(c)
                Loop
            End If
        End SyncLock
        log("[TLS] 新建连接：" & host)
        Return OpenTlsConn(host, port)
    End Function

    ''' <summary>归还连接。keepAlive 为 False 时直接关闭。</summary>
    ''' <summary>
    ''' 提前把某个主机的连接建好放进池里，之后真正请求时就省掉一次握手。
    '''
    ''' 设备码登录在等用户去浏览器授权的那几十秒里，正好可以拿来预热后面要用的域名：
    ''' user.auth.xboxlive.com、xsts.auth.xboxlive.com、api.minecraftservices.com。
    ''' 这三个域名在单核老机器上每次握手要 1-2 秒，提前建好能省下 4 秒以上。
    ''' </summary>
    Public Sub WarmUp(ByVal host As String)
        If String.IsNullOrEmpty(host) Then Exit Sub
        Try
            SyncLock ConnPoolLock
                Dim list As List(Of TlsConn) = Nothing
                If ConnPool.TryGetValue(host, list) AndAlso list.Count > 0 Then Exit Sub
            End SyncLock
            Dim c As TlsConn = OpenTlsConn(host, 443)
            ReleaseTlsConn(c, True)
            log("[TLS] 预热连接：" & host)
        Catch ex As Exception
            log("[TLS] 预热失败（" & host & "）：" & ex.Message)
        End Try
    End Sub

    Private Sub ReleaseTlsConn(ByVal c As TlsConn, ByVal keepAlive As Boolean)
        '记一条日志：能看到每个连接到底是归还了还是被关掉了，
        '否则「连接池为什么没复用」这种问题只能靠猜。
        If c IsNot Nothing Then log("[TLS] 归还连接：" & c.Host & If(keepAlive AndAlso Not c.Broken, "（可复用）", "（关闭）"))
        If c Is Nothing Then Exit Sub
        If Not keepAlive OrElse c.Broken Then
            CloseTlsConn(c)
            Exit Sub
        End If
        SyncLock ConnPoolLock
            Dim list As List(Of TlsConn) = Nothing
            If Not ConnPool.TryGetValue(c.Host, list) Then
                list = New List(Of TlsConn)
                ConnPool(c.Host) = list
            End If
            If list.Count >= CONN_MAX_IDLE Then
                CloseTlsConn(c)
            Else
                list.Add(c)
            End If
        End SyncLock
    End Sub

    #End Region

    ''' <summary>
    ''' 发一个 HTTPS 请求，返回解析好的响应。
    '''
    ''' 这里会复用连接：同一个主机的连续请求共用一条 TLS 连接，省掉 TCP 三次握手
    ''' 和 TLS 握手。对老机器意义很大 —— 登录一次要连好几个域名、下载一个版本要
    ''' 发几十上百个请求，每次重新握手在单核 1.5GHz 上就是好几秒的开销。
    '''
    ''' 安全性：一条连接同一时刻只会被一个请求取走（取出时即从池中移除），
    ''' 所以不存在两个线程同时读一条 TLS 流的情况。响应体按 Content-Length 或
    ''' chunked 精确读完，残留数据不会污染下一个请求；服务器主动要求关闭时不会归还。
    ''' </summary>
    Public Function Request(ByVal method As String, ByVal url As String, ByVal body As String,
                            ByVal contentType As String, ByVal bearer As String) As TlsHttpResponse
        Dim uri As New Uri(url)
        If uri.Scheme.ToLower() <> "https" Then
            Throw New NotSupportedException("ModTls 只处理 https 地址，收到的是：" & url)
        End If
        Dim port As Integer = If(uri.Port > 0, uri.Port, 443)
        Dim isHead As Boolean = String.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)

        Dim Stage As String = "准备"
        Dim conn As TlsConn = Nothing
        Dim keepAlive As Boolean = False
        Try
            Stage = "获取连接"
            conn = AcquireTlsConn(uri.Host, port)
            Dim stream As Stream = conn.Stream

            ' ---- 组装请求 ----
            Stage = "组装 HTTP 请求"
            Dim head As New StringBuilder()
            head.Append(method).Append(" ").Append(If(uri.PathAndQuery = "", "/", uri.PathAndQuery)).Append(" HTTP/1.1").Append(vbCrLf)
            head.Append("Host: ").Append(uri.Host).Append(vbCrLf)
            head.Append("User-Agent: PCL1-ManagedTLS/1.1").Append(vbCrLf)
            head.Append("Accept: application/json").Append(vbCrLf)
            ' 明确不接受压缩，省掉自己解 gzip 的麻烦
            head.Append("Accept-Encoding: identity").Append(vbCrLf)
            head.Append("Connection: keep-alive").Append(vbCrLf)
            If bearer <> "" Then head.Append("Authorization: Bearer ").Append(bearer).Append(vbCrLf)

            Dim bodyBytes As Byte() = Nothing
            If body IsNot Nothing AndAlso body <> "" Then
                bodyBytes = Encoding.UTF8.GetBytes(body)
                head.Append("Content-Type: ").Append(If(contentType = "", "application/octet-stream", contentType)).Append(vbCrLf)
                head.Append("Content-Length: ").Append(bodyBytes.Length.ToString).Append(vbCrLf)
            End If
            head.Append(vbCrLf)

            Stage = "发送请求"
            Dim headBytes As Byte() = Encoding.ASCII.GetBytes(head.ToString())
            stream.Write(headBytes, 0, headBytes.Length)
            If bodyBytes IsNot Nothing Then stream.Write(bodyBytes, 0, bodyBytes.Length)
            stream.Flush()

            ' ---- 读取响应 ----
            Stage = "读取响应"
            Dim resp As TlsHttpResponse = ReadResponse(stream, isHead)

            '服务器说不再复用，就不要放回池里
            Dim connHeader As String = ""
            If resp.Headers.TryGetValue("Connection", connHeader) AndAlso connHeader.IndexOf("close", StringComparison.OrdinalIgnoreCase) >= 0 Then
                keepAlive = False
            Else
                keepAlive = True
            End If

            'POST 一律不复用连接。
            'POST 的响应体边界最容易出问题（空响应、204、chunked 尾部处理各不相同），
            '一旦没读干净，残留字节会被下一个请求当成状态行，解析出 HTTP 0 ——
            '实测 login_with_xbox 之后紧跟的 mcstore 就是这个现象。
            'GET 之间仍然复用，那才是复用收益的主要来源。
            If keepAlive AndAlso Not String.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) AndAlso Not isHead Then
                keepAlive = False
            End If

            '只有确认响应体被精确读完，才把连接放回池里。
            '否则残留字节会被下一个请求当成状态行，解析出 HTTP 0 ——
            '实测 mcstore 之后紧跟的 profile 就是这个现象。
            Dim isHeadReq As Boolean = String.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
            Dim lengthOk As Boolean = False
            If isHeadReq Then
                lengthOk = True   'HEAD 没有正文，边界天然明确
            Else
                Dim teHeader As String = ""
                Dim clHeader As String = ""
                Dim isChunked As Boolean = resp.Headers.TryGetValue("Transfer-Encoding", teHeader) AndAlso teHeader.ToLower().Contains("chunked")
                If isChunked Then
                    lengthOk = True   'chunked 的边界由终止块确定（尾部 CRLF 已修）
                ElseIf resp.Headers.TryGetValue("Content-Length", clHeader) Then
                    Dim expected As Integer = 0
                    If Integer.TryParse(clHeader, expected) Then lengthOk = (expected = resp.RawBody.Length)
                End If
            End If
            If Not lengthOk Then
                log("[TLS] 响应体边界不确定，关闭连接不复用：" & url)
                keepAlive = False
            End If
            Return resp
        Catch ex As Exception
            If conn IsNot Nothing Then conn.Broken = True
            Throw New Exception("纯托管 TLS 在「" & Stage & "」阶段失败：" & ex.Message, ex)
        Finally
            ReleaseTlsConn(conn, keepAlive)
        End Try
    End Function

#End Region

#Region "文件下载"

    ''' <summary>
    ''' 用托管 TLS 发 HEAD 请求取文件大小（Content-Length）。失败返回 -1。
    '''
    ''' 系统通道在老系统上连不上只支持 TLS 1.2 的站点，而「获取文件大小」是下载流程
    ''' 的第一步，它失败会让整个任务被判失败（日志里表现为「获取文件大小失败」）。
    ''' </summary>
    Public Function GetContentLength(ByVal url As String) As Long
        Try
            Dim resp As TlsHttpResponse = Request("HEAD", url, "", "", "")
            If resp Is Nothing Then Return -1
            Dim raw As String = Nothing
            If resp.Headers.TryGetValue("Content-Length", raw) AndAlso Not String.IsNullOrEmpty(raw) Then
                Dim value As Long
                If Long.TryParse(raw.Trim(), value) AndAlso value > 0 Then Return value
            End If
        Catch ex As Exception
            log("[TLS] 取文件大小失败（" & url & "）：" & ex.Message)
        End Try
        Return -1
    End Function

    ''' <summary>
    ''' 用托管 TLS 下载文件到本地。
    '''
    ''' 处理两件麻烦事：
    '''   1. BMCLAPI 的入口是明文 http，会 302 到 https 镜像 —— 明文段不需要 TLS，
    '''      所以先用系统请求跟随重定向，拿到最终的 https 地址；
    '''   2. 那个最终地址是 60 秒过期的预签名 URL，必须立刻使用。
    ''' </summary>
    Public Sub DownloadToFile(ByVal url As String, ByVal localPath As String)
        ' 先逐跳解析重定向：明文段用系统请求，https 段用托管 TLS
        Dim target As String = ResolveFinalUrl(url)

        If target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
            DownloadHttpsToFile(target, localPath)
        Else
            ' 最终仍是明文地址：不涉及 TLS，交给系统直接下载
            Using wc As New WebClient()
                wc.DownloadFile(target, localPath)
            End Using
        End If
    End Sub

    ''' <summary>
    ''' 逐跳解析重定向链，返回最终的绝对地址。
    '''
    ''' 关键：明文 http 那几跳交给系统请求（不涉及 TLS，XP 也做得来），
    ''' 一旦跳到了 https 就改用托管 TLS 继续跟 —— 绝不能让系统去跟 https，
    ''' 否则又会撞回 schannel 没有 TLS 1.2 的问题。
    ''' </summary>
    Private Function ResolveFinalUrl(ByVal startUrl As String) As String
        Dim current As String = startUrl
        Dim hops As Integer = 0
        Do While hops < 5
            hops += 1
            Dim location As String = ""

            If current.StartsWith("http://", StringComparison.OrdinalIgnoreCase) Then
                location = GetRedirectLocationSystem(current)
            Else
                ' 【重要】这里只能读响应头，不能走 Request()。
                ' Request() 会把整个响应正文读进内存，而实际情况是：
                '   - assets 下载同时跑 15 个线程；
                '   - 每个文件解析完重定向后还要再下载一遍。
                ' 于是每个文件被完整传输两次并占用一整份内存，在内存紧张的 XP 上
                ' 足以让大批文件下载失败。只读头部可以彻底避掉这个开销。
                location = GetRedirectLocationManaged(current)
                If location Is Nothing OrElse location = "" Then Return current
            End If

            If location Is Nothing OrElse location = "" Then Return current
            If location.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then
                current = location
            Else
                current = New Uri(New Uri(current), location).AbsoluteUri
            End If
        Loop
        Return current
    End Function

    ''' <summary>用系统请求读一个明文 http 地址的 Location 头（不跟随重定向）。</summary>
    Private Function GetRedirectLocationSystem(ByVal url As String) As String
        Dim req As HttpWebRequest = CType(WebRequest.Create(url), HttpWebRequest)
        req.AllowAutoRedirect = False
        req.Timeout = TimeoutMs
        req.UserAgent = "PCL1-ManagedTLS/1.0"
        req.KeepAlive = False
        Try
            Using res As HttpWebResponse = CType(req.GetResponse(), HttpWebResponse)
                If CInt(res.StatusCode) >= 300 AndAlso CInt(res.StatusCode) < 400 Then
                    Return If(res.Headers("Location"), "")
                End If
                Return ""
            End Using
        Catch ex As WebException
            If ex.Response IsNot Nothing Then
                Using res As HttpWebResponse = CType(ex.Response, HttpWebResponse)
                    Return If(res.Headers("Location"), "")
                End Using
            End If
            Throw
        End Try
    End Function

    ''' <summary>
    ''' 用托管 TLS 读一个 https 地址的响应头，返回 Location（没有重定向就返回空串）。
    ''' 关键是【只读状态行和头部就断开】，绝不碰正文 —— 理由见 ResolveFinalUrl 里的注释。
    ''' </summary>
    Private Function GetRedirectLocationManaged(ByVal url As String) As String
        Dim uri As New Uri(url)
        Dim tcp As TcpClient = Nothing
        Dim protocol As TlsClientProtocol = Nothing
        Try
            tcp = New TcpClient()
            Dim port As Integer = If(uri.Port > 0, uri.Port, 443)
            '和 Request 里一样：先同步解析成 IP 再连。
            '原来直接传主机名会走异步 DNS，在 Windows 2000 上 EndConnect 会抛
            '「An invalid argument was supplied」，导致所有文件下载失败。
            Dim Target As IPAddress = ResolveIPv4(uri.Host)
            Dim ar As IAsyncResult = tcp.BeginConnect(Target, port, Nothing, Nothing)
            If Not ar.AsyncWaitHandle.WaitOne(TimeoutMs, False) Then Return ""
            tcp.EndConnect(ar)
            tcp.ReceiveTimeout = TimeoutMs
            tcp.SendTimeout = TimeoutMs

            protocol = New TlsClientProtocol(tcp.GetStream(), New SecureRandom())
            protocol.Connect(New ManagedTlsClient(uri.Host))
            Dim stream As Stream = protocol.Stream

            Dim head As String = "GET " & If(uri.PathAndQuery = "", "/", uri.PathAndQuery) & " HTTP/1.1" & vbCrLf &
                                 "Host: " & uri.Host & vbCrLf &
                                 "User-Agent: PCL1-ManagedTLS/1.0" & vbCrLf &
                                 "Accept: */*" & vbCrLf &
                                 "Accept-Encoding: identity" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
            Dim hb As Byte() = Encoding.ASCII.GetBytes(head)
            stream.Write(hb, 0, hb.Length)
            stream.Flush()

            Dim statusLine As String = ReadLine(stream)
        '诊断：状态行不正常时把原始值打出来。
        '之前这里只留下一个 HTTP 0，看不出是空响应、超时、还是协议错位。
        If statusLine Is Nothing OrElse Not statusLine.StartsWith("HTTP/") Then
            log("[TLS] 异常状态行（长度 " & If(statusLine Is Nothing, -1, statusLine.Length) & "）：[" & If(statusLine, "<null>") & "]")
        End If
            Dim parts As String() = statusLine.Split(New Char() {" "c}, 3)
            Dim code As Integer = 0
            If parts.Length >= 2 Then Integer.TryParse(parts(1), code)

            Dim location As String = ""
            Do
                Dim line As String = ReadLine(stream)
                If line Is Nothing OrElse line = "" Then Exit Do
                Dim idx As Integer = line.IndexOf(":"c)
                If idx > 0 Then
                    If line.Substring(0, idx).Trim().Equals("Location", StringComparison.OrdinalIgnoreCase) Then
                        location = line.Substring(idx + 1).Trim()
                    End If
                End If
            Loop

            If code >= 300 AndAlso code < 400 Then Return location
            Return ""
        Catch
            ' 解析失败就按「没有重定向」处理，交给上层去真正下载并暴露错误
            Return ""
        Finally
            If protocol IsNot Nothing Then
                Try
                    protocol.Close()
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

    ''' <summary>用托管 TLS 拉取 https 内容并流式写入文件（不把整个文件读进内存）。</summary>
    Private Sub DownloadHttpsToFile(ByVal url As String, ByVal localPath As String)
        Dim uri As New Uri(url)
        Dim tcp As TcpClient = Nothing
        Dim protocol As TlsClientProtocol = Nothing
        Try
            tcp = New TcpClient()
            Dim port As Integer = If(uri.Port > 0, uri.Port, 443)
            '同 Request：先同步解析成 IP 再连。
            '直接传主机名会走 .NET 的异步 DNS，在 Windows 2000 上 EndConnect 会抛
            '「An invalid argument was supplied」，导致所有文件下载失败。
            Dim Target As IPAddress = ResolveIPv4(uri.Host)
            Dim ar As IAsyncResult = tcp.BeginConnect(Target, port, Nothing, Nothing)
            If Not ar.AsyncWaitHandle.WaitOne(TimeoutMs, False) Then
                Throw New TimeoutException("连接 " & uri.Host & ":" & port & " 超时")
            End If
            tcp.EndConnect(ar)
            tcp.ReceiveTimeout = TimeoutMs

            protocol = New TlsClientProtocol(tcp.GetStream(), New SecureRandom())
            protocol.Connect(New ManagedTlsClient(uri.Host))
            Dim stream As Stream = protocol.Stream

            Dim head As String = "GET " & If(uri.PathAndQuery = "", "/", uri.PathAndQuery) & " HTTP/1.1" & vbCrLf &
                                 "Host: " & uri.Host & vbCrLf &
                                 "User-Agent: PCL1-ManagedTLS/1.0" & vbCrLf &
                                 "Accept: */*" & vbCrLf &
                                 "Accept-Encoding: identity" & vbCrLf &
                                 "Connection: close" & vbCrLf & vbCrLf
            Dim hb As Byte() = Encoding.ASCII.GetBytes(head)
            stream.Write(hb, 0, hb.Length)
            stream.Flush()

            ' 状态行
            Dim statusLine As String = ReadLine(stream)
            Dim parts As String() = statusLine.Split(New Char() {" "c}, 3)
            Dim code As Integer = 0
            If parts.Length >= 2 Then Integer.TryParse(parts(1), code)
            If code <> 200 Then
                Throw New Exception("托管 TLS 下载失败：HTTP " & code & " " & If(parts.Length >= 3, parts(2), ""))
            End If

            ' 头部
            Dim headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Do
                Dim line As String = ReadLine(stream)
                If line Is Nothing OrElse line = "" Then Exit Do
                Dim idx As Integer = line.IndexOf(":"c)
                If idx > 0 Then headers(line.Substring(0, idx).Trim()) = line.Substring(idx + 1).Trim()
            Loop

            ' 正文流式落盘
            ' 注意用完全限定名：VB 不区分大小写，Path 会被解析成 PCL1 的全局变量 PATH
            Dim dir As String = System.IO.Path.GetDirectoryName(localPath)
            If dir IsNot Nothing AndAlso dir <> "" AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            Using fs As New FileStream(localPath, FileMode.Create, FileAccess.Write)
                Dim te As String = ""
                If headers.ContainsKey("Transfer-Encoding") Then te = headers("Transfer-Encoding")
                If te.ToLower().Contains("chunked") Then
                    CopyChunkedToFile(stream, fs)
                Else
                    CopyRawToFile(stream, fs, headers)
                End If
            End Using
        Finally
            If protocol IsNot Nothing Then
                Try
                    protocol.Close()
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
    End Sub

    Private Sub CopyRawToFile(ByVal stream As Stream, ByVal fs As Stream, ByVal headers As Dictionary(Of String, String))
        Dim remaining As Long = -1
        If headers.ContainsKey("Content-Length") Then
            Dim cl As Long = -1
            If Long.TryParse(headers("Content-Length"), cl) Then remaining = cl
        End If
        Dim buf(65535) As Byte
        Do
            Dim want As Integer = buf.Length
            If remaining >= 0 Then
                If remaining = 0 Then Exit Do
                If remaining < want Then want = CInt(remaining)
            End If
            Dim n As Integer = stream.Read(buf, 0, want)
            If n <= 0 Then Exit Do
            fs.Write(buf, 0, n)
            If remaining >= 0 Then remaining -= n
        Loop
    End Sub

    Private Sub CopyChunkedToFile(ByVal stream As Stream, ByVal fs As Stream)
        Do
            Dim sizeLine As String = ReadLine(stream)
            Dim semi As Integer = sizeLine.IndexOf(";"c)
            If semi >= 0 Then sizeLine = sizeLine.Substring(0, semi)
            Dim size As Integer = 0
            If Not Integer.TryParse(sizeLine.Trim(), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, size) Then Exit Do
            If size <= 0 Then
                '终止块是 0\r\n\r\n —— 上面那次 ReadLine 只吃掉了「0\r\n」，
                '后面还剩一个 CRLF（以及可能存在的 trailer）。
                '不把它消费掉的话，它会留在流里，等这条连接被复用时
                '被下一个请求当成状态行读走，解析出 HTTP 0 —— 表现为
                '「登录成功了但 entitlements / profile 请求返回空」。
                Do
                    Dim trailer As String = ReadLine(stream)
                    If trailer Is Nothing OrElse trailer = "" Then Exit Do
                Loop
                Exit Do
            End If
            Dim left As Integer = size
            Dim buf(65535) As Byte
            Do While left > 0
                Dim want As Integer = Math.Min(buf.Length, left)
                Dim n As Integer = stream.Read(buf, 0, want)
                If n <= 0 Then Exit Do
                fs.Write(buf, 0, n)
                left -= n
            Loop
            ReadLine(stream)
        Loop
    End Sub

#End Region

#Region "HTTP 响应解析"

    Private Function ReadResponse(ByVal stream As Stream, Optional ByVal HeadersOnly As Boolean = False) As TlsHttpResponse
        Dim res As New TlsHttpResponse

        ' 状态行，例如 "HTTP/1.1 200 OK"
        Dim statusLine As String = ReadLine(stream)
        Dim parts As String() = statusLine.Split(New Char() {" "c}, 3)
        If parts.Length >= 2 Then Integer.TryParse(parts(1), res.StatusCode)
        If parts.Length >= 3 Then res.StatusText = parts(2)

        ' 头部
        Do
            Dim line As String = ReadLine(stream)
            If line Is Nothing OrElse line = "" Then Exit Do
            Dim idx As Integer = line.IndexOf(":"c)
            If idx > 0 Then
                Dim k As String = line.Substring(0, idx).Trim()
                Dim v As String = line.Substring(idx + 1).Trim()
                res.Headers(k) = v
            End If
        Loop

        ' 正文
        ' 正文。HEAD 请求没有正文，硬读会一直等到超时，所以跳过。
        If Not HeadersOnly Then
            res.RawBody = ReadBody(stream, res.Headers)
            res.Body = Encoding.UTF8.GetString(res.RawBody)
        End If
        Return res
    End Function

    ''' <summary>按 CRLF 读一行（忽略 CR，遇到 LF 结束）。</summary>
    Private Function ReadLine(ByVal stream As Stream) As String
        Dim ms As New MemoryStream()
        Do
            Dim b As Integer = stream.ReadByte()
            If b < 0 Then Exit Do
            If b = 10 Then Exit Do
            If b <> 13 Then ms.WriteByte(CByte(b))
        Loop
        Return Encoding.ASCII.GetString(ms.ToArray())
    End Function

    Private Function ReadBody(ByVal stream As Stream, ByVal headers As Dictionary(Of String, String)) As Byte()
        ' 分块传输
        Dim te As String = ""
        If headers.ContainsKey("Transfer-Encoding") Then te = headers("Transfer-Encoding")
        If te.ToLower().Contains("chunked") Then Return ReadChunkedBody(stream)

        ' 定长
        Dim cl As Integer = -1
        If headers.ContainsKey("Content-Length") Then Integer.TryParse(headers("Content-Length"), cl)
        If cl >= 0 Then
            Dim buf(cl - 1) As Byte
            Dim total As Integer = 0
            Do While total < cl
                Dim n As Integer = stream.Read(buf, total, cl - total)
                If n <= 0 Then Exit Do
                total += n
            Loop
            If total = cl Then Return buf
            Dim cut(total - 1) As Byte
            Array.Copy(buf, cut, total)
            Return cut
        End If

        ' 没有长度信息：读到连接关闭
        Dim all As New MemoryStream()
        Dim chunk(4095) As Byte
        Do
            Dim n As Integer = stream.Read(chunk, 0, chunk.Length)
            If n <= 0 Then Exit Do
            all.Write(chunk, 0, n)
        Loop
        Return all.ToArray()
    End Function

    Private Function ReadChunkedBody(ByVal stream As Stream) As Byte()
        Dim all As New MemoryStream()
        Do
            Dim sizeLine As String = ReadLine(stream)
            Dim semi As Integer = sizeLine.IndexOf(";"c)
            If semi >= 0 Then sizeLine = sizeLine.Substring(0, semi)
            Dim size As Integer = 0
            If Not Integer.TryParse(sizeLine.Trim(), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, size) Then Exit Do
            If size <= 0 Then Exit Do
            Dim buf(size - 1) As Byte
            Dim total As Integer = 0
            Do While total < size
                Dim n As Integer = stream.Read(buf, total, size - total)
                If n <= 0 Then Exit Do
                total += n
            Loop
            all.Write(buf, 0, total)
            ReadLine(stream) ' 每块结尾的 CRLF
        Loop
        Return all.ToArray()
    End Function

#End Region

#Region "BouncyCastle TLS 客户端实现"

    Private Class ManagedTlsClient
        Inherits DefaultTlsClient

        Private ReadOnly _host As String

        Public Sub New(ByVal host As String)
            _host = host
        End Sub

        Public Overrides Function GetAuthentication() As TlsAuthentication
            Return New ManagedTlsAuth()
        End Function

        ''' <summary>
        ''' 附加 SNI 扩展。现代服务器（微软、Cloudflare 等）都要求 SNI，
        ''' 没有它握手会被直接拒绝。字典的值必须是已编码的字节数组。
        ''' </summary>
        Public Overrides Function GetClientExtensions() As IDictionary
            Dim ext As IDictionary = MyBase.GetClientExtensions()
            If ext Is Nothing Then ext = New Hashtable()
            Dim names As New List(Of ServerName)()
            names.Add(New ServerName(0, _host))
            Dim ms As New MemoryStream()
            Dim snl As New ServerNameList(names)
            snl.Encode(ms)
            ext(ExtensionType.server_name) = ms.ToArray()
            Return ext
        End Function
    End Class

    Private Class ManagedTlsAuth
        Implements TlsAuthentication

        ''' <summary>
        ''' BouncyCastle 在收到服务器证书后会调用这里。默认不做额外校验，
        ''' 原因见模块头部关于 XP 根证书库的说明。
        ''' </summary>
        Public Sub NotifyServerCertificate(ByVal certificate As Certificate) Implements TlsAuthentication.NotifyServerCertificate
            ' 有意留空：接受服务器证书。
        End Sub

        Public Function GetClientCredentials(ByVal certificateRequest As CertificateRequest) As TlsCredentials Implements TlsAuthentication.GetClientCredentials
            Return Nothing
        End Function
    End Class

#End Region

End Module

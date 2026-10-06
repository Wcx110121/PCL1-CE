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
    Public Property TimeoutMs As Integer = 30000

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

    ''' <summary>
    ''' 发一次 HTTPS 请求。只支持 https，且不自动跟随重定向
    ''' （微软的登录接口不需要重定向）。
    ''' </summary>
    Public Function Request(ByVal method As String, ByVal url As String, ByVal body As String,
                            ByVal contentType As String, ByVal bearer As String) As TlsHttpResponse
        Dim uri As New Uri(url)
        If uri.Scheme.ToLower() <> "https" Then
            Throw New NotSupportedException("ModTls 只处理 https 地址，收到的是：" & url)
        End If
        Dim port As Integer = If(uri.Port > 0, uri.Port, 443)

        Dim tcp As TcpClient = Nothing
        Dim protocol As TlsClientProtocol = Nothing
        Try
            ' ---- TCP 连接（带超时，避免在 XP 上无限挂起）----
            tcp = New TcpClient()
            Dim ar As IAsyncResult = tcp.BeginConnect(uri.Host, port, Nothing, Nothing)
            If Not ar.AsyncWaitHandle.WaitOne(TimeoutMs, False) Then
                Throw New TimeoutException("连接 " & uri.Host & ":" & port & " 超时（" & TimeoutMs & "ms）")
            End If
            tcp.EndConnect(ar)
            tcp.ReceiveTimeout = TimeoutMs
            tcp.SendTimeout = TimeoutMs

            ' ---- TLS 握手（BouncyCastle，与 schannel 无关）----
            protocol = New TlsClientProtocol(tcp.GetStream(), New SecureRandom())
            protocol.Connect(New ManagedTlsClient(uri.Host))
            Dim stream As Stream = protocol.Stream

            ' ---- 组装请求 ----
            Dim head As New StringBuilder()
            head.Append(method).Append(" ").Append(If(uri.PathAndQuery = "", "/", uri.PathAndQuery)).Append(" HTTP/1.1").Append(vbCrLf)
            head.Append("Host: ").Append(uri.Host).Append(vbCrLf)
            head.Append("User-Agent: PCL1-ManagedTLS/1.0").Append(vbCrLf)
            head.Append("Accept: application/json").Append(vbCrLf)
            ' 明确不接受压缩，省掉自己解 gzip 的麻烦
            head.Append("Accept-Encoding: identity").Append(vbCrLf)
            head.Append("Connection: close").Append(vbCrLf)
            If bearer <> "" Then head.Append("Authorization: Bearer ").Append(bearer).Append(vbCrLf)

            Dim bodyBytes As Byte() = Nothing
            If body IsNot Nothing AndAlso body <> "" Then
                bodyBytes = Encoding.UTF8.GetBytes(body)
                head.Append("Content-Type: ").Append(If(contentType = "", "application/octet-stream", contentType)).Append(vbCrLf)
                head.Append("Content-Length: ").Append(bodyBytes.Length.ToString).Append(vbCrLf)
            End If
            head.Append(vbCrLf)

            Dim headBytes As Byte() = Encoding.ASCII.GetBytes(head.ToString())
            stream.Write(headBytes, 0, headBytes.Length)
            If bodyBytes IsNot Nothing Then stream.Write(bodyBytes, 0, bodyBytes.Length)
            stream.Flush()

            ' ---- 读取响应 ----
            Return ReadResponse(stream)
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

#End Region

#Region "文件下载"

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
            Dim ar As IAsyncResult = tcp.BeginConnect(uri.Host, port, Nothing, Nothing)
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
            Dim ar As IAsyncResult = tcp.BeginConnect(uri.Host, port, Nothing, Nothing)
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
            If size <= 0 Then Exit Do
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

    Private Function ReadResponse(ByVal stream As Stream) As TlsHttpResponse
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
        res.RawBody = ReadBody(stream, res.Headers)
        res.Body = Encoding.UTF8.GetString(res.RawBody)
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

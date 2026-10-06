Imports Ionic.Zip

Public Module modMain

#Region "声明"

    '常量
    ' 名称改为社区衍生版 PCL1-CE。
    ' 注意 APPLICATION_SHORT_NAME 必须保持 "PCL" —— 它不是显示名，
    ' 而是注册表路径（Software\PCL）和 launcher_profiles.json 里的标识，
    ' 改了会让已有用户的配置全部失效。
    Public Const VERSION_NAME As String = "1.0.9-CE"
    Public Const VERSION_CODE As Integer = 52
    Public Const MC_VERSION_CODE As Integer = 7
    ' 推荐源缓存格式的版本号。换了抓取接口之后内容要重新生成，从 2 提到 3，
    ' 让旧的缓存直接作废、启动时重新拉一次。
    Public Const PUSH_VERSION_CODE As Integer = 3
    Public Const MAINFORM_HEIGHT As Integer = 435
    Public Const MAINFORM_WIDTH As Integer = 760
    Public Const MAINFORM_NAME As String = "PCL1-CE"
    Public Const APPLICATION_SHORT_NAME As String = "PCL"
    Public Const APPLICATION_FULL_NAME As String = "PCL1-CE"
    Public Const DOWNLOADING_END As String = ".PCLdownloading"
    ''' <summary>
    ''' 检查启动器更新所用的 GitHub 仓库（owner/repo）。
    ''' 注意：Release 的 tag 需要写成版本号形式（例如 v1.0.10-CE），
    ''' 启动器会把 tag 里的数字段与 VERSION_NAME 逐段比较。
    ''' </summary>
    Public Const UPDATE_REPO As String = "Wcx110121/PCL1-CE"
    ''' <summary>
    ''' 发现新版本时打开的页面地址。
    ''' </summary>
    Public Const UPDATE_URL As String = "https://github.com/Wcx110121/PCL1-CE/releases/latest"
    Public MODE_DEBUG As Boolean = False
    Public MODE_OFFLINE As Boolean = False
    Public MODE_DEVELOPER As Boolean = False

    '各种路径
    ''' <summary>
    ''' 程序内嵌图片文件夹路径。
    ''' </summary>
    ''' <remarks></remarks>
    Public PATH_IMAGE As String = "pack://application:,,,/images/"
    ''' <summary>
    ''' 用户设置的下载文件夹路径。
    ''' </summary>
    ''' <remarks></remarks>
    Public PATH_DOWNLOAD As String
    ''' <summary>
    ''' Java路径。不包含“javaw.exe”，不以“\”结尾。
    ''' </summary>
    ''' <remarks></remarks>
    Public PATH_JAVA As String
    ''' <summary>
    ''' .minecraft 文件夹路径。以“\”结尾。
    ''' </summary>
    ''' <remarks></remarks>
    Public PATH_MC As String

    '窗体
    Public frmStart As formStart
    Public frmMain As New formMain
    Public frmHomeLeft As New formHomeLeft
    Public frmHomeRight As New formHomeRight
    Public frmDownloadLeft As New formDownloadLeft
    Public frmDownloadRight As New formDownloadRight
    Public frmSetup As formSetup
    '尚未使用的窗体
    Public frmManageLeft As formManageLeft
    Public frmManageRight As formManageLeft
    Public frmHelpLeft As formHelpLeft
    Public frmHelpRight As formHelpLeft

    '通用信息
    ''' <summary>
    ''' 是否启用基础控件动画。
    ''' </summary>
    ''' <remarks></remarks>
    Public UseControlAnimation As Boolean = False
    ''' <summary>
    ''' 程序加载耗时的计时器。
    ''' </summary>
    ''' <remarks></remarks>
    Public LoadTimeCost As Integer
    ''' <summary>
    ''' 正版登录结果。可以通过判断它是否为空来确定正版登录是否成功。
    ''' </summary>
    ''' <remarks></remarks>
    Public LoginResult As String = ""
    ''' <summary>
    ''' 所有Minecraft版本的列表。
    ''' </summary>
    ''' <remarks></remarks>
    Public VersionsList As New Dictionary(Of VersionSwapState, ArrayList) From {{VersionSwapState.UNKNOWN, New ArrayList}, {VersionSwapState.NORMAL, New ArrayList}, {VersionSwapState.SWAP, New ArrayList}, {VersionSwapState.OLD, New ArrayList}, {VersionSwapState.WRONG, New ArrayList}}
    Private _PathEnv As String = ""
    ''' <summary>
    ''' Path环境变量。
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property PathEnv As String
        Get
            If _PathEnv = "" Then _PathEnv = Environment.GetEnvironmentVariable("Path")
            Return _PathEnv
        End Get
        Set(ByVal value As String)
            ' 原先写成 _PathEnv = PathEnv（把当前值写回自己），传入的 value 被丢弃，
            ' 于是 SetJavaEnvironment 刚配置好的 PATH 不生效，之后读到的仍是旧值，
            ' 表现为每次启动都判定「Java 不在环境变量里」并反复尝试配置环境变量。
            _PathEnv = value
        End Set
    End Property
    ''' <summary>
    ''' 是否正在播放音乐。
    ''' </summary>
    ''' <remarks></remarks>
    Public IsPlayingMusic As Boolean = False
    ''' <summary>
    ''' 是否允许收集操作信息。
    ''' </summary>
    ''' <remarks></remarks>
    ''' <summary>
    ''' 固定为 False：事件只写进本地日志（调试模式下可见），不做任何网络上报。
    ''' </summary>
    Public AllowFeedback As Boolean = False

#End Region

#Region "枚举"

    ''' <summary>
    ''' 加载状态，如加载中、成功、失败等。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum LoadState As Byte
        ''' <summary>
        ''' 尚未开始加载，正在等待
        ''' </summary>
        ''' <remarks></remarks>
        Waiting = 0
        ''' <summary>
        ''' 正在加载中
        ''' </summary>
        ''' <remarks></remarks>
        Loading = 1
        ''' <summary>
        ''' 加载成功
        ''' </summary>
        ''' <remarks></remarks>
        Loaded = 2
        ''' <summary>
        ''' 加载失败
        ''' </summary>
        ''' <remarks></remarks>
        Failed = 3
    End Enum
    ''' <summary>
    ''' 提示信息种类，如警告、完成、错误等。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum HintState As Integer
        ''' <summary>
        ''' 信息，通常是蓝色的“i”。
        ''' </summary>
        ''' <remarks></remarks>
        Info = 0
        ''' <summary>
        ''' 已完成，通常是绿色的“√”。
        ''' </summary>
        ''' <remarks></remarks>
        Finish = 1
        ''' <summary>
        ''' 警告，通常是黄色的“！”。
        ''' </summary>
        ''' <remarks></remarks>
        Warn = 2
        ''' <summary>
        ''' 错误，通常是红色的“×”。
        ''' </summary>
        ''' <remarks></remarks>
        Critical = 3
    End Enum
    ''' <summary>
    ''' Minecraft登陆方式。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum LoginMethods As Integer
        ''' <summary>
        ''' 正版登录。
        ''' </summary>
        ''' <remarks></remarks>
        Mojang = 1
        ''' <summary>
        ''' 未知。这用来标记未加载时的状态。
        ''' </summary>
        ''' <remarks></remarks>
        Unknown = 2
    End Enum
    ''' <summary>
    ''' Minecraft版本检查结果。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum VersionCheckState As Byte
        ''' <summary>
        ''' 尚未检查。
        ''' </summary>
        ''' <remarks></remarks>
        NOT_LOAD = 0
        ''' <summary>
        ''' 已经检查，没有问题。
        ''' </summary>
        ''' <remarks></remarks>
        NO_PROBLEM = 1
        ''' <summary>
        ''' 文件夹 或 Json与Jar 均不存在。
        ''' </summary>
        ''' <remarks></remarks>
        EMPTY_FOLDER = 2
        ''' <summary>
        ''' Json不存在，但是Jar存在。
        ''' </summary>
        ''' <remarks></remarks>
        JSON_NOT_EXIST = 3
        ''' <summary>
        ''' Json存在但是无法读取。
        ''' </summary>
        ''' <remarks></remarks>
        JSON_CANT_READ = 4
        ''' <summary>
        ''' 依赖版本文件夹不存在。
        ''' </summary>
        ''' <remarks></remarks>
        INHERITS_NOT_EXIST = 5
        ''' <summary>
        ''' 依赖版本检查出错。
        ''' </summary>
        ''' <remarks></remarks>
        INHERITS_EXCEPTION = 6
        ''' <summary>
        ''' Json存在，但是主要Jar不存在。
        ''' </summary>
        ''' <remarks></remarks>
        JAR_NOT_EXIST = 7
    End Enum
    ''' <summary>
    ''' Minecraft版本类型。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum MCVersionType As Byte
        ''' <summary>
        ''' 未获取版本或版本获取失败。
        ''' </summary>
        ''' <remarks></remarks>
        UNKNOWN = 0
        ''' <summary>
        ''' 快照和预览版。
        ''' </summary>
        ''' <remarks></remarks>
        SNAPSHOT = 1
        ''' <summary>
        ''' 正式版。
        ''' </summary>
        ''' <remarks></remarks>
        RELEASE = 2
        ''' <summary>
        ''' 愚人节版。
        ''' </summary>
        ''' <remarks></remarks>
        FOOL = 6
        ''' <summary>
        ''' OptiFine版。
        ''' </summary>
        ''' <remarks></remarks>
        OPTIFINE = 3
        ''' <summary>
        ''' Forge版。
        ''' </summary>
        ''' <remarks></remarks>
        FORGE = 4
        ''' <summary>
        ''' 发布时间在2012年及更早的版本。
        ''' </summary>
        ''' <remarks></remarks>
        OLD = 5
    End Enum
    ''' <summary>
    ''' 版本折叠分类。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum VersionSwapState As Byte
        ''' <summary>
        ''' 尚未确认。
        ''' </summary>
        ''' <remarks></remarks>
        UNKNOWN = 255
        ''' <summary>
        ''' 正常，无需折叠。
        ''' </summary>
        ''' <remarks></remarks>
        NORMAL = 0
        ''' <summary>
        ''' 折叠。
        ''' </summary>
        ''' <remarks></remarks>
        SWAP = 1
        ''' <summary>
        ''' 老版本。
        ''' </summary>
        ''' <remarks></remarks>
        OLD = 2
        ''' <summary>
        ''' 错误的版本。
        ''' </summary>
        ''' <remarks></remarks>
        WRONG = 3
    End Enum

#End Region

#Region "类"

    ''' <summary>
    ''' 提示信息种类转换。
    ''' </summary>
    ''' <remarks></remarks>
    Public Class HintConverter

        ''' <summary>
        ''' 提示信息文本。
        ''' </summary>
        ''' <remarks></remarks>
        Public Text As String

        ''' <summary>
        ''' 提示信息种类。
        ''' </summary>
        ''' <remarks></remarks>
        Public Type As HintState = 0

        Public Sub New(ByVal Text As String)
            Me.Text = Text
        End Sub
        Public Sub New(ByVal Text As String, ByVal Type As HintState)
            Me.Text = Text
            Me.Type = Type
        End Sub

        Public Shared Widening Operator CType(ByVal Text As String) As HintConverter
            Return New HintConverter(Text)
        End Operator
        Public Shared Widening Operator CType(ByVal Conv As HintConverter) As Color
            Select Case Conv.Type
                Case HintState.Info
                    Return Color.FromRgb(26, 148, 252)
                Case HintState.Finish
                    Return Color.FromRgb(29, 160, 29)
                Case HintState.Warn
                    Return Color.FromRgb(216, 137, 8)
                Case Else
                    Return Color.FromRgb(255, 46, 0)
            End Select
        End Operator
        Public Shadows Function ToString() As String
            Return "[" & {"Hint", "Finish", "Warn", "Critical"}(Type) & "] " & Text
        End Function

        ''' <summary>
        ''' 获取目前提示种类的英文名。
        ''' </summary>
        ''' <returns>提示种类的英文名，如“Critical”。</returns>
        ''' <remarks></remarks>
        Public Function GetTypeName() As String
            Return HintState.GetName(Type.GetType, Type)
        End Function

    End Class

    ''' <summary>
    ''' 一个不包含UI支持库的Minecraft版本类。
    ''' </summary>
    ''' <remarks></remarks>
    Public Class MCVersion
        Implements IComparable(Of MCVersion)
        ''' <summary>
        ''' 这些版本信息是否都是从已经配置好的文件中读取的。
        ''' </summary>
        ''' <remarks></remarks>
        Public LoadedByFile As Boolean = True
        ''' <summary>
        ''' 版本折叠分类。
        ''' </summary>
        ''' <remarks></remarks>
        Public SwapType As VersionSwapState = VersionSwapState.UNKNOWN
        ''' <summary>
        ''' 对应的Minecraft版本。这与Forge等无关。
        ''' </summary>
        ''' <remarks></remarks>
        Public Version As String = "未知"
        ''' <summary>
        ''' 版本的发布时间。用于鉴别版本号。
        ''' </summary>
        ''' <remarks></remarks>
        Public ReleaseTime As String = "1970-01-01T00:00:00"
        ''' <summary>
        ''' 主版本号（如1.9.2的9）。快照版为0。
        ''' </summary>
        ''' <remarks></remarks>
        Public MainVersionCode As Integer = 0
        ''' <summary>
        ''' Minecraft版本种类。
        ''' </summary>
        ''' <remarks></remarks>
        Public Type As MCVersionType = MCVersionType.UNKNOWN

        ''' <summary>
        ''' Logo的内容。用于ListItem显示。
        ''' </summary>
        ''' <remarks></remarks>
        Public Logo As String = PATH_IMAGE & "Block-Grass.png"
        ''' <summary>
        ''' 描述内容。用于ListItem显示。
        ''' </summary>
        ''' <remarks></remarks>
        Public Description As String = ""

        ''' <summary>
        ''' 版本检查结果。在检查版本时完成。
        ''' </summary>
        ''' <remarks></remarks>
        Public VersionCheckResult As VersionCheckState = VersionCheckState.NOT_LOAD
        ''' <summary>
        ''' 该版本的依赖版本。在检查版本时完成。
        ''' </summary>
        ''' <remarks></remarks>
        Public InheritVersion As String = ""
        ''' <summary>
        ''' 该版本的资源文件号。在检查版本时完成。
        ''' </summary>
        ''' <remarks></remarks>
        Public Assets As String

        '自处理属性

        ''' <summary>
        ''' 文件夹名称，或者说是这个版本的名称。
        ''' </summary>
        ''' <remarks></remarks>
        Public Name As String

        Private _Json As JObject
        ''' <summary>
        ''' Json对象。
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property Json As JObject
            Get
                If IsNothing(_Json) Then ReadJson()
                Return If(_JsonText = "ERROR", "", _Json)
            End Get
        End Property

        Private _JsonText As String = ""
        ''' <summary>
        ''' Json文件的内容。
        ''' </summary>
        ''' <remarks></remarks>
        Public ReadOnly Property JsonText As String
            Get
                If _JsonText = "" Then ReadJson()
                Return If(_JsonText = "ERROR", "", _JsonText)
            End Get
        End Property

        ''' <summary>
        ''' 完整的文件夹地址。以“\”结尾。
        ''' </summary>
        ''' <remarks></remarks>
        Public ReadOnly Property Path As String
            Get
                If Name = "" Then Throw New Exception("没有指定本版本的文件夹。")
                Return PATH_MC & "versions\" & Name & "\"
            End Get
        End Property

        '事件

        Public Sub New(ByVal FolderName As String)
            Name = FolderName
        End Sub
        Private Sub ReadJson()
            '读取Json
            For Each Encode As Encoding In {Encoding.Default, Encoding.Unicode, New UTF8Encoding(False), Encoding.ASCII}
                Try
                    _JsonText = ReadFileToEnd(Path & Name & ".json", Encode)
                    If _JsonText = "" Then GoTo Fail
                    _Json = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(_JsonText), JObject)
                    Exit Sub
                Catch
                    '编码错误
                    _JsonText = "ERROR"
                End Try
NextFormat:
            Next
Fail:
            '各种编码都没戏了
            log("[System] Json文件读取失败：" & Path & Name & ".json", True)
            Throw New FileFormatException("Json文件读取失败：" & Path & Name & ".json")
        End Sub

        Public Overrides Function ToString() As String
            Return Name & " / " & GetStringFromEnum(SwapType) & If(Version = Name, "", " / " & Version) & If(VersionCheckResult = VersionCheckState.NO_PROBLEM, "", " / " & GetStringFromEnum(VersionCheckResult))
        End Function
        Public Overloads Function CompareTo(ByVal Other As MCVersion) As Integer Implements IComparable(Of MCVersion).CompareTo
            If IsNothing(Other) Then Return 1
            Return String.Compare(Me.Name, Other.Name)
        End Function

    End Class

#End Region

#Region "登录"

    ''' <summary>
    ''' 把微软登录结果整理成内部统一的登录结果 JSON。
    ''' 各处都通过 ReadJson(LoginResult)("selectedProfile")("name") 取玩家信息
    ''' （皮肤加载、正版启动参数、界面显示），统一成同一个结构可以避免大范围改动。
    ''' </summary>
    Private Function BuildCompatLoginJson(ByVal Result As ModAuth.MSALoginResult) As String
        Dim json As New JObject
        json("accessToken") = Result.MinecraftToken
        json("clientToken") = ""
        Dim profile As New JObject
        profile("id") = Result.PlayerUUID
        profile("name") = Result.PlayerName
        json("selectedProfile") = profile
        Return json.ToString(Newtonsoft.Json.Formatting.None)
    End Function

    ''' <summary>
    ''' 保存一次成功的微软登录结果。
    ''' </summary>
    Private Sub SaveMSALoginResult(ByVal Result As ModAuth.MSALoginResult)
        WriteReg("MSARefreshToken", SerAdd(Result.MsaRefreshToken))
        WriteReg("MojangPlayerName", Result.PlayerName)
        WriteReg("MojangPlayerUUID", Result.PlayerUUID)
        ' 该键位在当前登录体系里没有对应值，置空但保留，避免读取方拿到 Nothing
        WriteReg("ClientToken", "")
        LoginResult = BuildCompatLoginJson(Result)
        Try
            WriteIni("cache\skin\UUID", Result.PlayerName, Result.PlayerUUID)
        Catch
        End Try
    End Sub

    ''' <summary>防止用户重复点击导致同时跑起多个登录流程。</summary>
    Private MSALoginRunning As Boolean = False

    ''' <summary>
    ''' 交互式微软账号登录（OAuth 2.0 设备码流程）。必须在后台线程中调用。
    ''' 流程：申请设备码 → 弹窗让用户去 microsoft.com/link 输入 → 轮询等待授权
    '''       → Xbox Live → XSTS → Minecraft 服务 → 查询正版档案。
    ''' </summary>
    Public Function MSALoginInteractiveFlow() As Boolean
        If MSALoginRunning Then
            ShowHint("微软登录已经在进行中了，请先完成当前这次授权")
            Return False
        End If
        MSALoginRunning = True
        Try
            Return MSALoginInteractiveFlowCore()
        Finally
            MSALoginRunning = False
        End Try
    End Function

    Private Function MSALoginInteractiveFlowCore() As Boolean
        If MODE_OFFLINE Then
            ShowHint(New HintConverter("没有网络连接，无法登录", HintState.Warn))
            Return False
        End If

        ' 检查 client_id 是否已配置。未配置时直接提供「粘贴 ID」的入口，
        ' 而不是让用户自己去建文件、手工填 —— 那个流程太绕了。
        If ModAuth.ClientId = "" Then
            Dim choice As Integer = MyMsgbox(
                "微软账号登录需要一个「应用(客户端) ID」—— 它是启动器向微软报备的身份标识，" & vbCrLf &
                "类似某个 App 想支持「微信登录」就得先去微信开放平台注册。" & vbCrLf & vbCrLf &
                "这个 ID 不是密码，可以公开，注册免费、大约 5 分钟。" & vbCrLf & vbCrLf &
                "如果你已经有 ID，点「粘贴 ID」；还没有就点「查看注册步骤」。",
                "微软登录尚未配置", "粘贴 ID", "查看注册步骤", "取消")
            If choice = 1 Then
                If Not AskForClientId() Then Return False
            ElseIf choice = 2 Then
                MyMsgbox(ModAuth.ClientIdHelpText(), "注册步骤", "知道了")
                Return False
            Else
                Return False
            End If
        End If

        ' 如果之前授权过（存了 refresh token），先试一次静默刷新。
        ' 这样失败重试时不用每次都让用户拿手机重新走一遍授权 —— 排查问题时这点很关键。
        Dim CachedRefresh As String = SerRemove(ReadReg("MSARefreshToken", ""))
        If CachedRefresh <> "" Then
            log("[Login] 发现已保存的微软凭据，先尝试静默登录")
            Dim Silent As ModAuth.MSALoginResult = ModAuth.MSALoginByRefresh(CachedRefresh)
            If Silent.Success Then
                SaveMSALoginResult(Silent)
                RefreshLoginUI(Silent)
                ShowHint(New HintConverter("已用保存的凭据登录：" & Silent.PlayerName, HintState.Finish))
                Return True
            End If
            log("[Login] 静默登录失败，转为交互式授权：" & Silent.ErrorMessage)
        End If

        ' 清掉可能已失效的旧令牌
        WriteReg("AccessToken", "")
        WriteReg("ClientToken", "")

        ' 第一步：申请设备码
        Dim Info As ModAuth.MSADeviceCodeInfo
        Try
            Info = ModAuth.MSARequestDeviceCode()
        Catch ex As Exception
            ShowHint(New HintConverter("申请登录代码失败：" & ex.Message, HintState.Critical))
            Return False
        End Try

        ' 第二步：把代码展示给用户。
        ' MyMsgbox 在后台线程被调用时会自动排队到主线程显示、并阻塞等待点击，正是这里需要的行为。
        ' 顺手把代码放进剪贴板 —— 设备码形如 ABCD-EFGH，手抄很容易出错。
        Dim Copied As Boolean = False
        Try
            frmMain.Dispatcher.Invoke(Sub() System.Windows.Clipboard.SetText(Info.UserCode))
            Copied = True
        Catch ex As Exception
            log("[Login] 复制设备码到剪贴板失败：" & ex.Message)
        End Try

        Dim Caption As String =
            "请用手机或另一台电脑打开下面的网址，输入这个代码完成授权：" & vbCrLf & vbCrLf &
            "        " & ModAuth.URL_MSA_LINK & vbCrLf & vbCrLf &
            "代码：" & Info.UserCode & If(Copied, "（已复制到剪贴板）", "") & vbCrLf & vbCrLf &
            "代码 " & (Info.ExpiresIn \ 60) & " 分钟内有效。" & vbCrLf &
            "在网页上完成授权后，回到这里点击「我已完成」。"
        If MyMsgbox(Caption, "微软账号登录", "我已完成", "取消") <> 1 Then
            ShowHint("已取消微软账号登录")
            Return False
        End If

        ' 第三步：等待授权并换取 Minecraft 令牌
        ShowHint("正在等待微软授权结果…")
        Dim Result As ModAuth.MSALoginResult = ModAuth.MSALoginWithDeviceCode(Info, AddressOf MSALoginStatus)
        ' 【重要】即使后面的 Xbox / Minecraft 步骤失败，也要把微软账号的 refresh token 存下来。
        ' 否则用户每次重试都得重新用手机授权一遍，排查问题时极其折磨人。
        ' 存下之后，下次重试会先走静默刷新，只有 refresh token 失效时才需要重新授权。
        If Result.MsaRefreshToken <> "" Then
            WriteReg("MSARefreshToken", SerAdd(Result.MsaRefreshToken))
        End If
        If Not Result.Success Then
            ' 登录失败时把本机的 TLS 环境一并显示出来。
            ' XP 上最常见的两类失败是「schannel 不支持 TLS 1.2」和「根证书过旧」，
            ' 只有把环境信息摆出来，用户才知道该去装补丁还是该开证书兼容模式。
            MyMsgbox("微软登录失败：" & vbCrLf & vbCrLf & Result.ErrorMessage & vbCrLf & vbCrLf &
                     "—— 本机环境 ——" & vbCrLf & ModAuth.DescribeLocalTls(),
                     "微软登录失败", "知道了")
            Return False
        End If

        ' 第四步：保存并刷新界面
        SaveMSALoginResult(Result)
        RefreshLoginUI(Result)
        ShowHint(New HintConverter("微软账号登录成功：" & Result.PlayerName, HintState.Finish))
        Return True
    End Function

    ''' <summary>
    ''' 登录成功后刷新界面与正版皮肤（静默登录和交互登录共用）。
    ''' </summary>
    Private Sub RefreshLoginUI(ByVal Result As ModAuth.MSALoginResult)
        If frmHomeRight Is Nothing Then Return
        Try
            frmHomeRight.Dispatcher.Invoke(Sub()
                                               frmHomeRight.LoginMethod = LoginMethods.Mojang
                                               frmHomeRight.StartButtonRefresh()
                                           End Sub)
        Catch ex As Exception
            log("[Login] 刷新登录界面失败：" & ex.Message)
        End Try
        ' 加载正版皮肤（失败不影响登录结果）
        Try
            Dim SkinAddress As String = DownloadSkin(Result.PlayerUUID)
            If SkinAddress <> "" Then
                frmHomeRight.Dispatcher.Invoke(Sub() frmHomeRight.LoadMojangSkin(SkinAddress))
            End If
        Catch ex As Exception
            log("[Login] 加载皮肤失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 弹出输入框让用户粘贴 client_id 并写入配置文件。返回 True 表示配置完成。
    ''' </summary>
    Private Function AskForClientId() As Boolean
        Dim Id As String = ""
        Try
            frmMain.Dispatcher.Invoke(Sub()
                                          Id = Microsoft.VisualBasic.Interaction.InputBox(
                                              "请粘贴 Azure 应用(客户端) ID，然后点「确定」：" & vbCrLf & vbCrLf &
                                              "（形如 12345678-1234-1234-1234-123456789abc）",
                                              "配置微软登录", "")
                                      End Sub)
        Catch ex As Exception
            log("[Login] 打开 client_id 输入框失败：" & ex.Message)
            Return EditClientIdWithNotepad()
        End Try

        Id = If(Id, "").Trim()
        If Id = "" Then Return False

        Try
            System.IO.Directory.CreateDirectory(PATH & "PCL1_CE")
            System.IO.File.WriteAllText(PATH & ModAuth.CLIENT_ID_FILE, Id, New System.Text.UTF8Encoding(False))
            ModAuth.ReloadClientId()
            log("[Login] client_id 已保存")
            ShowHint(New HintConverter("微软登录已配置好，正在开始登录…", HintState.Finish))
            Return True
        Catch ex As Exception
            MyMsgbox("保存 client_id 失败：" & ex.Message & vbCrLf & vbCrLf &
                     "请改用手工方式，把 ID 写进下面这个文件（一行纯文本）：" & vbCrLf &
                     PATH & ModAuth.CLIENT_ID_FILE, "配置微软登录", "知道了")
            Return False
        End Try
    End Function

    ''' <summary>输入框不可用时的兜底：用记事本打开配置文件让用户自己填。</summary>
    Private Function EditClientIdWithNotepad() As Boolean
        Try
            Dim configPath As String = PATH & ModAuth.CLIENT_ID_FILE
            System.IO.Directory.CreateDirectory(PATH & "PCL1_CE")
            If Not System.IO.File.Exists(configPath) Then
                System.IO.File.WriteAllText(configPath, "", New System.Text.UTF8Encoding(False))
            End If
            Process.Start("notepad.exe", """" & configPath & """")
            MyMsgbox("已用记事本打开配置文件。" & vbCrLf & vbCrLf &
                     "请把申请到的 client_id 粘贴进去、保存，然后重新点击登录。",
                     "配置微软登录", "知道了")
        Catch ex As Exception
            MyMsgbox("打开配置方式失败：" & ex.Message, "配置微软登录", "知道了")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 登录过程的状态回调（在后台线程执行）。
    ''' </summary>
    Private Sub MSALoginStatus(ByVal Message As String)
        log("[Login] " & Message)
    End Sub


#End Region

#Region "线程池"
    Public PoolCount As Integer = 0 '目前运行中的线程计数
    Public Const POOL_MAXCOUNT As Integer = 15 '最大线程数

    Public Pool As New ArrayList '线程池
    Public Sub PoolLoader()
        On Error Resume Next
        Do While True
            If Pool.Count > 0 Then
ReSearch:
                For i = 0 To Pool.Count - 1
                    If IsNothing(Pool(i)) Then
                        Pool.RemoveAt(i)
                        Exit For
                    End If
                    Select Case Pool(i).ThreadState
                        Case System.Threading.ThreadState.Unstarted
                            If PoolCount < POOL_MAXCOUNT Then
                                '线程尚未启动
                                Pool(i).Start()
                                PoolCount = PoolCount + 1
                            End If
                        Case System.Threading.ThreadState.Stopped, System.Threading.ThreadState.Aborted
                            '线程已经停止
                            PoolCount = PoolCount - 1
                            Pool.RemoveAt(i)
                            i = i - 1
                    End Select
                    If i >= Pool.Count - 1 Then Exit For
                Next i
            End If
            Thread.Sleep(20)
        Loop
    End Sub

    ''' <summary>
    ''' 加载线程：Java文件夹检测。它会检测PATH_JAVA的值，如果无Java则初始化。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub PoolJavaFolder(IsJustFind As Boolean)

        '初始化
        PATH_JAVA = If(IsJustFind, "", ReadReg("SetupJavaPath"))

        Try

            '如果注册表已经存在 Java 的正确路径就结束
            Dim Env As String = PathEnv
            If File.Exists(PATH_JAVA & "\javaw.exe") And (File.Exists(PATH_JAVA & "\jli.dll") Or PATH_JAVA.Contains("javapath")) Then
                log("[Pool] Java 路径：" & PATH_JAVA)
                Exit Sub
            End If

            '检查环境变量中的 Java
            Dim EachPath As String() = Split(Env, ";")
            For Each PathFind As String In EachPath
                '格式化字符串，保证不以“\”结尾
                If PathFind.EndsWith("\") Then PathFind = Left(PathFind, Len(PathFind) - 1)
                '检查有效性
                If File.Exists(PathFind & "\javaw.exe") And (File.Exists(PathFind & "\jli.dll") Or PathFind.Contains("javapath")) Then
                    PATH_JAVA = PathFind
                    log("[Pool] 环境变量中找到 Java：" & PATH_JAVA)
                    WriteReg("SetupJavaPath", PATH_JAVA)
                    Exit Sub
                End If
            Next

            '人工查找
            log("[Pool] 遍历寻找 Java 开始")
            Dim Result As String
            '循环每个盘
            For Each Disk As DriveInfo In DriveInfo.GetDrives()
                Result = SearchJava(Disk.Name)
                '如果找到了的话就跳出，并且设置环境变量
                If Not Result = "" Then
                    PATH_JAVA = Mid(Result, 1, Result.LastIndexOf("\"))
                    GoTo FinishSearch
                End If
            Next
            '查找当前启动器目录
            Result = SearchJava(PATH, True)
            If Not Result = "" Then
                PATH_JAVA = Mid(Result, 1, Result.LastIndexOf("\"))
                GoTo FinishSearch
            End If

            '人工查找失败
            log("[Pool] 遍历寻找 Java 失败")
            PATH_JAVA = ""
            ShowHint(New HintConverter("未找到可用的 Java", HintState.Warn))
            Exit Sub

        Catch ex As Exception
            ExShow(ex, "查找 Java 时出错", ErrorLevel.MsgboxAndFeedback)
        End Try

FinishSearch:
        If Not IsJustFind Then SetJavaEnvironment()

    End Sub
    ''' <summary>
    ''' 检查指定路径下的文件夹并且模糊搜索Java。这不会搜索全部路径。
    ''' </summary>
    ''' <param name="path">开始搜索的起始路径</param>
    ''' <param name="fullSearch">搜索当前文件夹下的全部文件夹（这不会传递到子级文件夹）</param>
    ''' <returns>搜索到的Java路径，如果失败则为空</returns>
    ''' <remarks></remarks>
    Private Function SearchJava(ByVal path As String, Optional ByVal fullSearch As Boolean = False) As String
        Try
            SearchJava = ""
            Dim AllPath() As String
            Dim LastEntry As String '文件夹或文件名
            If Directory.Exists(path) Then
                '该目录存在
                AllPath = Directory.GetFileSystemEntries(path)
                For Each Entry As String In AllPath
                    LastEntry = Entry.Split("\")(Entry.Split("\").Length - 1) '获取文件夹或文件名
                    Dim SearchEntry = LastEntry.ToLower.Replace(" ", "") '用于搜索的字符串
                    If fullSearch Or SearchEntry.Contains("java") Or SearchEntry.Contains("jdk") Or SearchEntry.Contains("jre") Or SearchEntry.Contains("bin") Or SearchEntry.Contains("mc") Or SearchEntry.Contains("minecraft") Or SearchEntry.Contains("program") Or SearchEntry.Contains("我的世界") Or SearchEntry.Contains("net") Or SearchEntry.Contains("runtime") Or SearchEntry.Contains("oracle") Or SearchEntry.Contains("1.") Or SearchEntry.Contains("启") Then
                        If File.Exists(Entry) Then
                            '如果是文件
                            If LastEntry = "javaw.exe" And File.Exists(Mid(Entry, 1, Entry.LastIndexOf("\")) & "\jli.dll") Then
                                '找到Java
                                SearchJava = Entry
                                Exit For
                            End If
                        ElseIf Directory.Exists(Entry) Then
                            '如果是文件夹
                            Dim Result As String = SearchJava(Entry)
                            If Not Result = "" Then Return Result
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            ExShow(ex, "遍历查找Java时出错", ErrorLevel.Slient)
            '防止无文件夹权限而导致崩溃
            SearchJava = ""
        End Try
    End Function

    ''' <summary>
    ''' 加载线程：Minecraft文件夹检测。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub PoolMinecraftFolder()
        Try
            '等待版本列表加载结束
            ' 加超时保护：这段等待运行在 UI 线程上（首次使用向导 formGuild 与设置页的按钮都会直接调用本方法），
            ' 一旦标志位因任何原因没有复位，界面就会永久卡死。超过 30 秒后放弃等待、继续执行。
            Dim VersionWaitStart As Integer = Environment.TickCount
            Do While IsPoolVersionListRunning AndAlso Environment.TickCount - VersionWaitStart < 30000
                Thread.Sleep(25)
            Loop

            Dim MojangPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\"
            Dim AllFoldersList As New ArrayList

            '加载可用文件夹地址
            Dim AllFolders As String = ReadIni("setup", "LaunchFolders", "")
            If Not AllFolders = "" Then
                '如果存在可用文件夹地址，则再次检查它们
                Dim NewFolderList As New ArrayList
                For Each Dir As String In AllFolders.Split("|")
                    If CheckDirectoryPermission(Dir) Then
                        NewFolderList.Add(Dir)
                        AllFoldersList.Add(Dir)
                    End If
                Next
                AllFolders = Join(NewFolderList.ToArray, "|")
                WriteIni("setup", "LaunchFolders", AllFolders)
            End If

            '加载设置中的Minecraft文件夹地址
            PATH_MC = ReadIni("setup", "LaunchFolderSelect", "")
            If CheckDirectoryPermission(MojangPath & "versions\") Then
                If Not Directory.GetDirectories(MojangPath & "versions\").Length = 0 Then AllFoldersList.Add(MojangPath)
            End If

            '如果为当前目录则自动创建
            If PATH_MC = PATH & ".minecraft\" Then
                If Not CheckDirectoryPermission(PATH_MC) Then
                    Directory.CreateDirectory(PATH_MC)
                    Directory.CreateDirectory(PATH_MC & "versions\")
                End If
                GoTo FinishMinecraftFolderCheck
            End If

            '如果在可用文件夹列表且有效则结束
            If AllFoldersList.Contains(PATH_MC) And CheckDirectoryPermission(PATH_MC) Then GoTo FinishMinecraftFolderCheck

            '如果是用户设置的路径，则显示一个通知
            If Not PATH_MC = "" Then ShowHint("原先的 Minecraft 文件夹路径已失效：" & PATH_MC)

            '优先尝试设置为当前文件夹
            If CheckDirectoryPermission(PATH & ".minecraft\versions") Then
                PATH_MC = PATH & ".minecraft\"
                GoTo FinishMinecraftFolderCheck
            End If

            '优先尝试设置为官启文件夹
            If AllFoldersList.Contains(MojangPath) Then
                If MyMsgbox("官方启动器的 Minecraft 文件夹下存在 " & Directory.GetDirectories(MojangPath & "versions").Length & " 个版本，是否使用官方启动器的文件夹？" & vbCrLf & "你可以在设置页面更改这个设置。", "Minecraft 文件夹确认", "确定", "取消") = 1 Then
                    '使用官启文件夹
                    PATH_MC = MojangPath
                    GoTo FinishMinecraftFolderCheck
                End If
            End If
            AllFoldersList.Remove(MojangPath)

            '从列表中查找新的目录
            For Each Dir As String In AllFolders.Split("|")
                If CheckDirectoryPermission(Dir) Then
                    PATH_MC = Dir
                    GoTo FinishMinecraftFolderCheck
                End If
            Next

            '执行到这里就需要创建一个新的.minecraft文件夹了
            PATH_MC = PATH & ".minecraft\"
            Directory.CreateDirectory(PATH_MC)
            Directory.CreateDirectory(PATH_MC & "versions\")

FinishMinecraftFolderCheck:

            log("[Pool] .minecraft文件夹：" & PATH_MC)
            WriteIni("setup", "LaunchFolderSelect", PATH_MC)

            '输出启动器信息
            If Not File.Exists(PATH_MC & "launcher_profiles.json") Then
                WriteFile(PATH_MC & "launcher_profiles.json",
                            "{" & vbCrLf &
                            "  ""profiles"": {" & vbCrLf &
                            "    """ & APPLICATION_SHORT_NAME & """: {" & vbCrLf &
                            "      ""name"": """ & APPLICATION_SHORT_NAME & """," & vbCrLf &
                            "    }," & vbCrLf &
                            "    ""(Default)"": {" & vbCrLf &
                            "      ""name"": ""(" & APPLICATION_SHORT_NAME & ")""" & vbCrLf &
                            "    }" & vbCrLf &
                            "  }," & vbCrLf &
                            "  ""selectedProfile"": ""(Default)""," & vbCrLf &
                            "  ""clientToken"": ""88888888-8888-8888-8888-888888888888""" & vbCrLf &
                            "}")
                log("[Pool] 已创建 launcher_profiles.json")
            End If
            If Not File.Exists(PATH_MC & "options.txt") Then
                WriteFile(PATH_MC & "options.txt", "lang:zh_cn" & vbCrLf)
                log("[Pool] 已创建 options.txt")
            End If

            '继续加载版本列表
            Pool.Add(New Thread(AddressOf PoolVersionList))

        Catch ex As Exception
            ExShow(ex, "检测MC文件夹失败", ErrorLevel.MsgboxAndFeedback)
        End Try
    End Sub

    ''' <summary>
    ''' 启动时的静默刷新是否还在进行。
    ''' 启动线程要等它为 False 才能把访问令牌交给游戏，否则会传一个空令牌过去。
    ''' </summary>
    Public IsSilentLoginRunning As Boolean = False

    ''' <summary>
    ''' 用上次登录留下的玩家名与 UUID，先把界面恢复成「已登录」的样子。
    ''' 这样打开启动器立刻能看到自己的账号，不必等几秒钟的令牌刷新。
    ''' 注意这里只恢复显示：访问令牌仍然只由刷新流程产生，不会落盘、也不会被复用。
    ''' </summary>
    Public Sub RestoreCachedLogin()
        Try
            If ReadReg("MSARefreshToken", "") = "" Then Exit Sub
            Dim PlayerName As String = ReadReg("MojangPlayerName", "")
            Dim PlayerUUID As String = ReadReg("MojangPlayerUUID", "")
            If PlayerName = "" OrElse PlayerUUID = "" Then Exit Sub

            Dim json As New JObject
            json("accessToken") = ""
            json("clientToken") = ""
            Dim profile As New JObject
            profile("id") = PlayerUUID
            profile("name") = PlayerName
            json("selectedProfile") = profile
            LoginResult = json.ToString(Newtonsoft.Json.Formatting.None)
            log("[Login] 已用上次的登录信息恢复账号显示：" & PlayerName)
        Catch ex As Exception
            log("[Login] 恢复登录信息失败：" & ex.Message)
        End Try
    End Sub

    Private PoolLoginLock As New Object
    ''' <summary>
    ''' 加载线程：自动登录。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub PoolLogin(IsAutoLogin As Boolean)
        IsSilentLoginRunning = True
        Try
            If MODE_OFFLINE Then Exit Sub
            SyncLock PoolLoginLock
                ' 改用微软账号登录：只有保存过 refresh token 才做静默自动登录。
                ' 交互式登录（设备码流程）需要用户去网页输入代码，不能在启动时自动弹出，
                ' 所以它由 MSALoginInteractiveFlow 单独负责。
                Dim MSARefreshRaw As String = ReadReg("MSARefreshToken", "")
                Dim MSARefresh As String = If(MSARefreshRaw = "", "", SerRemove(MSARefreshRaw))
                If MSARefresh = "" Then GoTo ExitSub
                ' 不再有「是否保存登录状态」开关：保存刷新令牌没有副作用，
                ' 而关掉它只会让用户每次启动都要重新走一遍设备码登录，因此总是保存。

                ' 先清掉旧令牌，避免静默刷新失败时残留一个过期的 accessToken 被拿去启动游戏
                WriteReg("AccessToken", "")

                '静默刷新期间不再把启动按钮切成「正在登录」：
                '账号名与头像已经在 RestoreCachedLogin 里用缓存显示出来了，刷新在后台完成。
                '万一用户在这期间就点了开始游戏，启动线程会等这里结束（见 IsSilentLoginRunning）。

                Dim MSAResult As ModAuth.MSALoginResult = ModAuth.MSALoginByRefresh(MSARefresh)
                If MSAResult.Success Then
                    SaveMSALoginResult(MSAResult)
                    log("[Login] 静默刷新登录成功：" & MSAResult.PlayerName)
                Else
                    log("[Login] 静默刷新失败：" & MSAResult.ErrorMessage)
                    ' refresh token 失效（改密码、撤销授权、长期未使用），清掉凭据等用户重新交互登录
                    WriteReg("MSARefreshToken", "")
                    LoginResult = ""
                End If

ExitSub:
            End SyncLock

            '加载皮肤
            If LoginResult.Contains("selectedProfile") Then
                Dim UUID As String = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(LoginResult), JObject)("selectedProfile")("id").ToString
                WriteIni("cache\skin\UUID", CType(Newtonsoft.Json.JsonConvert.DeserializeObject(LoginResult), JObject)("selectedProfile")("name").ToString, UUID)
                Dim SkinAddress As String = DownloadSkin(UUID)
                If Not SkinAddress = "" Then frmMain.Dispatcher.Invoke(Sub() frmHomeRight.LoadMojangSkin(SkinAddress))
            End If

        Catch ex As Exception
            ExShow(ex, "自动登录出错", ErrorLevel.MsgboxAndFeedback)
        Finally
            '无论成功失败都要清掉，否则启动线程会一直等下去
            IsSilentLoginRunning = False
        End Try
    End Sub

    Public NeedUpdate As LoadState = LoadState.Waiting
    ''' <summary>
    ''' 检查到的新版本号；没有新版本时为空字符串。
    ''' </summary>
    Public LatestLauncherVersion As String = ""

    ''' <summary>
    ''' 把版本号字符串里的连续数字逐段取出来，用于比较。
    ''' GitHub Releases 的 tag 形如 v1.0.10-CE，启动器内的 VERSION_NAME 形如 1.0.9-CE，
    ''' 直接按字符串比较既分不清 1.0.10 与 1.0.9，也没法忽略 v 前缀和 -CE 后缀，
    ''' 所以提取出所有数字段再逐段按数值比较。
    ''' </summary>
    Private Function GetVersionNumberParts(ByVal Version As String) As String()
        Dim Result As New List(Of String)
        Dim Now As String = ""
        For Each c As Char In Version
            If Char.IsDigit(c) Then
                Now &= c
            ElseIf Now.Length > 0 Then
                Result.Add(Now)
                Now = ""
            End If
        Next
        If Now.Length > 0 Then Result.Add(Now)
        Return Result.ToArray
    End Function

    ''' <summary>
    ''' 比较两个版本号：Left 较新返回 1，Left 较旧返回 -1，完全相同返回 0。
    ''' </summary>
    Private Function CompareVersion(ByVal Left As String, ByVal Right As String) As Integer
        Dim LeftParts As String() = GetVersionNumberParts(Left)
        Dim RightParts As String() = GetVersionNumberParts(Right)
        For i As Integer = 0 To Math.Max(LeftParts.Length, RightParts.Length) - 1
            Dim LeftValue As Integer = If(i < LeftParts.Length, Val(LeftParts(i)), 0)
            Dim RightValue As Integer = If(i < RightParts.Length, Val(RightParts(i)), 0)
            If LeftValue <> RightValue Then Return If(LeftValue > RightValue, 1, -1)
        Next
        Return 0
    End Function

    ''' <summary>
    ''' 加载线程：检查启动器更新。
    '''
    ''' （HTTP 405），每次启动都会稳定产生两条错误日志，而这些日志对使用者毫无意义。
    ''' 现在改为读取本项目在 GitHub 上的 Releases：仓库还没有发布任何 Release 时接口返回
    ''' 404，这种情况按「已是最新版本」处理，不弹任何错误。
    ''' 更新方式同步简化为「仅提示」：发现新版本时只在开始游戏按钮下方显示一行提示，
    ''' 点击后打开 Releases 页面由使用者自行下载；启动器不再自动下载、替换自身
    ''' （自替换所依赖的更新包地址同样已失效，且自动替换程序文件在杀毒软件下极易误报）。
    ''' </summary>
    Public Sub PoolUpdate()
        LatestLauncherVersion = ""
        If MODE_OFFLINE Then NeedUpdate = LoadState.Failed : Exit Sub
        '设置为不检查更新
        If ReadIni("setup", "SysUpdate", "True") = "False" Then
            NeedUpdate = LoadState.Failed
            log("[Pool] 已在设置中关闭启动器更新检查")
            Exit Sub
        End If

        Try
            NeedUpdate = LoadState.Loading
            'Silent 模式：检查更新失败不该打扰使用者——仓库还没有 Release（404）、
            'GitHub 接口对未登录请求限流（403）都属于正常情况，只写日志。
            Dim Data As String = GetWebsiteCode("https://api.github.com/repos/" & UPDATE_REPO & "/releases/latest", Encoding.UTF8, True)
            If Data.Length = 0 Then
                NeedUpdate = LoadState.Failed
                log("[Pool] 未能取得启动器更新信息，按已是最新版本处理")
                Exit Sub
            End If

            Dim TagToken As JToken = ReadJson(Data)("tag_name")
            If TagToken Is Nothing OrElse TagToken.ToString.Length = 0 Then
                NeedUpdate = LoadState.Failed
                log("[Pool] 启动器更新信息中缺少版本号")
                Exit Sub
            End If

            Dim Tag As String = TagToken.ToString
            If CompareVersion(Tag, VERSION_NAME) > 0 Then
                '更新可用
                LatestLauncherVersion = Tag
                NeedUpdate = LoadState.Loaded
                log("[Pool] 发现启动器新版本（" & Tag & "）")
                frmHomeRight.Dispatcher.Invoke(Sub() frmHomeRight.ShowUpdate("发现启动器更新：" & Tag, formHomeRight.UpdateType.PCL))
            Else
                NeedUpdate = LoadState.Failed
                log("[Pool] 启动器已经是最新版本（GitHub 上最新为 " & Tag & "）")
            End If
        Catch ex As Exception
            '检查更新失败不打扰使用者，只写日志
            NeedUpdate = LoadState.Failed
            log("[Pool] 检查启动器更新失败：" & GetStringFromException(ex, True))
        End Try
    End Sub

    Public IsPoolVersionListRunning As Boolean = False
    ''' <summary>
    ''' 加载线程：加载版本列表。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub PoolVersionList()
        '保证不同时加载版本列表
        Do While IsPoolVersionListRunning
            Thread.Sleep(RandomInteger(15, 65))
        Loop
        IsPoolVersionListRunning = True
        frmHomeRight.StartButtonIsLoading = True

        Try

            log("[Pool] 预加载版本列表开始")

            Dim ReloadAll As Boolean = False '是否重载全部版本

            '检查Versions文件夹
            If Not Directory.Exists(PATH_MC & "versions\") Then
                VersionsList = New Dictionary(Of VersionSwapState, ArrayList) From {{VersionSwapState.UNKNOWN, New ArrayList}, {VersionSwapState.NORMAL, New ArrayList}, {VersionSwapState.SWAP, New ArrayList}, {VersionSwapState.OLD, New ArrayList}, {VersionSwapState.WRONG, New ArrayList}}
                GoTo LoadEnd
            End If

            '遍历文件夹
            Dim FolderList As New ArrayList
            For Each Folder As DirectoryInfo In (New DirectoryInfo(PATH_MC & "versions")).GetDirectories
                FolderList.Add(Folder.Name)
            Next

            '对比文件夹列表
            Dim FolderListCheck As String = (MC_VERSION_CODE & "#" & Join(FolderList.ToArray, "#")).Replace(vbCrLf, "\n").Replace(":", "..").Replace("LastCheckedFolder", "LCF")
            If Not ReadIni(PATH_MC & "PCL.ini", "LastCheckedFolder") = FolderListCheck Then
                '文件夹列表不符
                log("[Pool] 文件夹列表变更，重载所有版本")
                ReloadAll = True
                WriteIni(PATH_MC & "PCL.ini", "LastCheckedFolder", FolderListCheck)
            End If

Reload:
            VersionsList = New Dictionary(Of VersionSwapState, ArrayList) From {{VersionSwapState.UNKNOWN, New ArrayList}, {VersionSwapState.NORMAL, New ArrayList}, {VersionSwapState.SWAP, New ArrayList}, {VersionSwapState.OLD, New ArrayList}, {VersionSwapState.WRONG, New ArrayList}}

            '遍历每个版本
            For Each VersionName As String In FolderList
                If Not CheckDirectoryPermission(PATH_MC & "versions\" & VersionName & "\") Then GoTo NextFolder
                Dim Version As New MCVersion(VersionName)
                Directory.CreateDirectory(Version.Path & "\PCL")

                If ReadIni(Version.Path & "\PCL\Version.ini", "VersionListCode", "0") = MC_VERSION_CODE And Not ReloadAll Then

                    '存在有效的PCL配置文件，读取配置文件
                    Version.LoadedByFile = True
                    With Version
                        .SwapType = ReadIni(Version.Path & "\PCL\Version.ini", "SwapType")
                        .Version = ReadIni(Version.Path & "\PCL\Version.ini", "Version")
                        .ReleaseTime = ReadIni(Version.Path & "\PCL\Version.ini", "ReleaseTime")
                        .Assets = ReadIni(Version.Path & "\PCL\Version.ini", "Assets")
                        .MainVersionCode = ReadIni(Version.Path & "\PCL\Version.ini", "MainVersionCode")
                        .Type = ReadIni(Version.Path & "\PCL\Version.ini", "Type")
                        .Logo = ReadIni(Version.Path & "\PCL\Version.ini", "Logo")
                        .Description = ReadIni(Version.Path & "\PCL\Version.ini", "Description")
                        .VersionCheckResult = ReadIni(Version.Path & "\PCL\Version.ini", "VersionCheckResult")
                        .InheritVersion = ReadIni(Version.Path & "\PCL\Version.ini", "InheritVersion")
                    End With

                    '重新检测
                    Dim OldResult As VersionCheckState = Version.VersionCheckResult
                    CheckMCVersion(Version, Version.VersionCheckResult = VersionCheckState.INHERITS_EXCEPTION)
                    If Not Version.VersionCheckResult = OldResult Then
                        ReloadAll = True
                        GoTo Reload
                    End If

                    '图片的重新检测
                    If Not Version.Logo.StartsWith(PATH_IMAGE) Then
                        If Not File.Exists(Version.Logo) Then GoTo NotLoadFile
                    End If

                Else
NotLoadFile:
                    '不存在PCL配置文件，读取版本信息
                    Version.LoadedByFile = False
                    CheckMCVersion(Version, True)

                    '版本存在错误
                    If Not Version.VersionCheckResult = VersionCheckState.NO_PROBLEM Then
                        '错误的版本
                        Version.SwapType = VersionSwapState.WRONG
                        GoTo NextVersion
                    End If

                    '尝试从继承版本处获取版本号
                    If Not Version.InheritVersion = "" Then
                        Version.Version = Version.InheritVersion
                        GoTo VersionSearchEnd
                    End If

                    '从json获取版本失败，试图从文件夹名称获取版本号，如果失败则标记“未知”
                    Dim CharCheck As ArrayList = RegexSearch(Version.Name, "1\.1?[0-9]{1}(\.[1-9]{1}([0-9]{1})?)?(-pre[1-9]?)?|1[2-9]{1}w[0-9]{1,2}[a-z]{1}")
                    If CharCheck.Count > 0 Then
                        Version.Version = CharCheck(0)
                    Else
                        Version.Version = "未知"
                    End If
VersionSearchEnd:

                    '获取发布时间
                    If Version.JsonText.Contains("releaseTime") Then
                        Version.ReleaseTime = RegexSearch(Version.JsonText.Replace(" ", ""), "(?<=releaseTime"":"")([^T]+)([^\+\-]+)")(0)
                    Else
                        Version.ReleaseTime = ""
                    End If

                    '设置主版本号
                    Dim MainVersion As ArrayList = RegexSearch(Version.Version, "1.[0-9]+")
                    Version.MainVersionCode = Math.Min(20, If(MainVersion.Count = 1, Int(MainVersion(0).Replace("1.", "")), 0))

                    '获取版本种类
                    Version.Type = MCVersionType.RELEASE
                    If Version.InheritVersion = "" Then
                        '无继承版本的
                        If Version.Version = "未知" Then Version.Type = MCVersionType.UNKNOWN
                        If Version.Version.ToLower.Contains("w") Or Version.Version.ToLower.Contains("pre") Or Version.JsonText.Replace(" ", "").Contains("""type"":""snapshot""") Then Version.Type = MCVersionType.SNAPSHOT
                        If Len(Version.ReleaseTime) > 6 Then If Val(Mid(Version.ReleaseTime, 3, 2)) < 14 Then Version.Type = MCVersionType.OLD '未知版本可能显示为1970年，故取后两位数
                        If If(IsNothing(Version.Json("type")), False, Version.Json("type") = "fool") Then Version.Type = MCVersionType.FOOL
                        If Version.JsonText.Contains("minecraftforge:minecraftforge") Then Version.Type = MCVersionType.FORGE
                    Else
                        '有继承版本的
                        Version.Type = If(Version.JsonText.Contains("Forge"), MCVersionType.FORGE, MCVersionType.OPTIFINE)
                    End If

                    '检测自定义种类信息
                    Version.SwapType = Val(ReadIni(Version.Path & "PCL1_CE\Setup.ini", "CustomType", "255"))
                    If Version.SwapType = VersionSwapState.UNKNOWN Then 'UNKNOWN为255
                        Select Case Version.Type
                            Case MCVersionType.SNAPSHOT, MCVersionType.RELEASE, MCVersionType.OPTIFINE
                                '需要再次确认是否应该折叠，不加处理
                            Case MCVersionType.OLD
                                '老版本，直接归类
                                Version.SwapType = VersionSwapState.OLD
                            Case MCVersionType.FOOL
                                '愚人节版本，直接折叠
                                Version.SwapType = VersionSwapState.SWAP
                            Case Else
                                '未知版本、Forge、不折叠版本，直接列入列表
                                Version.SwapType = VersionSwapState.NORMAL
                        End Select
                    End If

NextVersion:

                    '设置图标
                    If File.Exists(Version.Path & "PCL1_CE\Icon.png") Then
                        Try
                            Dim LogoTryLoad = New MyBitmap(Version.Path & "PCL1_CE\Icon.png")
                            Version.Logo = Version.Path & "PCL1_CE\Icon.png"
                        Catch ex As Exception
                            File.Delete(Version.Path & "PCL1_CE\Icon.png") '加载失败就删了图片，因为下一次还是会失败的……
                            GoTo CommonPicture
                        End Try
                    Else
CommonPicture:
                        Select Case Version.Type
                            Case MCVersionType.FORGE
                                Version.Logo = PATH_IMAGE & "Block-Anvil.png"
                            Case MCVersionType.OLD
                                Version.Logo = PATH_IMAGE & "Block-CobbleStone.png"
                            Case MCVersionType.SNAPSHOT
                                Version.Logo = PATH_IMAGE & "Block-CommandBlock.png"
                            Case MCVersionType.FOOL
                                Version.Logo = PATH_IMAGE & "Block-Dirt.png"
                            Case Else 'OptiFine、原版、未知
                                If Version.SwapType = VersionSwapState.WRONG Then
                                    Version.Logo = PATH_IMAGE & "Block-RedstoneBlock.png"
                                Else
                                    Version.Logo = PATH_IMAGE & "Block-Grass.png"
                                End If
                        End Select
                    End If

                    '设置描述文本
                    If Version.SwapType = VersionSwapState.WRONG Then
                        '错误描述文本
                        Select Case Version.VersionCheckResult
                            Case VersionCheckState.EMPTY_FOLDER
                                Version.Description = "空文件夹"
                            Case VersionCheckState.INHERITS_EXCEPTION
                                Version.Description = "依赖版本出错：" & Version.InheritVersion
                            Case VersionCheckState.INHERITS_NOT_EXIST
                                Version.Description = "依赖版本丢失：" & Version.InheritVersion
                            Case VersionCheckState.JAR_NOT_EXIST
                                Version.Description = "Jar 文件缺失"
                            Case VersionCheckState.JSON_CANT_READ
                                Version.Description = "Json 读取失败"
                            Case VersionCheckState.JSON_NOT_EXIST
                                Version.Description = "Json 文件缺失"
                            Case Else
                                Version.Description = "未知问题"
                        End Select
                    Else
                        '加载自定义文本
                        Version.Description = ReadIni(Version.Path & "PCL1_CE\Setup.ini", "Description")
                        If Version.Description = "" Then
                            '程序输出文本
                            Dim Description As String = If(Version.Type = MCVersionType.OPTIFINE, "OptiFine ", If(Version.Type = MCVersionType.FORGE, "Forge ", "")) & Version.Version
                            If Not Version.Name = Description And Not Description = "未知" Then Version.Description = Description
                        End If
                    End If

                End If

                VersionsList(Version.SwapType).Add(Version)
NextFolder:
            Next

            '判断是否需要重载列表次序

            If VersionsList(VersionSwapState.UNKNOWN).Count = 0 Then GoTo LoadEnd

            '计算最新的版本
            Dim LargestTimeVer As New MCVersion("") '最新的版本
            Dim LargestTime As ULong = 0 '最新的版本的时间
            Dim CurrentTime As ULong '当前的版本的时间
            Dim NewVersion As New Dictionary(Of String, MCVersion) '存储版本列表，键为“MainVersionCode-Type”
            For Each ver As MCVersion In VersionsList(VersionSwapState.UNKNOWN)
                CurrentTime = Val(ver.ReleaseTime.Replace("-", "").Replace(":", "").Replace("T", ""))
                '取最大时间
                If CurrentTime > LargestTime Then
                    LargestTime = CurrentTime
                    LargestTimeVer = ver
                End If
                'OptiFine与正式版判断
                If Not ver.Type = MCVersionType.SNAPSHOT Then
                    Dim CurrentVersion As New MCVersion("")
                    If NewVersion.TryGetValue(ver.MainVersionCode & "-" & ver.Type, CurrentVersion) Then
                        If CurrentTime > Val(CurrentVersion.ReleaseTime.Replace("-", "").Replace(":", "").Replace("T", "")) Then
                            NewVersion(ver.MainVersionCode & "-" & ver.Type) = ver
                        End If
                    Else
                        NewVersion.Add(ver.MainVersionCode & "-" & ver.Type, ver)
                    End If
                End If
            Next

            '如果最新的版本为快照，则添加入列表
            If LargestTimeVer.Type = MCVersionType.SNAPSHOT Then LargestTimeVer.SwapType = VersionSwapState.NORMAL

            '准备检查后的主版本列表
            Dim MainCodeList As New ArrayList '主版本列表
            For Each Key As String In NewVersion.Keys
                If Not MainCodeList.Contains(Integer.Parse(Key.Split("-")(0))) Then MainCodeList.Add(Integer.Parse(Key.Split("-")(0)))
            Next

            '把各个OptiFine和原版加入列表
            Dim WillAdd As New ArrayList
            For Each MainCode As Integer In MainCodeList
                If NewVersion.ContainsKey(MainCode & "-" & MCVersionType.OPTIFINE) Then
                    '存在OptiFine版本
                    WillAdd.Add(NewVersion(MainCode & "-" & MCVersionType.OPTIFINE))
                    If NewVersion.ContainsKey(MainCode & "-" & MCVersionType.RELEASE) Then
                        '如果原版比OptiFine版更新也显示原版
                        If Val(NewVersion(MainCode & "-" & MCVersionType.RELEASE).ReleaseTime.Replace("-", "").Replace(":", "").Replace("T", "")) > Val(NewVersion(MainCode & "-" & MCVersionType.OPTIFINE).ReleaseTime.Replace("-", "").Replace(":", "").Replace("T", "")) Then
                            WillAdd.Add(NewVersion(MainCode & "-" & MCVersionType.RELEASE))
                        End If
                    End If
                Else
                    '不存在OptiFine版本，检查原版版本
                    If NewVersion.ContainsKey(MainCode & "-" & MCVersionType.RELEASE) Then WillAdd.Add(NewVersion(MainCode & "-" & MCVersionType.RELEASE))
                End If
            Next

            '列表处理
            For Each Ver As MCVersion In WillAdd
                Ver.SwapType = VersionSwapState.NORMAL
            Next
            For Each Ver As MCVersion In VersionsList(VersionSwapState.UNKNOWN)
                If Ver.SwapType = VersionSwapState.UNKNOWN Then Ver.SwapType = VersionSwapState.SWAP
                VersionsList(Ver.SwapType).Add(Ver)
            Next

LoadEnd:
            VersionsList.Remove(VersionSwapState.UNKNOWN)

            '根据设置合并折叠与老版本
            If ReadIni("setup", "HomeVersionSwap", "True") = "False" Then
                VersionsList(VersionSwapState.NORMAL).AddRange(VersionsList(VersionSwapState.SWAP))
                VersionsList(VersionSwapState.SWAP).Clear()
            End If
            If ReadIni("setup", "HomeVersionOld", "True") = "False" Then
                VersionsList(VersionSwapState.NORMAL).AddRange(VersionsList(VersionSwapState.OLD))
                VersionsList(VersionSwapState.OLD).Clear()
            End If

            '保存文件 & 列表排序
            For Each List As ArrayList In VersionsList.Values
                '排序
                Dim Sorter As MCVersion() = ArrayConventer(Of MCVersion, ArrayList)(List)
                Array.Sort(Sorter)
                List.Clear()
                For Each Item In Sorter
                    If Not IsNothing(Item) Then List.Add(Item)
                Next
                '写入文件
                For Each Version As MCVersion In List
                    If Version.LoadedByFile = False Then
                        '写入PCL配置文件
                        Using iniWriter As New StreamWriter(Version.Path & "PCL1_CE\Version.ini", False, New UTF8Encoding(False))
                            iniWriter.WriteLine("VersionListCode:" & MC_VERSION_CODE)
                            iniWriter.WriteLine("SwapType:" & Version.SwapType)
                            iniWriter.WriteLine("Version:" & Version.Version)
                            iniWriter.WriteLine("ReleaseTime:" & Version.ReleaseTime)
                            iniWriter.WriteLine("MainVersionCode:" & Version.MainVersionCode)
                            iniWriter.WriteLine("Type:" & Version.Type)
                            iniWriter.WriteLine("Logo:" & Version.Logo)
                            iniWriter.WriteLine("Description:" & Version.Description)
                            iniWriter.WriteLine("VersionCheckResult:" & Version.VersionCheckResult)
                            iniWriter.WriteLine("InheritVersion:" & Version.InheritVersion)
                            iniWriter.Flush()
                        End Using
                    End If
                Next
            Next

            log("[Pool] 预加载版本列表结束")

        Catch ex As Exception

            ExShow(ex, "加载版本列表失败", ErrorLevel.MsgboxAndFeedback)
            '清空列表
            VersionsList = New Dictionary(Of VersionSwapState, ArrayList)

        Finally
            ' IsPoolVersionListRunning 原本只在 LoadVersionList 的 Finally（运行于 UI 线程）
            ' 里复位，而 PoolMinecraftFolder 会在 UI 线程上自旋等待这个标志。两者一旦相遇就会互相等待：
            ' UI 线程等标志复位，PoolVersionList 又在等 UI 线程执行下面的 Dispatcher.Invoke，
            ' 界面于是永久卡死，只能结束进程。这里先在 PoolVersionList 自己的线程上复位，确保等待方一定能解除。
            IsPoolVersionListRunning = False
            frmHomeRight.Dispatcher.Invoke(Sub() frmHomeRight.LoadVersionList())
        End Try
    End Sub

    ''' <summary>
    ''' 加载线程：刷新界面配色。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub PoolLoadPictures()
        ' 背景图与顶部图现在只支持本地设置。此方法仅保留刷新主题配色。
        frmMain.Dispatcher.Invoke(New RefreshThemeInvoke(AddressOf RefreshTheme))
    End Sub

    Private PoolPushCount As Integer = 0
    ''' <summary>
    ''' 加载线程：首页。
    ''' </summary>
    ''' <remarks></remarks>
    Public Class PushSource
        Public Name As String
        Public URL As String
        Public Introduce As String
        Public IsEnabled As String
        Public LocalPath As String
    End Class
    Public Sub PoolPush()
        If MODE_OFFLINE Then frmStart.IsPushLoading = False : Exit Sub

        ' 推荐功能默认关闭，由设置项控制。
        If ReadIni("setup", "HomeEnabled", "True") = "False" Then
            frmStart.IsPushLoading = False
            log("[Pool-HomeLeft] 推荐功能已关闭，跳过全部推荐源")
            Exit Sub
        End If

        ' 推荐源共两个：
        '   Minecraft 吧精品 —— 走百度贴吧客户端接口，返回 JSON，不受网页风控影响
        '   Mojang 官方     —— 走官方启动器自己的新闻接口，与官方启动器同源
        Dim Threads As New ArrayList
        If ReadIni("setup", "HomeTbPush", "True") = "True" Then Threads.Add(New Thread(AddressOf PoolMainTb))
        If ReadIni("setup", "HomeMojangPush", "True") = "True" Then Threads.Add(New Thread(AddressOf PoolMainMojang))
        For Each Pusher As Thread In Threads
            Pusher.Priority = ThreadPriority.BelowNormal
            PoolPushCount = PoolPushCount + 1
            Pusher.Start()
        Next

        If PoolPushCount = 0 Then frmStart.IsPushLoading = False
        log("[Pool-HomeLeft] 订阅信息加载完成")
    End Sub

#Region "推荐源"

    ''' <summary>
    ''' 百度贴吧客户端接口的签名。
    '''
    ''' 规则：把除 sign 以外的所有参数按参数名升序排列，拼成 key=value 直接相连的字符串
    ''' （中间没有分隔符），末尾接上固定盐 "tiebaclient!!!"，取 MD5 的小写十六进制。
    ''' 这个算法从 2013 年客户端开始使用至今没有变过，多个开源贴吧客户端（如 TiebaLite、
    ''' aiotieba）都用同一套。注意签名用的是原始值，不能先用 URL 编码。
    ''' </summary>
    Private Function GetTiebaSign(ByVal Params As Dictionary(Of String, String)) As String
        Dim Keys As New List(Of String)(Params.Keys)
        Keys.Sort(StringComparer.Ordinal)
        Dim Builder As New StringBuilder
        For Each Key As String In Keys
            Builder.Append(Key).Append("=").Append(Params(Key))
        Next
        Builder.Append("tiebaclient!!!")
        Dim MD5 As System.Security.Cryptography.MD5 = System.Security.Cryptography.MD5.Create()
        Dim Hash As Byte() = MD5.ComputeHash(Encoding.UTF8.GetBytes(Builder.ToString))
        Dim Result As New StringBuilder
        For Each OneByte As Byte In Hash
            Result.Append(OneByte.ToString("x2"))
        Next
        Return Result.ToString
    End Function

    ''' <summary>
    ''' 安全地取出 Json 字段的文本：字段不存在或为 null 时返回空字符串，
    ''' 避免 Nothing.ToString 抛空引用异常。
    ''' </summary>
    Private Function GetJsonString(ByVal Token As JToken) As String
        If Token Is Nothing Then Return ""
        If Token.Type = JTokenType.Null Then Return ""
        Return Token.ToString
    End Function

    ''' <summary>
    ''' 从贴吧帖子里取出配图地址。
    ''' 优先用 media 里的 w=720 小图（体积小、加载快），没有则退回正文里第一个图片碎片的原图地址。
    ''' 需要注意的是这两个字段在接口里有时是数组、有时是单个对象，必须都兼容。
    ''' </summary>
    Private Function GetTiebaImage(ByVal ThreadItem As JToken) As String
        Dim MediaList As New List(Of JToken)
        Dim MediaToken As JToken = ThreadItem("media")
        If MediaToken IsNot Nothing Then
            If MediaToken.Type = JTokenType.Array Then
                For Each Item As JToken In MediaToken
                    MediaList.Add(Item)
                Next
            Else
                MediaList.Add(MediaToken)
            End If
        End If
        For Each Item As JToken In MediaList
            Dim SmallPic As String = GetJsonString(Item("small_pic"))
            If SmallPic.Length > 0 Then Return SmallPic
            Dim BigPic As String = GetJsonString(Item("big_pic"))
            If BigPic.Length > 0 Then Return BigPic
        Next

        Dim ContentList As New List(Of JToken)
        Dim ContentToken As JToken = ThreadItem("first_post_content")
        If ContentToken IsNot Nothing Then
            If ContentToken.Type = JTokenType.Array Then
                For Each Item As JToken In ContentToken
                    ContentList.Add(Item)
                Next
            Else
                ContentList.Add(ContentToken)
            End If
        End If
        For Each Item As JToken In ContentList
            'type = 3 表示这个碎片是一张图片
            If GetJsonString(Item("type")) = "3" Then
                Dim Source As String = GetJsonString(Item("src"))
                If Source.Length > 0 Then Return Source
            End If
        Next
        Return ""
    End Function

    ''' <summary>
    ''' 推荐标题黑名单：过滤掉服务器招人、联机群、水楼、其它启动器广告之类的无关内容。
    ''' 只用于贴吧这种用户发帖的源；官方新闻源的内容本来就在主题内，不需要过滤。
    ''' </summary>
    Private Function IsPushTitleBlocked(ByVal Title As String) As Boolean
        For Each Word As String In New String() {"启动器", "吧", "水楼", "赛", "招聘", "联机", "服务器", "HMCL", "Launcher", "Baka"}
            If Title.Contains(Word) Then Return True
        Next
        Return False
    End Function

    ''' <summary>
    ''' 清掉贴吧标题里的日期与【…】／[…] 前缀，让信息框里显示得更干净。
    ''' </summary>
    Private Function CleanTiebaTitle(ByVal Title As String) As String
        Title = (New RegularExpressions.Regex("\[[0-9]{2,4}-[0-9]{1,2}-[0-9]{1,2}\]")).Replace(Title, "")
        Dim Prefix As ArrayList = RegexSearch(Title, "【[^0]{2,4}】")
        If Prefix.Count = 0 Then Prefix = RegexSearch(Title, "\[[^0]{2,4}\]")
        If Prefix.Count > 0 AndAlso Title.StartsWith(Prefix(0)) Then Title = Title.Replace(Prefix(0), "")
        Return Title.Trim
    End Function

    ''' <summary>
    ''' 贴吧源：Minecraft 吧精品。
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub PoolMainTb()
        log("[Pool-HomeLeft] 加载推荐源：Minecraft 吧精品")
        If File.Exists(PATH & "PCL1_CE\cache\source\CA28E736032912D2801529958AB8937EBB351E2338212FA49529\cache.ini") Then
            '如果缓存存在，读取->(下载->处理)
            PoolMainTbLoad()
            PoolPushCount = PoolPushCount - 1
            If PoolPushCount = 0 Then frmStart.IsPushLoading = False
            Thread.Sleep(5000)
            PoolMainTbDownload()
        Else
            '如果缓存不存在，(下载->处理)->读取
            If PoolMainTbDownload() Then PoolMainTbLoad()
            PoolPushCount = PoolPushCount - 1
            If PoolPushCount = 0 Then frmStart.IsPushLoading = False
        End If
    End Sub
    ''' <summary>
    ''' 改用百度贴吧客户端的接口抓取精品帖。
    '''
    ''' 原实现直接抓 https://tieba.baidu.com/f?kw=minecraft&amp;tab=good 这个网页，再用正则去抠
    ''' col2_right、thread_list 之类的 class。两条致命问题：一是会被百度的网页风控拦成 403
    ''' （补 User-Agent 也未必过得了），二是网页模板一改整套正则就全部失效，实际上早就抓不到东西。
    ''' 现在改成调用客户端接口 c/f/frs/page 并带上 is_good=1，返回的是 JSON：
    '''   thread_list[].id / title / is_good / media / first_post_content
    ''' 字段稳定，也不需要解析 HTML。rn=30 一次能拿回约 30 条精品帖。
    ''' </summary>
    Private Function PoolMainTbDownload() As Boolean

        '————————————
        '下载
        '————————————
        Dim ThreadList As JArray
        Try
            Dim Params As New Dictionary(Of String, String) From {
                {"kw", "minecraft"},
                {"pn", "0"},
                {"rn", "30"},
                {"is_good", "1"},
                {"client_type", "2"},
                {"client_version", "11.0.8.7"},
                {"cuid", "baidutiebaapp"}
            }
            Params.Add("sign", GetTiebaSign(Params))
            Dim Query As New StringBuilder
            For Each Item As KeyValuePair(Of String, String) In Params
                If Query.Length > 0 Then Query.Append("&")
                Query.Append(Item.Key).Append("=").Append(Uri.EscapeDataString(Item.Value))
            Next
            Dim SourceCode As String = GetWebsiteCode("https://c.tieba.baidu.com/c/f/frs/page?" & Query.ToString, Encoding.UTF8)
            If SourceCode.Length < 100 Then Throw New Exception("获取的代码长度不足：" & SourceCode)
            Dim Root As JObject = ReadJson(SourceCode)
            Dim ErrorCode As String = GetJsonString(Root("error_code"))
            If ErrorCode <> "0" Then Throw New Exception("接口返回错误码 " & ErrorCode & "：" & GetJsonString(Root("error_msg")))
            ThreadList = CType(Root("thread_list"), JArray)
            If ThreadList Is Nothing OrElse ThreadList.Count = 0 Then Throw New Exception("接口没有返回任何帖子。")
        Catch ex As Exception
            ExShow(ex, "下载推荐源失败（Minecraft 吧精品）")
            Return False
        End Try

        '————————————
        '处理推荐
        '————————————
        Dim Sources As New ArrayList
        Try
            For Each ThreadItem As JToken In ThreadList
                Dim Tid As String = GetJsonString(ThreadItem("id"))
                Dim Title As String = GetJsonString(ThreadItem("title"))
                If Tid.Length = 0 OrElse Title.Length = 0 Then Continue For
                '接口偶尔会把置顶的普通帖一起返回，这里只留精品帖
                If GetJsonString(ThreadItem("is_good")) <> "1" Then Continue For
                '过滤服务器招人、联机群、水楼之类的无关内容
                If IsPushTitleBlocked(Title) Then Continue For

                Dim Source As New InfoBoxSource With {
                            .OnClickURL = "https://tieba.baidu.com/p/" & Tid,
                            .Title = CleanTiebaTitle(Title),
                            .Source = "Minecraft 吧精品",
                            .PictureName = GetTiebaImage(ThreadItem),
                            .Type = ""}
                GetSourceType(Source)
                Sources.Add(Source)
                '信息框一页只放四个，十几个已经足够翻好几页了
                If Sources.Count >= 12 Then Exit For
            Next
            If Sources.Count = 0 Then Throw New Exception("没有解析出任何可用的帖子。")
        Catch ex As Exception
            ExShow(ex, "处理推荐源失败（Minecraft 吧精品）")
            Return False
        End Try

        '————————————
        '输出
        '————————————
        WritePushCache("CA28E736032912D2801529958AB8937EBB351E2338212FA49529", Sources)
        Return True
    End Function
    Private Sub PoolMainTbLoad()
        PoolMainPushURLLoad(New PushSource With {
                            .LocalPath = PATH & "PCL1_CE\cache\source\CA28E736032912D2801529958AB8937EBB351E2338212FA49529\cache.ini",
                            .Name = "Minecraft 吧精品"})
    End Sub

    ''' <summary>
    ''' Mojang 源：官方启动器的新闻推送。
    ''' </summary>
    ''' <remarks></remarks>
    Private Sub PoolMainMojang()
        log("[Pool-HomeLeft] 加载推荐源：Mojang 官方")
        If File.Exists(PATH & "PCL1_CE\cache\source\A407DF2F142912192C15239087B89C1BBB82\cache.ini") Then
            PoolMainMojangLoad()
            PoolPushCount = PoolPushCount - 1 : If PoolPushCount = 0 Then frmStart.IsPushLoading = False
            Thread.Sleep(5000)
            PoolMainMojangDownload()
        Else
            '如果缓存不存在，(下载->处理)->读取
            If PoolMainMojangDownload() Then PoolMainMojangLoad()
            PoolPushCount = PoolPushCount - 1 : If PoolPushCount = 0 Then frmStart.IsPushLoading = False
        End If
    End Sub
    ''' <summary>
    ''' 该源此前读的是第三方论坛的门户页，与 Mojang 官方并无关系，
    ''' 那个站点关停后自然一起失效。
    ''' 现在改用官方启动器自己在用的新闻接口 launchercontent.mojang.com/v2/news.json：
    ''' 返回 entries[]，每条有 title / date / readMoreLink / playPageImage.url，
    ''' 正好一一对应信息框需要的标题、链接和配图（配图是相对路径，要补上域名）。
    ''' 注意必须用 v2 这个路径：不带 v2 的 news.json 同样能访问，但内容停在 2024 年 1 月，
    ''' 是旧版启动器留下的废弃接口，v2 才是官方现在在更新的。
    ''' </summary>
    Private Function PoolMainMojangDownload() As Boolean

        '————————————
        '下载
        '————————————
        Dim Entries As JArray
        Try
            Dim SourceCode As String = GetWebsiteCode("https://launchercontent.mojang.com/v2/news.json", Encoding.UTF8)
            If SourceCode.Length < 1000 Then Throw New Exception("获取的代码长度不足：" & SourceCode)
            Entries = CType(ReadJson(SourceCode)("entries"), JArray)
            If Entries Is Nothing OrElse Entries.Count = 0 Then Throw New Exception("接口没有返回任何新闻。")
        Catch ex As Exception
            ExShow(ex, "下载推荐源失败（Mojang 官方）")
            Return False
        End Try

        '————————————
        '处理推荐
        '————————————
        Dim Sources As New ArrayList
        Try
            For Each Entry As JToken In Entries
                Dim Title As String = GetJsonString(Entry("title"))
                Dim Link As String = GetJsonString(Entry("readMoreLink"))
                If Title.Length = 0 OrElse Link.Length = 0 Then Continue For

                Dim Picture As String = ""
                Dim PlayPage As JToken = Entry("playPageImage")
                If PlayPage IsNot Nothing Then
                    Dim Relative As String = GetJsonString(PlayPage("url"))
                    If Relative.Length > 0 Then Picture = "https://launchercontent.mojang.com" & Relative
                End If

                '把日期写进标题，信息框里一眼能看出这条新闻有多新
                Dim DateText As String = GetJsonString(Entry("date"))
                Sources.Add(New InfoBoxSource With {
                            .OnClickURL = Link,
                            .Title = If(DateText.Length > 0, "[" & DateText & "] " & Title, Title),
                            .Source = "Mojang 官方",
                            .PictureName = Picture,
                            .Type = "新闻"})
                If Sources.Count >= 8 Then Exit For
            Next
            If Sources.Count = 0 Then Throw New Exception("没有解析出任何可用的新闻。")
        Catch ex As Exception
            ExShow(ex, "处理推荐源失败（Mojang 官方）")
            Return False
        End Try

        '————————————
        '输出
        '————————————
        WritePushCache("A407DF2F142912192C15239087B89C1BBB82", Sources)
        Return True
    End Function
    Private Sub PoolMainMojangLoad()
        PoolMainPushURLLoad(New PushSource With {
                            .LocalPath = PATH & "PCL1_CE\cache\source\A407DF2F142912192C15239087B89C1BBB82\cache.ini",
                            .Name = "Mojang 官方"})
    End Sub

    ''' <summary>
    ''' 把解析结果写进推荐源的缓存 ini。
    ''' 原来每个源各自复制一遍这段拼接逻辑，现在收拢成一个函数。
    ''' </summary>
    Private Sub WritePushCache(ByVal CacheName As String, ByVal Sources As ArrayList)
        Dim OutputIni As String = "Version:" & PUSH_VERSION_CODE & vbCrLf
        For i As Integer = 1 To Sources.Count
            Dim Source As InfoBoxSource = Sources(i - 1)
            OutputIni = OutputIni & vbCrLf
            OutputIni = OutputIni & "Picture" & i & ":" & Source.PictureName & vbCrLf
            OutputIni = OutputIni & "Title" & i & ":" & Source.Title & vbCrLf
            OutputIni = OutputIni & "Link" & i & ":" & Source.OnClickURL & vbCrLf
            OutputIni = OutputIni & "Type" & i & ":" & Source.Type & vbCrLf
        Next
        WriteFile("PCL1_CE\cache\source\" & CacheName & "\cache.ini", OutputIni, isFullPath:=False)
    End Sub

    Private Sub PoolMainPushURLLoad(ByVal Source As PushSource)

        '版本检查
        If Val(ReadIni(Source.LocalPath, "Version", "0")) < PUSH_VERSION_CODE Then
            log("[Pool-HomeLeft] 推荐源版本过老（" & Source.Name & "）：目前为 " & ReadIni(Source.LocalPath, "Version", "0") & "，要求为 " & PUSH_VERSION_CODE, True)
            Exit Sub
        End If

        '处理代码
        Try
            Dim CanLoadItem As New ArrayList
            Dim ii As Integer = 1
            Do Until ReadIni(Source.LocalPath, "Title" & ii) = ""
                '过滤
                Dim Context As String = ReadIni(Source.LocalPath, "Title" & ii)
                If Context.Contains("Plain Craft Launcher") Or Context.Contains("PCL") Or Not (Context.Contains("启动器") Or Context.Contains("HMCL") Or Context.Contains("Launcher") Or Context.Contains("Baka") Or ReadIni(Source.LocalPath, "Link" & ii).Contains("twitter")) Then
                    Dim InfoSource As New InfoBoxSource With {
                        .OnClickURL = ReadIni(Source.LocalPath, "Link" & ii),
                        .PictureName = ReadIni(Source.LocalPath, "Picture" & ii),
                        .Title = Context,
                        .Source = Source.Name,
                        .Type = ReadIni(Source.LocalPath, "Type" & ii)}
                    '纯文本订阅只保留一半
                    If Not InfoSource.PictureName.Contains("://") Then
                        If ReadIni("setup", "HomeHide", "True") = "True" And Not RandomInteger(0, 1) = 1 Then GoTo NextItem
                    End If
                    frmHomeLeft.LoadingItem.Add(InfoSource)
                    InfoSource.Load()
                End If
NextItem:
                ii = ii + 1
            Loop

        Catch ex As Exception
            log("[Pool-HomeLeft] 处理推荐源失败（" & Source.Name & "）：" & GetStringFromException(ex, True), True)
        End Try

    End Sub

    Private Sub GetSourceType(ByRef Source As InfoBoxSource)
        If Source.Type = "新闻" Then
        ElseIf Source.Title.Contains("教程") Or Source.Title.Contains("新手") Or Source.Title.Contains("技巧") Or Source.Title.ToLower.Contains("wiki") Or Source.Title.Contains("翻译") Then
            Source.Type = "教程"
        ElseIf Source.Title.Contains("编辑器") Or Source.Title.Contains("软件") Or Source.Title.Contains("工具") Or Source.Title.Contains("生成器") Then
            Source.Type = "软件"
        ElseIf Source.Title.Contains("联机") Or Source.Title.Contains("插件") Or Source.Title.Contains("服务端") Or Source.Title.Contains("开服") Or Source.Title.Contains("服务器") Then
            Source.Type = "多人"
        ElseIf Source.Title.Contains("皮肤") Then
            Source.Type = "皮肤"
        ElseIf Source.Title.ToLower.Contains("mod") Then
            Source.Type = "Mod"
        ElseIf Source.Title.Contains("解密") Or Source.Title.Contains("逃") Or Source.Title.Contains("跑酷") Or Source.Title.Contains("空岛") Or Source.Title.Contains("RPG") Or Source.Title.Contains("生存") Or Source.Title.Contains("小游戏") Or Source.Title.Contains("CB") Or Source.Title.Contains("红石") Or Source.Title.Contains("闯关") Or Source.Title.Contains("PVP") Or Source.Title.Contains("PVE") Or Source.Title.Contains("命令") Or Source.Title.Contains("战") Or Source.Title.Contains("冒险") Then
            Source.Type = "地图"
        ElseIf Source.Title.Contains("创作") Or Source.Title.Contains("小说") Or Source.Title.Contains("漫画") Or Source.Title.Contains("物语") Or Source.Title.Contains("插画") Or Source.Title.Contains("短篇") Or Source.Title.Contains("长篇") Then
            Source.Type = "创作"
        ElseIf Source.Title.Contains("材质") Or Source.Title.Contains("资源包") Then
            Source.Type = "资源包"
        ElseIf Source.Title.Contains("整合") Or Source.Title.Contains("懒人包") Then
            Source.Type = "懒人包"
        ElseIf Source.Title.Contains("阁") Or Source.Title.Contains("航母") Or Source.Title.Contains("港") Or Source.Title.Contains("宫") Or Source.Title.Contains("上古之石") Or Source.Title.Contains("楼") Or Source.Title.Contains("城") Or Source.Title.Contains("镇") Or Source.Title.Contains("殿") Or Source.Title.Contains("厅") Or Source.Title.Contains("桥") Or Source.Title.Contains("陵") Or Source.Title.Contains("园") Or Source.Title.Contains("宅") Or Source.Title.Contains("街") Or Source.Title.Contains("筑") Or Source.Title.Contains("山") Or Source.Title.Contains("景") Or Source.Title.ToLower.Contains("city") Then
            Source.Type = "建筑"
        ElseIf Source.Title.Contains("地图") Or Source.Title.Contains("作品") Then
            Source.Type = "地图"
        ElseIf Source.Title.Contains("发布") Or Source.Title.Contains("eleas") Then
            Source.Type = "新闻"
        Else
            Source.Type = ""
        End If
    End Sub

#End Region

#End Region

#Region "自动安装"

    ''' <summary>
    ''' 自动安装文件或文件夹。
    ''' </summary>
    ''' <param name="Path">文件或文件夹完整路径。文件夹不以“\”结尾。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetup(ByVal Path As String)
        log("[System] 安装文件请求：" & Path)
        Try

            '基础检测
            If Directory.Exists(Path) Then
                '文件夹
                Path = Path & "\"
                Dim a = Path & GetFileNameFromPath(Mid(Path, 1, Len(Path) - 1)) & ".jar"
ReCheckDir:
                If File.Exists(Path & "level.dat") And Directory.Exists(Path & "region") Then
                    '存档文件夹
                    Dim th As New Thread(AddressOf AutoSetupSaveFolder)
                    th.Start(Path)
                ElseIf File.Exists(Path & "pack.mcmeta") And Directory.Exists(Path & "assets") Then
                    '资源包文件夹
                    Dim th As New Thread(AddressOf AutoSetupResFolder)
                    th.Start(Path)
                ElseIf File.Exists(Path & GetFileNameFromPath(Mid(Path, 1, Len(Path) - 1)) & ".jar") And File.Exists(Path & GetFileNameFromPath(Mid(Path, 1, Len(Path) - 1)) & ".json") Then
                    '版本文件夹
                    Dim th As New Thread(AddressOf AutoSetupVersionFolder)
                    th.Start(Path)
                Else
                    '进一层鉴别
                    Dim AllDir As String() = Directory.GetDirectories(Path)
                    If AllDir.Length = 1 Then
                        '发现进一层文件夹
                        Path = AllDir(0) & "\"
                        log("[System] 进一层的文件夹：" & Path)
                        GoTo ReCheckDir
                    Else
                        '没有进一层文件夹
                        ShowHint(New HintConverter("自动安装失败：不支持的文件夹", HintState.Warn))
                        Exit Sub
                    End If
                End If
            ElseIf File.Exists(Path) Then
                '文件
                '获取后缀
                Dim SplitResult As String() = Path.Split(".")
                Dim EndDot As String = If(SplitResult.Length > 1, SplitResult(SplitResult.Length - 1), "")
                '判断后缀
                If EndDot = "zip" Then
                    'zip处理线程
                    Dim th As New Thread(Sub()
                                             Try
                                                 Using zip As New ZipFile(Path, Encoding.Default)
                                                     Dim BasePath As String = "" '基础路径，用于一层嵌套的文件
ReCheckZip:
                                                     '检查是否为版本文件夹
                                                     Dim LastPath As String
                                                     If BasePath = "" Then
                                                         LastPath = GetFileNameFromPath(Path).Replace(".zip", "")
                                                     Else
                                                         LastPath = Mid(BasePath, 1, Len(BasePath) - 1).Split("/")(Mid(BasePath, 1, Len(BasePath) - 1).Split("/").Length - 1)
                                                     End If
                                                     log("[System] 末端文件夹名：" & LastPath)

                                                     '文件检查
                                                     If zip.ContainsEntry(BasePath & "level.dat") And zip.ContainsEntry(BasePath & "region/") Then
                                                         '存档zip
                                                         AutoSetupSaveZip(zip, BasePath, Path)
                                                     ElseIf zip.ContainsEntry(BasePath & "pack.mcmeta") And zip.ContainsEntry(BasePath & "assets/") Then
                                                         '资源包zip
                                                         If BasePath = "" Then
                                                             AutoSetupResZip(Path)
                                                         Else
                                                             ShowHint(New HintConverter("自动安装失败：资源包压缩包不支持内置文件夹", HintState.Warn))
                                                             Exit Sub
                                                         End If
                                                     ElseIf zip.ContainsEntry(BasePath & LastPath & ".jar") And zip.ContainsEntry(BasePath & LastPath & ".json") Then
                                                         '版本zip
                                                         AutoSetupVersionZip(zip, BasePath, Path)
                                                     Else
                                                         '进一层鉴别
                                                         Dim AllDir As New ArrayList
                                                         For Each Entry As String In zip.EntryFileNames
                                                             If Entry.IndexOf("/") = Len(Entry) - 1 Then AllDir.Add(Entry)
                                                         Next
                                                         If AllDir.Count = 1 Then
                                                             BasePath = AllDir(0)
                                                             log("[System] 进一层的压缩文件路径：" & BasePath)
                                                             GoTo ReCheckZip
                                                         Else
                                                             ShowHint(New HintConverter("自动安装失败：不支持的文件", HintState.Warn))
                                                             Exit Sub
                                                         End If
                                                     End If
                                                 End Using
                                             Catch ex As Exception
                                                 ShowHint(New HintConverter("自动安装失败：" & GetStringFromException(ex), HintState.Warn))
                                             End Try
                                         End Sub)
                    th.Start()
                ElseIf EndDot = "json" Or EndDot = "jar" Then
                    '版本文件
                    AutoSetupVersionFile(Path, EndDot)
                ElseIf EndDot = "rar" Or EndDot = "7z" Then
                    ShowHint(New HintConverter("自动安装失败：PCL暂时只支持.zip文件", HintState.Warn))
                    Exit Sub
                Else
                    ShowHint(New HintConverter("自动安装失败：未知的文件后缀", HintState.Warn))
                    Exit Sub
                End If
            Else
                ShowHint(New HintConverter("自动安装失败：它既不是文件也不是文件夹", HintState.Warn))
                Exit Sub
            End If

        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
    End Sub

    ''' <summary>
    ''' 自动安装存档文件夹。需要异步调用。
    ''' </summary>
    ''' <param name="DirPath">文件夹路径，以“\”结尾。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupSaveFolder(ByVal DirPath As String)
        Try

            log("[System] 存档文件夹：" & DirPath)
            Dim SaveName As String = GetFileNameFromPath(Left(DirPath, Len(DirPath) - 1))
            Dim TargetPath As String = PATH_MC & "saves\" & SaveName & "\"
            Directory.CreateDirectory(PATH_MC & "saves\")
            If TargetPath = DirPath Then Exit Sub '路径相同

            '已存在检测
Recheck:
            If Directory.Exists(TargetPath) Then
                Select Case AutoSetupCheck("存档 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath, True)
                    Case 1
                        '替换
                        ShowHint("正在删除原存档：" & SaveName)
                        Directory.Delete(TargetPath, True)
                    Case 2
                        '重命名
                        TargetPath = Left(TargetPath, Len(TargetPath) - 1) & "-\"
                        SaveName = SaveName & "-"
                        GoTo Recheck
                    Case 3
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装存档：" & SaveName)
            Directory.CreateDirectory(TargetPath)
            My.Computer.FileSystem.CopyDirectory(DirPath, TargetPath)
            ShowHint(New HintConverter("安装存档成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
    End Sub

    ''' <summary>
    ''' 自动安装资源包文件夹。需要异步调用。
    ''' </summary>
    ''' <param name="DirPath">文件夹路径，以“\”结尾。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupResFolder(ByVal DirPath As String)
        Try

            log("[System] 资源包文件夹：" & DirPath)
            Dim SaveName As String = GetFileNameFromPath(Left(DirPath, Len(DirPath) - 1))
            Dim TargetPath As String = PATH_MC & "resourcepacks\" & SaveName & "\"
            Directory.CreateDirectory(PATH_MC & "resourcepacks\")
            If TargetPath = DirPath Then Exit Sub '路径相同

            '已存在检测
Recheck:
            If Directory.Exists(TargetPath) Then
                Select Case AutoSetupCheck("资源包 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath)
                    Case 1
                        '替换
                        ShowHint("正在删除原资源包：" & SaveName)
                        Directory.Delete(TargetPath, True)
                    Case 2
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装资源包：" & SaveName)
            Directory.CreateDirectory(TargetPath)
            My.Computer.FileSystem.CopyDirectory(DirPath, TargetPath)
            ShowHint(New HintConverter("安装资源包成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
    End Sub

    ''' <summary>
    ''' 自动安装版本文件夹。需要异步调用。
    ''' </summary>
    ''' <param name="DirPath">文件夹路径，以“\”结尾。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupVersionFolder(ByVal DirPath As String)
        Try

            log("[System] 版本文件夹：" & DirPath)
            Dim SaveName As String = GetFileNameFromPath(Left(DirPath, Len(DirPath) - 1))
            Dim TargetPath As String = PATH_MC & "versions\" & SaveName & "\"
            Directory.CreateDirectory(PATH_MC & "versions\")
            If TargetPath = DirPath Then Exit Sub '路径相同

            '已存在检测
Recheck:
            If Directory.Exists(TargetPath) Then
                Select Case AutoSetupCheck("版本 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath)
                    Case 1
                        '替换
                        ShowHint("正在删除原版本：" & SaveName)
                        Directory.Delete(TargetPath, True)
                    Case 2
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装版本：" & SaveName)
            Directory.CreateDirectory(TargetPath)
            My.Computer.FileSystem.CopyDirectory(DirPath, TargetPath)
            ShowHint(New HintConverter("安装版本成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
        '强制重载版本列表
        WriteIni(PATH_MC & "PCL.ini", "LastCheckedFolder", "")
        Pool.Add(New Thread(AddressOf PoolVersionList))
    End Sub

    ''' <summary>
    ''' 自动安装版本文件。需要异步调用。
    ''' </summary>
    ''' <param name="FilePath">文件路径。</param>
    ''' <param name="EndDot">文件后缀。不包含小数点。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupVersionFile(ByVal FilePath As String, ByVal EndDot As String)
        Try

            log("[System] 版本文件：" & FilePath)
            Dim SaveName As String = GetFileNameFromPath(FilePath).Replace("." & EndDot, "")
            Dim TargetPath As String = PATH_MC & "versions\" & SaveName & "\" & SaveName & "." & EndDot
            Directory.CreateDirectory(PATH_MC & "versions\")
            If TargetPath = FilePath Then Exit Sub '路径相同

            '已存在检测
Recheck:
            If Directory.Exists(PATH_MC & "versions\" & SaveName & "\") Then
                If File.Exists(TargetPath) Then
                    Select Case AutoSetupCheck("版本文件 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath, True)
                        Case 1
                            '替换
                            ShowHint("正在删除原版本文件：" & SaveName)
                            File.Delete(TargetPath)
                        Case 2
                            '重命名
                            SaveName = SaveName & "-"
                            TargetPath = PATH_MC & "versions\" & SaveName & "\" & SaveName & "." & EndDot
                            GoTo Recheck
                        Case 3
                            '取消
                            Exit Sub
                    End Select
                End If
            End If

            ShowHint("正在安装版本文件：" & SaveName)
            Directory.CreateDirectory(PATH_MC & "versions\" & SaveName & "\")
            File.Copy(FilePath, TargetPath)
            ShowHint(New HintConverter("安装版本文件成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
        '强制重载版本列表
        WriteIni(PATH_MC & "PCL.ini", "LastCheckedFolder", "")
        Pool.Add(New Thread(AddressOf PoolVersionList))
    End Sub

    ''' <summary>
    ''' 自动安装存档zip压缩包。需要异步调用。
    ''' </summary>
    ''' <param name="Zip">ZipFile类型的已经打开的压缩文件对象。</param>
    ''' <param name="BasePath">在压缩文件中的基础路径。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupSaveZip(ByVal Zip As ZipFile, ByVal BasePath As String, ByVal FilePath As String)
        Try

            log("[System] 存档zip" & If(BasePath = "", "", "：" & BasePath))
            Dim SaveName As String = If(BasePath = "", GetFileNameFromPath(FilePath).Replace(".zip", ""), Left(BasePath, Len(BasePath) - 1))
            Dim TargetPath As String = PATH_MC & "saves\" & SaveName & "\"
            Directory.CreateDirectory(PATH_MC & "saves\")

            '已存在检测
            Dim Renamed As Boolean = False
Recheck:
            If Directory.Exists(TargetPath) Then
                Select Case AutoSetupCheck("存档 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath, True)
                    Case 1
                        '替换
                        ShowHint("正在删除原存档：" & SaveName)
                        Directory.Delete(TargetPath, True)
                    Case 2
                        '重命名
                        TargetPath = Left(TargetPath, Len(TargetPath) - 1) & "-\"
                        SaveName = SaveName & "-"
                        Renamed = True
                        GoTo Recheck
                    Case 3
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装存档：" & SaveName)
            Directory.CreateDirectory(TargetPath)
            If BasePath = "" Then
                Zip.ExtractAll(TargetPath, ExtractExistingFileAction.OverwriteSilently)
            Else
                Zip.ExtractAll(PATH, ExtractExistingFileAction.OverwriteSilently)
                My.Computer.FileSystem.CopyDirectory(PATH & Left(BasePath, Len(BasePath) - 1), TargetPath)
                Directory.Delete(PATH & Left(BasePath, Len(BasePath) - 1), True)
            End If
            ShowHint(New HintConverter("安装存档成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
    End Sub

    ''' <summary>
    ''' 自动安装资源包zip压缩包。需要异步调用。
    ''' </summary>
    ''' <param name="FilePath">Zip文件路径。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupResZip(ByVal FilePath As String)
        Try

            log("[System] 资源包zip：" & FilePath)
            Dim SaveName As String = GetFileNameFromPath(FilePath)
            Dim TargetPath As String = PATH_MC & "resourcepacks\" & SaveName
            Directory.CreateDirectory(PATH_MC & "resourcepacks\")
            If TargetPath = FilePath Then Exit Sub '路径相同

            '已存在检测
Recheck:
            If File.Exists(TargetPath) Then
                Select Case AutoSetupCheck("资源包 " & SaveName & " 已经存在。" & vbCrLf & "位置：" & TargetPath)
                    Case 1
                        '替换
                        ShowHint("正在删除原资源包：" & SaveName)
                        File.Delete(TargetPath)
                    Case 2
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装资源包：" & SaveName)
            File.Copy(FilePath, TargetPath)
            ShowHint(New HintConverter("安装资源包成功：" & SaveName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
    End Sub

    ''' <summary>
    ''' 自动安装版本zip压缩包。需要异步调用。
    ''' </summary>
    ''' <param name="Zip">ZipFile类型的已经打开的压缩文件对象。</param>
    ''' <param name="BasePath">在压缩文件中的基础路径。</param>
    ''' <remarks></remarks>
    Public Sub AutoSetupVersionZip(ByVal Zip As ZipFile, ByVal BasePath As String, ByVal FilePath As String)
        Try

            log("[System] 版本zip" & If(BasePath = "", "", "：" & BasePath))
            Dim VersionName As String = If(BasePath = "", GetFileNameFromPath(FilePath).Replace(".zip", ""), Left(BasePath, Len(BasePath) - 1))
            Dim TargetPath As String = PATH_MC & "versions\" & VersionName & "\"
            Directory.CreateDirectory(PATH_MC & "versions\")

            '已存在检测
            Dim Renamed As Boolean = False
Recheck:
            If Directory.Exists(TargetPath) Then
                Select Case AutoSetupCheck("版本 " & VersionName & " 已经存在。" & vbCrLf & "位置：" & TargetPath)
                    Case 1
                        '替换
                        ShowHint("正在删除原版本：" & VersionName)
                        Directory.Delete(TargetPath, True)
                    Case 2
                        '取消
                        Exit Sub
                End Select
            End If

            ShowHint("正在安装版本：" & VersionName)
            Directory.CreateDirectory(TargetPath)
            If BasePath = "" Then
                Zip.ExtractAll(TargetPath, ExtractExistingFileAction.OverwriteSilently)
            Else
                Zip.ExtractAll(PATH, ExtractExistingFileAction.OverwriteSilently)
                My.Computer.FileSystem.CopyDirectory(PATH & Left(BasePath, Len(BasePath) - 1), TargetPath)
                Directory.Delete(PATH & Left(BasePath, Len(BasePath) - 1), True)
            End If
            ShowHint(New HintConverter("安装版本成功：" & VersionName, HintState.Finish))
        Catch ex As Exception
            ExShow(ex, "自动安装失败", ErrorLevel.AllUsers)
        End Try
        '强制重载版本列表
        WriteIni(PATH_MC & "PCL.ini", "LastCheckedFolder", "")
        Pool.Add(New Thread(AddressOf PoolVersionList))
    End Sub

    ''' <summary>
    ''' 弹出自动安装的确认消息。需要异步调用。
    ''' </summary>
    ''' <param name="Text">显示的文本。</param>
    ''' <param name="CanRename">是否显示重命名选项。</param>
    ''' <returns>用户做出的是否安装的决定。</returns>
    ''' <remarks></remarks>
    Public Function AutoSetupCheck(ByVal Text As String, Optional ByVal CanRename As Boolean = False) As Integer
        Return MyMsgbox(Text, "自动安装确认", "覆盖", If(CanRename, "重命名", "取消"), If(CanRename, "取消", ""), True)
    End Function

#End Region

#Region "设置"

    ''' <summary>
    ''' 切换页面隐藏。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub SetupRefreshHidden()
        frmMain.btnTopDown.Visibility = If(ReadIni("setup", "UiHiddenDownload", "False") = "True", Visibility.Collapsed, Visibility.Visible)
        frmMain.btnTopSetup.Visibility = Visibility.Visible
    End Sub

    ''' <summary>
    ''' 切换背景图片的展示方式。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub SetupRefreshBackground()
        Select Case Val(ReadIni("setup", "UiBackgroundShow", "0"))
            Case 0
                '右下
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Right
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Bottom
                frmMain.imgMainBg.Stretch = Stretch.None
            Case 1
                '左上
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Left
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Top
                frmMain.imgMainBg.Stretch = Stretch.None
            Case 2
                '右上
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Right
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Top
                frmMain.imgMainBg.Stretch = Stretch.None
            Case 3
                '居中
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Center
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Center
                frmMain.imgMainBg.Stretch = Stretch.None
            Case 4
                '拉伸
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Stretch
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Stretch
                frmMain.imgMainBg.Stretch = Stretch.Fill
            Case 5
                '适应
                frmMain.imgMainBg.HorizontalAlignment = HorizontalAlignment.Stretch
                frmMain.imgMainBg.VerticalAlignment = VerticalAlignment.Stretch
                frmMain.imgMainBg.Stretch = Stretch.UniformToFill
        End Select
    End Sub

    ''' <summary>
    ''' 根据进行版本隔离返回某版本所需的游戏目录，以“\”结尾。
    ''' </summary>
    ''' <param name="Version"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function GetVersionFolder(ByVal Version As MCVersion) As String
        Select Case Val(ReadIni("setup", "LaunchSplit", "0"))
            Case 0
                Return PATH_MC
            Case 1
                Return If(Version.Type = MCVersionType.FORGE, Version.Path, PATH_MC)
            Case Else
                Return Version.Path
        End Select
    End Function

#End Region

#Region "提示"

    ''' <summary>
    ''' 等待弹出的提示文本。
    ''' </summary>
    ''' <remarks></remarks>
    Public WaitingHint As ArrayList = If(IsNothing(WaitingHint), New ArrayList, WaitingHint)
    ''' <summary>
    ''' 在窗口左下角弹出提示文本。
    ''' </summary>
    ''' <param name="Hint">要显示的提示。</param>
    ''' <remarks></remarks>
    Public Sub ShowHint(ByVal Hint As HintConverter)
        If IsNothing(WaitingHint) Then WaitingHint = New ArrayList 'MDZZ这初始化也太坑了
        For Each NowHint As HintConverter In WaitingHint
            If Hint.Text = NowHint.Text Then Exit Sub
        Next
        WaitingHint.Add(Hint)
    End Sub
    ''' <summary>
    ''' 在窗口左下角弹出提示文本。
    ''' </summary>
    ''' <param name="Text">要显示的提示。</param>
    ''' <remarks></remarks>
    Public Sub ShowHint(ByVal Text As String)
        ShowHint(New HintConverter(Text))
    End Sub

    ''' <summary>
    ''' 等待弹出的提示窗口。
    ''' </summary>
    ''' <remarks></remarks>
    Public WaitingHintWindow As ArrayList = If(IsNothing(WaitingHintWindow), New ArrayList, WaitingHintWindow)
    ''' <summary>
    ''' 弹出提示窗口。
    ''' </summary>
    ''' <param name="Hint">要显示的提示文本。</param>
    ''' <remarks></remarks>
    Public Sub ShowHintWindow(ByVal Title As String, ByVal Hint As String)
        frmMain.Dispatcher.Invoke(Sub()

                                      If IsNothing(WaitingHintWindow) Then WaitingHintWindow = New ArrayList '初始化
                                      If Not WaitingHintWindow.Contains({Title, Hint}) Then
                                          WaitingHintWindow.Add({Title, Hint})
                                      End If

                                  End Sub)
    End Sub

#End Region

#Region "事件日志"

    ''' <summary>
    ''' 记录一条事件。只写入本地日志，不做任何网络上报。
    ''' 保留这个函数是为了让各处调用点保持原样，日志在调试与排查时仍然有用。
    ''' </summary>
    Public Sub SendStat(ByVal MainType As String, ByVal Action As String, Optional ByVal Lab As String = "", Optional ByVal Number As Integer = 0)
        Try
            MainType = MainType.Replace(vbCrLf, "").Replace("""", " ").Replace("\", "/")
            Action = Action.Replace(vbCrLf, "").Replace("""", " ").Replace("\", "/")
            Lab = Lab.Replace(vbCrLf, "").Replace("""", " ").Replace("\", "/")
            If MODE_DEVELOPER Then log("[事件] " & MainType & " > " & Action & If(Lab = "", "", " > " & Lab) & "（" & Number & "）")
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' 处理错误信息。
    ''' </summary>
    ''' <param name="ex"></param>
    ''' <param name="description">描述文本，如“下载文件时出错”。</param>
    ''' <param name="errorLevel">错误的严重程度。</param>
    ''' <remarks></remarks>
    Public Sub ExShow(ByVal ex As Exception, ByVal description As String, Optional ByVal errorLevel As ErrorLevel = ErrorLevel.DebugOnly, Optional ByVal msgboxText As String = "")
        On Error Resume Next
        '发送统计
        If Not description.Contains("下载文件失败") Then SendStat("错误", GetStringFromEnum(errorLevel), description & "：" & GetStringFromException(ex), errorLevel)
        '处理错误
        Select Case errorLevel
            Case modMain.ErrorLevel.Slient
                '不提示用户
                log("[Error] " & description & "：" & GetStringFromException(ex, MODE_DEVELOPER),, True)
            Case modMain.ErrorLevel.DebugOnly
                '提示调试模式用户
                log("[Error] " & description & "：" & GetStringFromException(ex, True), True)
            Case modMain.ErrorLevel.AllUsers
                '提示所有用户
                log("[Error] " & description & "：" & GetStringFromException(ex, True))
                ShowHint(New HintConverter(description & "：" & GetStringFromException(ex), HintState.Critical))
            Case modMain.ErrorLevel.MsgboxWithoutFeedback
                '弹窗，不要求反馈
                MyMsgbox(If(Not msgboxText = "", msgboxText & vbCrLf & vbCrLf, "") & "详细的错误信息：" & GetStringFromException(ex, True), description, IsWaitExit:=False)
            Case modMain.ErrorLevel.MsgboxAndFeedback
                '弹窗，要求反馈
                If MyMsgbox(If(Not msgboxText = "", msgboxText & vbCrLf & vbCrLf, "") & "详细的错误信息：" & GetStringFromException(ex, True) & vbCrLf & vbCrLf & "你是否愿意反馈这个问题？", description, "反馈", "不反馈") = 1 Then
                    Feedback()
                End If
            Case modMain.ErrorLevel.Barrier
                '尽可能显示错误信息，然后关闭程序
                If MsgBox(If(Not msgboxText = "", msgboxText & vbCrLf & vbCrLf, "") & "详细的错误信息：" & GetStringFromException(ex, True) & vbCrLf & vbCrLf & "你是否愿意反馈这个问题？", MsgBoxStyle.YesNo + MsgBoxStyle.Critical, description) = MsgBoxResult.Yes Then
                    Feedback()
                End If
                End
        End Select
    End Sub
    Public Enum ErrorLevel As Integer
        ''' <summary>
        ''' 正常情况下的常发错误。只记录 Log，不提示用户。
        ''' </summary>
        ''' <remarks></remarks>
        Slient = 0
        ''' <summary>
        ''' 只需要提示调试模式下的用户。
        ''' </summary>
        ''' <remarks></remarks>
        DebugOnly = 1
        ''' <summary>
        ''' 需要以弹出提示的方式提示所有用户。
        ''' </summary>
        ''' <remarks></remarks>
        AllUsers = 2
        ''' <summary>
        ''' 需要以弹窗的方式提示所有用户，但是不要求反馈。
        ''' </summary>
        ''' <remarks></remarks>
        MsgboxWithoutFeedback = 3
        ''' <summary>
        ''' 需要以弹窗的方式提示所有用户，并且要求反馈。
        ''' </summary>
        ''' <remarks></remarks>
        MsgboxAndFeedback = 4
        ''' <summary>
        ''' 尽可能弹出提示，然后结束程序。
        ''' </summary>
        ''' <remarks></remarks>
        Barrier = 5
    End Enum

    ''' <summary>
    ''' 打开用户反馈通道：直接打开 PCL1-CE 在 GitHub 上的新建 Issue 页面。
    ''' 程序崩溃时的反馈流程也调用本函数。
    ''' </summary>
    Public Sub Feedback()
        Try
            Process.Start("https://github.com/Wcx110121/PCL1-CE/issues/new")
        Catch ex As Exception
            ExShow(ex, "打开反馈页面失败", ErrorLevel.DebugOnly)
        End Try
    End Sub


#End Region

#Region "皮肤"

    ''' <summary>
    ''' 从官方 UUID 获取本地缓存的皮肤路径。如果失败返回空字符串。
    ''' </summary>
    ''' <returns></returns>
    Public Function GetCacheSkinAddressByMojangUUID(UUID As String) As String
        Dim SkinName = ReadIni("cache\skin\SkinName", UUID)
        If SkinName = "" Then
            Return ""
        ElseIf SkinName = "Steve" Or SkinName = "Alex" Then
            Return SkinName
        Else
            GetCacheSkinAddressByMojangUUID = PATH & "PCL1_CE\cache\skin\" & SkinName & ".png"
            If Not File.Exists(GetCacheSkinAddressByMojangUUID) Then Return ""
        End If
    End Function

    ''' <summary>
    ''' 下载对应官方 UUID 的皮肤并返回文件路径。如果失败返回空字符串。
    ''' </summary>
    ''' <param name="UUID"></param>
    ''' <returns></returns>
    Public Function DownloadSkin(UUID As String) As String
        Try

            '尝试读取缓存的皮肤

            Dim CacheSkinAddress As String = GetCacheSkinAddressByMojangUUID(UUID)
            If Not CacheSkinAddress = "" Then Return CacheSkinAddress

            '向官方档案接口查询该 UUID 的皮肤信息
            Dim Raw As String = GetWebsiteCode("https://sessionserver.mojang.com/session/minecraft/profile/" & UUID, Encoding.UTF8)

            '接口没有返回内容（离线 UUID 或无效 UUID），回退到默认头像
            If Raw = "" Then
                log("[Skin] 档案接口未返回内容，使用默认头像：" & UUID)
                Return GetSkinTypeFromUUID(UUID)
            End If

            '解析外层 JSON，取出 properties 里 textures 属性的 value（base64）
            '
            ' 这里原先是拿 IndexOf 在一整段文本里抠 textures","value":" 这类固定串。
            ' Mojang 后来把响应改成了带缩进和换行的格式化 JSON，两个字段之间夹了换行，
            ' 于是匹配必然失败、静默回退成默认头像——而且不报错，表面上只表现为「皮肤没加载」。
            ' 改成走 JSON 解析，之后无论对方怎么调整排版都不会再受影响。
            Dim SkinUrl As String = ""
            Try
                Dim Profile As JObject = ReadJson(Raw)
                Dim Props As JArray = CType(Profile("properties"), JArray)
                If Props IsNot Nothing Then
                    For Each Prop As JToken In Props
                        If Prop("name") IsNot Nothing AndAlso Prop("name").ToString = "textures" Then
                            Dim Decoded As String = Encoding.UTF8.GetString(Convert.FromBase64String(Prop("value").ToString))
                            Dim Tex As JObject = ReadJson(Decoded)
                            If Tex("textures") IsNot Nothing AndAlso Tex("textures")("SKIN") IsNot Nothing AndAlso Tex("textures")("SKIN")("url") IsNot Nothing Then
                                SkinUrl = Tex("textures")("SKIN")("url").ToString
                            End If
                            Exit For
                        End If
                    Next
                End If
            Catch ex As Exception
                '格式不符合预期时当作没有自定义皮肤处理，不要让登录流程失败
                log("[Skin] 解析皮肤信息失败：" & GetStringFromException(ex, True))
            End Try

            If SkinUrl = "" Then
                log("[Skin] 档案中没有自定义皮肤，使用默认头像：" & UUID)
                Return GetSkinTypeFromUUID(UUID)
            End If
            '官方给出的地址目前是 http，统一换成 https，避免中间设备干扰或被重定向打断
            SkinUrl = SkinUrl.Replace("http://", "https://")

            '下载皮肤
            '
            ' 原写法不检查 DownloadFile 的返回值，失败时后面的 Rename 会抛异常，
            ' 被外层 Catch 以 Slient 级别吞掉，用户只会看到「皮肤没加载」而不知原因。
            Dim FileName As String = Mid(SkinUrl, SkinUrl.LastIndexOf("/") + 2)
            Dim LocalFile As String = PATH & "PCL1_CE\cache\skin\" & FileName & ".png"
            If Not File.Exists(LocalFile) Then
                If Not DownloadFile(SkinUrl, LocalFile & DOWNLOADING_END) Then
                    log("[Skin] 皮肤文件下载失败，使用默认头像：" & SkinUrl, True)
                    Return GetSkinTypeFromUUID(UUID)
                End If
                File.Delete(LocalFile)
                FileSystem.Rename(LocalFile & DOWNLOADING_END, LocalFile)
                log("[System] 皮肤下载成功：" & FileName)
            End If
            WriteIni("cache\skin\SkinName", UUID, FileName)
            Return LocalFile

        Catch ex As Exception
            ExShow(ex, "下载皮肤失败（" & UUID & "）", ErrorLevel.Slient)
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 获取UUID对应的离线皮肤，返回“Steve”或“Alex”。
    ''' </summary>
    ''' <param name="UUID"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function GetSkinTypeFromUUID(ByVal UUID As String) As String
        If Not UUID.Length = 32 Then Return "Steve"
        Dim a = Integer.Parse(UUID(7), Globalization.NumberStyles.AllowHexSpecifier)
        Dim b = Integer.Parse(UUID(15), Globalization.NumberStyles.AllowHexSpecifier)
        Dim c = Integer.Parse(UUID(23), Globalization.NumberStyles.AllowHexSpecifier)
        Dim d = Integer.Parse(UUID(31), Globalization.NumberStyles.AllowHexSpecifier)
        Return If(((a Xor b) Xor (c Xor d)) Mod 2, "Alex", "Steve")
    End Function

#End Region

    Public Delegate Sub RefreshThemeInvoke()
    ''' <summary>刷新主题。</summary>
    ''' <remarks></remarks>
    Public Sub RefreshTheme()
        Try
            '读取数据
            Dim LeftbarUri As String = PATH_IMAGE & "LeftBar-BG_Blue.png"
            frmMain.imgMainBg.Source = Nothing
            If File.Exists(PATH & "PCL1_CE\top.png") Then LeftbarUri = PATH & "PCL1_CE\top.png"
            If File.Exists(PATH & "PCL1_CE\back.png") Then
                frmMain.imgMainBg.Source = New MyBitmap(PATH & "PCL1_CE\back.png")
                SetupRefreshBackground()
            End If
            ' 老配置里存着的这两个编号都会落进 Select Case 的空档，导致配色一个都不设置、
            ' 界面变成一片没有主题色的样子，所以这里统一按默认主题处理。
            Dim ThemeCode As Integer = Val(ReadIni("setup", "UiTheme", "0"))
            If ThemeCode = 4 OrElse ThemeCode = 101 Then ThemeCode = 0
            Select Case ThemeCode
                Case 0
                    Application.Current.Resources("Color1") = New SolidColorBrush(Color.FromRgb(213, 233, 255))
                    Application.Current.Resources("Color2") = New SolidColorBrush(Color.FromRgb(106, 177, 255))
                    Application.Current.Resources("Color3") = New SolidColorBrush(Color.FromRgb(0, 121, 255))
                    Application.Current.Resources("Color4") = New SolidColorBrush(Color.FromRgb(0, 81, 170))
                    Application.Current.Resources("Color5") = New SolidColorBrush(Color.FromRgb(0, 40, 85))
                    log("[System] 主题配色：蓝色")
                Case 1
                    LeftbarUri = PATH_IMAGE & "LeftBar-BG_Black.png"
                    Application.Current.Resources("Color1") = New SolidColorBrush(Color.FromRgb(212, 212, 212))
                    Application.Current.Resources("Color2") = New SolidColorBrush(Color.FromRgb(148, 148, 148))
                    Application.Current.Resources("Color3") = New SolidColorBrush(Color.FromRgb(127, 127, 127))
                    Application.Current.Resources("Color4") = New SolidColorBrush(Color.FromRgb(90, 90, 90))
                    Application.Current.Resources("Color5") = New SolidColorBrush(Color.FromRgb(42, 42, 42))
                    log("[System] 主题配色：黑色")
                Case 2
                    LeftbarUri = PATH_IMAGE & "LeftBar-BG_Orange.png"
                    Application.Current.Resources("Color1") = New SolidColorBrush(Color.FromRgb(255, 232, 213))
                    Application.Current.Resources("Color2") = New SolidColorBrush(Color.FromRgb(255, 168, 96))
                    Application.Current.Resources("Color3") = New SolidColorBrush(Color.FromRgb(255, 126, 21))
                    Application.Current.Resources("Color4") = New SolidColorBrush(Color.FromRgb(191, 86, 0))
                    Application.Current.Resources("Color5") = New SolidColorBrush(Color.FromRgb(106, 48, 0))
                    log("[System] 主题配色：橙色")
                Case 3
                    LeftbarUri = PATH_IMAGE & "LeftBar-BG_Green.png"
                    Application.Current.Resources("Color1") = New SolidColorBrush(Color.FromRgb(213, 255, 217))
                    Application.Current.Resources("Color2") = New SolidColorBrush(Color.FromRgb(97, 233, 111))
                    Application.Current.Resources("Color3") = New SolidColorBrush(Color.FromRgb(43, 213, 60))
                    Application.Current.Resources("Color4") = New SolidColorBrush(Color.FromRgb(29, 141, 40))
                    Application.Current.Resources("Color5") = New SolidColorBrush(Color.FromRgb(15, 70, 20))
                    log("[System] 主题配色：绿色")
                Case 100
                    LeftbarUri = PATH_IMAGE & "LeftBar-BG_Hunluan.png"
                    Application.Current.Resources("Color1") = New SolidColorBrush(Color.FromRgb(248, 231, 191))
                    Application.Current.Resources("Color2") = New SolidColorBrush(Color.FromRgb(244, 208, 125))
                    Application.Current.Resources("Color3") = New SolidColorBrush(Color.FromRgb(228, 165, 16))
                    Application.Current.Resources("Color4") = New SolidColorBrush(Color.FromRgb(182, 130, 20))
                    Application.Current.Resources("Color5") = New SolidColorBrush(Color.FromRgb(102, 71, 0))
                    log("[System] 主题配色：混乱")
            End Select
            Application.Current.Resources("ColorE1") = Application.Current.Resources("Color1").Color
            Application.Current.Resources("ColorE2") = Application.Current.Resources("Color2").Color
            Application.Current.Resources("ColorE3") = Application.Current.Resources("Color3").Color
            Application.Current.Resources("ColorE4") = Application.Current.Resources("Color4").Color
            Application.Current.Resources("ColorE5") = Application.Current.Resources("Color5").Color
            '改变控件图片
            If Not LeftbarUri = "" Then CType(frmMain.panTop.Background, Object).ImageSource = New MyBitmap(LeftbarUri)
            CType(frmMain.panTop.Background, Object).Stretch = Stretch.None
            CType(frmMain.panTop.Background, Object).TileMode = TileMode.Tile
            CType(CType(frmMain.panTop.Background, Object), ImageBrush).Viewport = (New RectConverter()).ConvertFromString("0,0," & CType(CType(frmMain.panTop.Background, Object).ImageSource, ImageSource).Width & "," & CType(CType(frmMain.panTop.Background, Object).ImageSource, ImageSource).Height)
            '改变已有控件
            'Dim E2NoAlpha As Color = Application.Current.Resources("ColorE2")
            'E2NoAlpha.A = 1
            'If Not IsNothing(frmHomeRight.SwapOldLab) Then CType(frmHomeRight.SwapOldLab.Background, SolidColorBrush).SetCurrentValue(SolidColorBrush.ColorProperty, E2NoAlpha)
            'If Not IsNothing(frmHomeRight.SwapVersionLab) Then CType(frmHomeRight.SwapVersionLab.Background, SolidColorBrush).SetCurrentValue(SolidColorBrush.ColorProperty, E2NoAlpha)
        Catch ex As Exception
            ExShow(ex, "刷新主题失败", ErrorLevel.Barrier)
        End Try
    End Sub

    ''' <summary>
    ''' 以正常方式结束程序运行，这会输出log与执行动画。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub EndNormal()
        AniStart({
            AaOpacity(frmMain, -1, 150),
            AaScaleTransform(frmMain.panAllBack, -0.05, 150,, New AniEaseJumpStart(0.4)),
            AaCode({"End"}, 250)
        }, "EndAll")
        log("[System] 收到正常关闭指令")
    End Sub
    ''' <summary>
    ''' 强制暴力结束程序执行。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub EndForce()
        On Error Resume Next
        Process.GetCurrentProcess.Kill()
        End
        My.Application.Shutdown()
        frmMain.Close()
        frmStart.Close()
    End Sub

    '动画事件
    Public Sub AaCodeCall(ByVal type As String, ByVal data As Array)
        Select Case type
            Case "HideInfoBox"
                data(1).Hide()
            Case "PageChange"
                frmMain.PageChange(data(1))
        End Select
    End Sub

    Private LogLock As New Object
    ''' <summary>
    ''' 输出Log。
    ''' </summary>
    ''' <param name="LogText">Log文本。</param>
    ''' <param name="Notice">是否为重要记录，如果为是则会给调试模式用户发送提示。</param>
    ''' <remarks></remarks>
    Public Sub Log(ByVal LogText As String, Optional ByVal Notice As Boolean = False, Optional DeveloperNotice As Boolean = False)
        Try
            Dim OutputText = "[" & GetTime() & "] " & LogText
            Debug.WriteLine(OutputText)
            If Not File.Exists(PATH & "PCL1_CE\log.txt") Then
                File.Create(PATH & "PCL1_CE\log.txt").Dispose()
            End If
            SyncLock LogLock
                Using Writter As New StreamWriter(PATH & "PCL1_CE\log.txt", True)
                    Writter.WriteLine(OutputText)
                    Writter.Close()
                End Using
            End SyncLock
            If Notice And MODE_DEBUG Then ShowHint(New HintConverter("[调试模式] " & LogText.Replace("[Error] ", ""), HintState.Warn))
            If DeveloperNotice And MODE_DEVELOPER Then ShowHint(New HintConverter("[开发者模式] " & LogText.Replace("[Error] ", ""), HintState.Info))
        Catch
        End Try
    End Sub

    ' 极易被拦截或误报。更新现在只做提示，由使用者自行到 GitHub Releases 下载。
    Private LastRefreshOffline As Boolean = False
    ''' <summary>
    ''' 刷新离线模式状态。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub RefreshOffline()
        '刷新状态：只根据真实网络状况判断，不再提供手动开关
        MODE_OFFLINE = Not My.Computer.Network.IsAvailable
        '检测是否变化
        If LastRefreshOffline = MODE_OFFLINE Then Exit Sub
        '各种UI的变化
        On Error Resume Next
        If Not IsNothing(frmMain.labTopVer) Then frmMain.labTopVer.Content = VERSION_NAME & If(MODE_DEVELOPER, " | 开发者模式", If(MODE_DEBUG, " | 调试模式", "")) & If(MODE_OFFLINE, " | 离线模式", "")
        If Not IsNothing(frmHomeLeft) Then frmHomeLeft.ChangeWidth()
        '更新状态
        LastRefreshOffline = MODE_OFFLINE
    End Sub

    ''' <summary>
    ''' 检查MC版本。
    ''' </summary>
    ''' <param name="Version">至少包含完整路径的MCVersion类。</param>
    ''' <param name="CheckInherit">是否对依赖版本进行递归检查。</param>
    ''' <remarks></remarks>
    Public Sub CheckMCVersion(ByRef Version As MCVersion, Optional ByVal CheckInherit As Boolean = False)

        '检查Json文件
        If Not File.Exists(Version.Path & Version.Name & ".json") Then
            If File.Exists(Version.Path & Version.Name & ".jar") Then
                '存在Jar，但是不存在Json
                Version.VersionCheckResult = VersionCheckState.JSON_NOT_EXIST
            Else
                'Jar与Json均不存在
                Version.VersionCheckResult = VersionCheckState.EMPTY_FOLDER
            End If
            Exit Sub
        End If

        '尝试读取Json
        Try
            If Version.JsonText.Length = 0 Then
            End If
        Catch
            Version.VersionCheckResult = VersionCheckState.JSON_CANT_READ
            Exit Sub
        End Try

        '检查Assets与依赖版本
        Try
            Version.InheritVersion = If(Version.Json("inheritsFrom"), "").ToString
            If IsNothing(Version.Json("assets")) Then
                If Version.InheritVersion = "" Then
                    Version.Assets = ""
                Else
                    Version.Assets = If(New MCVersion(Version.InheritVersion).Json("assets"), "").ToString
                End If
            Else
                Version.Assets = Version.Json("assets")
            End If
        Catch
            Version.Assets = ""
        End Try

        If Not Version.InheritVersion = "" Then
            '存在依赖版本
            If Not Directory.Exists(PATH_MC & "versions\" & Version.InheritVersion) Then
                '依赖版本不存在
                Version.VersionCheckResult = VersionCheckState.INHERITS_NOT_EXIST
                Exit Sub
            End If
            '检查依赖版本问题
            If CheckInherit Then
                Dim InheritVersion As New MCVersion(Version.InheritVersion)
                CheckMCVersion(InheritVersion)
                If Not InheritVersion.VersionCheckResult = VersionCheckState.NO_PROBLEM Then
                    '依赖版本存在问题
                    Version.VersionCheckResult = VersionCheckState.INHERITS_EXCEPTION
                    Exit Sub
                End If
            End If
        End If

        '获取全部引用
        Dim LibrariesName As New ArrayList
        Try
            For Each File As JToken In Version.Json("libraries")
                LibrariesName.Add(File("name").ToString)
            Next
        Catch
            Version.VersionCheckResult = VersionCheckState.JSON_CANT_READ
            Exit Sub
        End Try

        '获取主Jar路径
        For Each Library As String In LibrariesName

            If ReadIni("setup", "LaunchMending", "True") Then
                '允许自动修复
                If Library.Contains("net.minecraftforge") Or Library.Contains("optifine:OptiFine") Then
                    Version.VersionCheckResult = VersionCheckState.NO_PROBLEM
                    Exit Sub
                End If
            Else
                '不允许自动修复
                If Library.Contains("net.minecraftforge") Or Library.Contains("optifine:OptiFine") Then
                    '找到主Jar
                    If File.Exists(GetPathFromLibrary(Library)) Then
                        Version.VersionCheckResult = VersionCheckState.NO_PROBLEM
                    Else
                        Version.VersionCheckResult = VersionCheckState.JAR_NOT_EXIST
                    End If
                    Exit Sub
                End If
            End If

        Next

        '主Jar为原版

        If File.Exists(Version.Path & Version.Name & ".jar") Then
            Version.VersionCheckResult = VersionCheckState.NO_PROBLEM
        Else
            Version.VersionCheckResult = VersionCheckState.JAR_NOT_EXIST
        End If

    End Sub

    ''' <summary>
    ''' 从Json文件中的Library名获取文件所在的完整路径。
    ''' </summary>
    ''' <param name="Library"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function GetPathFromLibrary(ByVal Library As String) As String
        Dim SourceArray = Library.Split(":")
        Return PATH_MC & "libraries\" &
                                      SourceArray(0).Replace(".", "\") & "\" &
                                      SourceArray(1) & "\" &
                                      SourceArray(2) & "\" &
                                      SourceArray(1) & "-" & SourceArray(2) & ".jar"
    End Function

    Private IsSettingJavaEnvironment As Boolean = False
    ''' <summary>
    ''' 配置Java运行环境。必须在非主线程调用。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub SetJavaEnvironment()
        If IsSettingJavaEnvironment Then Exit Sub
        IsSettingJavaEnvironment = True

        '处理环境变量字符串，包括去重、去空白
        Dim Env As String = PathEnv
        Dim EnvironArray As Object() = ArrayNoDouble(Split(Env.Replace(";;", ";") & ";" & PATH_JAVA, ";")).ToArray
        Dim NewEnv As String = Join(EnvironArray, ";").Replace(";;", ";")
        Try
            ' 只在本次运行内记住这个 PATH，不去改系统环境变量、也不落地任何可执行文件：
            ' 启动游戏时始终直接用 PATH_JAVA 拼出 javaw 的完整路径，不需要系统 PATH 里有 java。
            PathEnv = NewEnv
        Catch ex As Exception
            PATH_JAVA = ""
            ExShow(ex, "设置环境变量时出现异常")
            ShowHint(New HintConverter("配置游戏环境失败：" & GetStringFromException(ex) & "（尝试重启电脑或关闭杀毒软件）", HintState.Critical))
            IsSettingJavaEnvironment = False
            Exit Sub
        End Try

        ''等待并检查
        'For i As Integer = 1 To 10
        '    Dim a = PathEnv
        '    Dim b = Mid(PATH_JAVA, 1, Len(PATH_JAVA) - 1)
        '    If PathEnv.Contains(Mid(PATH_JAVA, 1, Len(PATH_JAVA) - 1)) Then
        '        ShowHint(New HintConverter("配置游戏环境成功！", HintState.FINISH))
        '        IsSettingJavaEnvironment = False
        '        Exit Sub
        '    Else
        '        Thread.Sleep(2000)
        '    End If
        'Next i
        'ShowHint(New HintConverter("配置游戏环境失败！请尝试重启PCL或关闭杀毒软件。", HintState.CRITICAL))
        IsSettingJavaEnvironment = False
    End Sub

End Module

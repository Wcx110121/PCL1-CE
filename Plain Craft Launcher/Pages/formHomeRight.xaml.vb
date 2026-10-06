Public Class formHomeRight

    ''' <summary>
    ''' 是否已经初始化过本窗体。
    ''' </summary>
    ''' <remarks></remarks>
    Public FormLoaded As Boolean = False

    ''' <summary>
    ''' 窗体初始化。
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub Load(ByVal sender As Object, ByVal e As System.Windows.RoutedEventArgs) Handles panMain.Loaded
        '仅初始化一次
        If FormLoaded Then Exit Sub
        FormLoaded = True
        log("[HomeRight] 初始化开始")

        textLoginEmail.IsReadOnly = True
        textLoginEmail.Text = "点这里登录微软账号"
        ' 登录方式固定为微软正版账号
        ChangeLoginMethod(LoginMethods.Mojang, False)
        '版本信息已经由线程池委托加载

        '如果打开 PCL 在 7 次以内，提示切换版本列表的位置
        If Val(ReadIni("setup", "LastVersionCode", "0")) < 7 Then btnStartMore.ToolTip = "切换版本"

        log("[HomeRight] 初始化结束")
    End Sub

#Region "Top | 登录方式"

    '各个状态下的透明度
    Private Const LAUNCH_SELECT As Double = 1
    Private Const LAUNCH_MOUSEIN As Double = 0.8
    Private Const LAUNCH_MOUSEOUT As Double = 0.5

    '统一的动画时长
    Private Const LAUNCH_LENGTH As Integer = 100
    Private Const LAUNCH_NEWPAGE As Integer = 100
    Private Const LAUNCH_OLDPAGE_DELAY As Integer = 20

    Private _LoginMethod As LoginMethods = LoginMethods.Unknown
    ''' <summary>
    ''' 目前的登录方式。
    ''' </summary>
    ''' <remarks></remarks>
    Public Property LoginMethod As LoginMethods
        Get
            Return _LoginMethod
        End Get
        Set(ByVal value As LoginMethods)
            _LoginMethod = value
            StartButtonRefresh()
        End Set
    End Property
    ''' <summary>
    ''' 切换登录方式。
    ''' </summary>
    ''' <param name="NewMethod">新的登录方式。</param>
    ''' <param name="UseAnimation">是否在切换方式时使用动画。</param>
    ''' <remarks></remarks>
    Public Sub ChangeLoginMethod(ByVal NewMethod As LoginMethods, Optional ByVal UseAnimation As Boolean = True)
        ' 登录方式固定为微软正版账号
        ' 参数保留只是为了避免改动调用方，传什么进来都会被忽略。
        If LoginMethod = LoginMethods.Mojang Then Exit Sub

        '记录新的数据
        LoginMethod = LoginMethods.Mojang
        log("[HomeRight] 切换登录方式：" & GetStringFromEnum(LoginMethod))

        '从目前的登录结果判断页面类型
        If LoginResult = "" Or IsMojangHeadLoaded = False Then
            ChangeLoginPage(LoginPages.Login, UseAnimation)
        Else
            ChangeLoginPage(LoginPages.Mojang, UseAnimation)
        End If

        '改变顶部条的显示：只剩正版一个入口，常亮即可
        AniStop("btnTopMojangMouse")
        btnTopMojang.IsHitTestVisible = False
        If UseAnimation Then
            AniStart({AaOpacity(btnTopMojang, LAUNCH_SELECT - btnTopMojang.Opacity, LAUNCH_LENGTH)}, "HomeRightChangeLoginMethod")
        Else
            btnTopMojang.Opacity = LAUNCH_SELECT
        End If

    End Sub

    ''' <summary>
    ''' 目前的登录页面种类。
    ''' </summary>
    ''' <remarks></remarks>
    Private LoginPage As LoginPages = LoginPages.Unknown
    ''' <summary>
    ''' 登录页面的种类。
    ''' </summary>
    ''' <remarks></remarks>
    Private Enum LoginPages As Byte
        Mojang = 0
        Login = 2
        Unknown = 3
    End Enum
    ''' <summary>
    ''' 切换显示的登录页面。
    ''' </summary>
    ''' <param name="NewPage">新的页面编号。</param>
    ''' <param name="UseAnimation">是否在切换页面时使用动画。</param>
    ''' <remarks></remarks>
    Private Sub ChangeLoginPage(ByVal NewPage As LoginPages, Optional ByVal UseAnimation As Boolean = True)
        If NewPage = LoginPage Then Exit Sub

        '切换页面
        Select Case NewPage
            Case LoginPages.Login
                panLogin.Visibility = Visibility.Visible
                If UseAnimation Then
                    panLogin.Opacity = 0
                    AniStart({
                             AaOpacity(panMojang, -panMojang.Opacity, LAUNCH_NEWPAGE, LAUNCH_OLDPAGE_DELAY),
                             AaOpacity(panLogin, 1 - panLogin.Opacity, LAUNCH_NEWPAGE),
                             AaCode({"Visible", panMojang, False})
                         }, "ChangeLoginPage")
                Else
                    panLogin.Opacity = 1
                    panMojang.Visibility = Visibility.Collapsed
                End If
            Case LoginPages.Mojang
                panMojang.Visibility = Visibility.Visible
                If UseAnimation Then
                    panMojang.Opacity = 0
                    AniStart({
                             AaOpacity(panLogin, -panLogin.Opacity, LAUNCH_NEWPAGE, LAUNCH_OLDPAGE_DELAY),
                             AaOpacity(panMojang, 1 - panMojang.Opacity, LAUNCH_NEWPAGE),
                             AaCode({"Visible", panLogin, False})
                         }, "ChangeLoginPage")
                Else
                    panMojang.Opacity = 1
                    panLogin.Visibility = Visibility.Collapsed
                End If
        End Select

        '记录新的数据
        LoginPage = NewPage
        log("[HomeRight] 切换登录页面：" & GetStringFromEnum(LoginPage))

    End Sub

#Region "鼠标动画"
    Private Sub btnLaunch_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnTopMojang.MouseEnter
        '不可用时的动画用IsHitTestVisible和平解决
        If Not sender.IsHitTestVisible Then Exit Sub
        AniStart({AaOpacity(sender, LAUNCH_MOUSEIN - sender.Opacity, LAUNCH_LENGTH)}, sender.Name & "Mouse")
    End Sub
    Private Sub btnLaunch_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnTopMojang.MouseLeave
        If Not sender.IsHitTestVisible Then Exit Sub
        AniStart({AaOpacity(sender, LAUNCH_MOUSEOUT - sender.Opacity, LAUNCH_LENGTH)}, sender.Name & "Mouse")
    End Sub
#End Region

#End Region

#Region "Login | 未登录页"

    ''' <summary>
    ''' 点击登录面板就直接开始微软账号登录，等价于点「启动」按钮。
    ''' </summary>
    Private Sub panLogin_MouseLeftButtonUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles panLogin.MouseLeftButtonUp
        ' 只要处于「正版标签 + 未登录」就允许点，不再依赖按钮当前处于哪个状态
        If LoginMethod <> LoginMethods.Mojang Then Exit Sub
        If LoginResult <> "" Then Exit Sub
        If StartButtonIsLogining Then Exit Sub
        If MODE_OFFLINE Then
            ShowHint(New HintConverter("没有网络连接，无法登录", HintState.Warn))
            Exit Sub
        End If
        Pool.Add(New Thread(Sub() MSALoginInteractiveFlow()))
    End Sub


    ' 原「查看密码」「Tab 切到密码框」的交互随密码框一起去掉了。

#End Region

#Region "Mojang | 已登录页"

    '退出按钮的展开/收回
    Private Sub panMojang_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles panMojang.MouseEnter
        AniStart({
                 AaWidth(btnMojangLogout, 18 - btnMojangLogout.ActualWidth, 100, , New AniEaseEnd),
                 AaHeight(btnMojangLogout, 20 - btnMojangLogout.ActualHeight, 100, , New AniEaseEnd),
                 AaWidth(labMojangLogoutPosition, -labMojangLogoutPosition.ActualWidth, 100, , New AniEaseEnd)
             }, "HomeRightMojangLogout")
    End Sub
    Private Sub panMojang_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles panMojang.MouseLeave
        AniStart({
                 AaWidth(btnMojangLogout, -btnMojangLogout.ActualWidth, 100, , New AniEaseStart),
                 AaHeight(btnMojangLogout, -btnMojangLogout.ActualHeight, 100, , New AniEaseStart),
                 AaWidth(labMojangLogoutPosition, 10 - labMojangLogoutPosition.ActualWidth, 100, , New AniEaseStart)
             }, "HomeRightMojangLogout")
    End Sub

    '退出按钮的点击
    Private Sub panMojangInner_MouseLeftButtonUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles panMojangInner.MouseLeftButtonUp
        If e.GetPosition(panMojangInner).X - 10 - labMojangName.ActualWidth < 0 Then Exit Sub '退出的Path不完整所以不拥有完整的碰撞箱
        Logout()
    End Sub

    ''' <summary>
    ''' 退出登录。这会清空所有的登录信息。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub Logout()
        log("[HomeRight] 退出正版登录")
        LoginResult = ""
        textLoginEmail.Text = "点这里登录微软账号"
        WriteReg("AccessToken", "")
        WriteReg("ClientToken", "")
        WriteReg("MSARefreshToken", "")
        WriteReg("MojangPlayerUUID", "")
        WriteReg("MojangPlayerName", "")
        IsMojangHeadLoaded = False
        timerMojangCheck.Tag = False
        timerMojangCheck.IsEnabled = True
        ChangeLoginPage(LoginPages.Login)
        StartButtonRefresh()
    End Sub

    '点击头像，打开皮肤页面
    Private Sub imgMojangHead_MouseLeftButtonDown(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles imgMojangHead2.MouseLeftButtonDown
        Process.Start("https://minecraft.net/zh-hans/profile")
    End Sub

    ''' <summary>
    ''' 是否加载过正版头像。
    ''' </summary>
    ''' <remarks></remarks>
    Public IsMojangHeadLoaded As Boolean = False
    ''' <summary>
    ''' 刷新正版头像。
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function LoadMojangSkin(ByVal SkinAddress As String) As Boolean

        '判断直接图片
        Select Case SkinAddress
            Case "Steve"
                log("[HomeRight] 载入正版头像：Steve")
                imgMojangHead1.Source = Nothing
                imgMojangHead2.Source = New MyBitmap(PATH_IMAGE & "steve.png")
                IsMojangHeadLoaded = True
                Return True
            Case "Alex"
                log("[HomeRight] 载入正版头像：Alex")
                imgMojangHead1.Source = Nothing
                imgMojangHead2.Source = New MyBitmap(PATH_IMAGE & "alex.png")
                IsMojangHeadLoaded = True
                Return True
        End Select

        '刷新头像
        Try
            If Not File.Exists(SkinAddress) Then
                log("[HomeRight] 载入正版头像失败：文件 " & SkinAddress & " 不存在")
                Return False
            End If
            imgMojangHead1.Source = New CroppedBitmap(New MyBitmap(SkinAddress), New Int32Rect(40, 8, 8, 8))
            imgMojangHead2.Source = New CroppedBitmap(New MyBitmap(SkinAddress), New Int32Rect(8, 8, 8, 8))
            log("[HomeRight] 载入正版头像成功")
            IsMojangHeadLoaded = True
            Return True
        Catch ex As Exception
            log("[HomeRight] 载入正版头像失败：" & GetStringFromException(ex), True)
            Return False
        End Try

    End Function

    Private Sub timerMojangCheck_Tick() Handles timerMojangCheck.Tick
        Try
            '确保控件已经加载
            If IsNothing(panMojang) Or IsNothing(labMojangName) Or IsNothing(panMain) Or LoginResult = "" Then Exit Sub

            '判断皮肤状态
            If IsMojangHeadLoaded Then
                '已经由登录线程加载了头像
                timerMojangCheck.IsEnabled = False
            Else
                '缓存头像只尝试一次
                If timerMojangCheck.Tag = True Then Exit Sub
                timerMojangCheck.Tag = True
                '尚未加载头像，试图加载缓存头像
                Dim UUID = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(LoginResult), JObject)("selectedProfile")("id").ToString
                Dim SkinAddress As String = ReadIni("cache\skin\SkinName", UUID)
                If Not SkinAddress = "" Then
                    SkinAddress = PATH & "PCL1_CE\cache\skin\" & SkinAddress & ".png"
                    log("[HomeRight] 从缓存加载正版头像：" & SkinAddress)
                    LoadMojangSkin(SkinAddress)
                End If
                '如果加载失败则退出
                If Not IsMojangHeadLoaded Then Exit Sub
            End If

            '加载用户信息
            Dim LoginJson = CType(Newtonsoft.Json.JsonConvert.DeserializeObject(LoginResult), JObject)
            labMojangName.Content = LoginJson("selectedProfile")("name").ToString

            '显示登陆页
            If LoginMethod = LoginMethods.Mojang And IsMojangHeadLoaded Then ChangeLoginPage(LoginPages.Mojang)

        Catch ex As Exception
            log("[HomeRight] 正版页检测刻异常：" & GetStringFromException(ex, True), True)
        End Try
    End Sub

#End Region

#Region "Start | 开始游戏"

#Region "状态切换"

    ''' <summary>
    ''' 开始游戏按钮状态枚举。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum StartButtonState As Byte
        ''' <summary>
        ''' 正常状态。
        ''' </summary>
        ''' <remarks></remarks>
        Normal = 0
        ''' <summary>
        ''' 加载版本列表中。
        ''' </summary>
        ''' <remarks></remarks>
        Loading = 1
        ''' <summary>
        ''' 没有可用版本。
        ''' </summary>
        ''' <remarks></remarks>
        NoVersion = 2
        ''' <summary>
        ''' 启动 Minecraft 中。
        ''' </summary>
        ''' <remarks></remarks>
        Launching = 3
        ''' <summary>
        ''' 需要登录。
        ''' </summary>
        ''' <remarks></remarks>
        Login = 4
        ''' <summary>
        ''' 正在登录。
        ''' </summary>
        Logining = 5
    End Enum

    Private _StartButtonIsLogining As Boolean = False
    ''' <summary>
    ''' 是否正在登录。
    ''' </summary>
    ''' <returns></returns>
    Public Property StartButtonIsLogining As Boolean
        Get
            Return _StartButtonIsLogining
        End Get
        Set(value As Boolean)
            _StartButtonIsLogining = value
            frmMain.Dispatcher.Invoke(Sub() StartButtonRefresh())
        End Set
    End Property

    Private _StartButtonIsLoading As Boolean = False
    ''' <summary>
    ''' 是否正在加载版本。
    ''' </summary>
    ''' <returns></returns>
    Public Property StartButtonIsLoading As Boolean
        Get
            Return _StartButtonIsLoading
        End Get
        Set(value As Boolean)
            _StartButtonIsLoading = value
            If value = False Then frmStart.IsVersionLoading = False
            frmMain.Dispatcher.Invoke(Sub() StartButtonRefresh())
        End Set
    End Property

    Private _StartButtonIsLaunching As Boolean = False
    ''' <summary>
    ''' 是否正在启动。
    ''' </summary>
    ''' <returns></returns>
    Public Property StartButtonIsLaunching As Boolean
        Get
            Return _StartButtonIsLaunching
        End Get
        Set(value As Boolean)
            _StartButtonIsLaunching = value
            frmMain.Dispatcher.Invoke(Sub() StartButtonRefresh())
        End Set
    End Property

    Private _StartButtonCurrent As StartButtonState = StartButtonState.Loading
    ''' <summary>
    ''' 开始游戏按钮的当前状态。
    ''' </summary>
    ''' <remarks></remarks>
    Public ReadOnly Property StartButtonCurrent As StartButtonState
        Get
            Return _StartButtonCurrent
        End Get
    End Property

    ''' <summary>
    ''' 刷新开始按钮状态。
    ''' </summary>
    Public Sub StartButtonRefresh()
        'panStart.UpdateLayout()
        'btnStartMore.UpdateLayout()
        'pathStart1.UpdateLayout()
        'pathStart2.UpdateLayout()
        'pathStart3.UpdateLayout()
        'labStartTop.UpdateLayout()
        'labStartButtom.UpdateLayout()

        '确定状态
        Dim NewValue As StartButtonState
        If StartButtonIsLaunching Then
            NewValue = StartButtonState.Launching
        ElseIf StartButtonIsLoading Then
            NewValue = StartButtonState.Loading
        ElseIf StartButtonIsLogining And LoginMethod = LoginMethods.Mojang Then
            NewValue = StartButtonState.Logining
        ElseIf LoginResult = "" And LoginMethod = LoginMethods.Mojang Then
            ' 登录判断必须排在「没有版本」之前。
            ' 原来「没有版本」的优先级更高，导致版本列表为空时按钮显示成「下载游戏」，
            ' 微软登录的入口完全不可达（在真实 XP 上踩到了这个问题）。
            NewValue = StartButtonState.Login
        ElseIf SelectVersion.Name = "" Then
            NewValue = StartButtonState.NoVersion
        Else
            NewValue = StartButtonState.Normal
        End If

        '如果不变则跳过
        If StartButtonCurrent = NewValue Then Exit Sub

        '切换事件
        Select Case NewValue
            Case StartButtonState.Normal
                labStartTop.Content = "开始游戏"
                labStartButtom.Content = SelectVersion.Name
                btnStartMore.IsHitTestVisible = True
                btnStart.IsHitTestVisible = True
                StartRightShow()
                StartButtomShow()
            Case StartButtonState.Loading
                labStartTop.Content = "加载中"
                SelectVersion = New MCVersion("")
                btnStartMore.IsHitTestVisible = False
                btnStart.IsHitTestVisible = False
                StartRightHide()
                HideVersion()
                StartButtomHide()
            Case StartButtonState.NoVersion
                labStartTop.Content = "下载游戏"
                SelectVersion = New MCVersion("")
                btnStartMore.IsHitTestVisible = False
                btnStart.IsHitTestVisible = True
                StartRightHide()
                StartButtomHide()
            Case StartButtonState.Launching
                labStartTop.Content = "游戏启动中"
                labStartButtom.Content = "初始化中"
                btnStartMore.IsHitTestVisible = False
                btnStart.IsHitTestVisible = False
                StartRightHide()
                StartButtomShow()
            Case StartButtonState.Login
                labStartTop.Content = "登录"
                btnStartMore.IsHitTestVisible = True
                btnStart.IsHitTestVisible = True
                StartRightShow()
                StartButtomHide()
            Case StartButtonState.Logining
                labStartTop.Content = "正在登录"
                btnStartMore.IsHitTestVisible = True
                btnStart.IsHitTestVisible = False
                StartRightShow()
                StartButtomHide()
        End Select

        _StartButtonCurrent = NewValue
    End Sub

#End Region

#Region "指向动画"

    Private Sub btnStart_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnStart.MouseEnter
        AniStart({
                 AaOpacity(rectStart, 0.93 - rectStart.Opacity, 100)
             }, "HomeRightStart", False)
    End Sub
    Private Sub btnStart_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnStart.MouseLeave
        AniStart({
                 AaOpacity(rectStart, 1 - rectStart.Opacity, 100)
             }, "HomeRightStart", False)
    End Sub
    Private Sub btnStartMore_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnStartMore.MouseEnter
        AniStart({
                 AaY(pathStart1, -0.8, 100),
                 AaY(pathStart3, 0.8, 100)
             }, GetUUID)
    End Sub
    Private Sub btnStartMore_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnStartMore.MouseLeave
        AniStart({
                 AaY(pathStart1, 0.8, 100),
                 AaY(pathStart3, -0.8, 100)
             }, GetUUID)
    End Sub

#End Region

#Region "UI 界面入口"

    '显示/隐藏 开始按钮右边的版本列表选择
    Public Sub StartRightHide()
        frmMain.Dispatcher.Invoke(Sub()
                                      AniStart(New Animation() {
                                               AaWidth(Me.btnStartMore, 12.0 - Me.btnStartMore.Width, 70, , New AniEaseEnd),
                                               AaOpacity(Me.pathStart1, -Me.pathStart1.Opacity, 70),
                                               AaOpacity(Me.pathStart2, -Me.pathStart2.Opacity, 70),
                                               AaOpacity(Me.pathStart3, -Me.pathStart3.Opacity, 70)
                                          }, "HomeRightStartButtonRight")
                                  End Sub)
    End Sub
    Public Sub StartRightShow()
        frmMain.Dispatcher.Invoke(Sub()
                                      AniStart(New Animation() {
                                                             AaWidth(Me.btnStartMore, 36.0 - Me.btnStartMore.Width, 70,, New AniEaseEnd),
                                                             AaOpacity(Me.pathStart1, 1.0 - Me.pathStart1.Opacity, 70),
                                                             AaOpacity(Me.pathStart2, 1.0 - Me.pathStart2.Opacity, 70),
                                                             AaOpacity(Me.pathStart3, 1.0 - Me.pathStart3.Opacity, 70)
                                                 }, "HomeRightStartButtonRight")
                                  End Sub)
    End Sub

    '显示/隐藏 开始按钮下的小字
    Public Sub StartButtomHide()
        frmMain.Dispatcher.Invoke(Sub()
                                      AniStart({
                                                    AaY(labStartTop, 17 - labStartTop.Margin.Top, 70, , New AniEaseEnd),
                                                    AaOpacity(labStartButtom, -labStartButtom.Opacity, 70),
                                                    AaY(labStartButtom, 40 - labStartButtom.Margin.Top, 70, , New AniEaseEnd)
                                                }, "StartButtonButtom")
                                  End Sub)
    End Sub
    Public Sub StartButtomShow()
        frmMain.Dispatcher.Invoke(Sub()
                                      AniStart({
                                                    AaY(labStartTop, 10 - labStartTop.Margin.Top, 70, , New AniEaseStart),
                                                    AaOpacity(labStartButtom, 0.5 - labStartButtom.Opacity, 70),
                                                    AaY(labStartButtom, 35 - labStartButtom.Margin.Top, 70, , New AniEaseStart)
                                                }, "StartButtonButtom")
                                  End Sub)
    End Sub

    '设置 开始按钮下的小字
    Public Sub StartButtomSet(Text As String)
        frmHomeRight.Dispatcher.Invoke(Sub() labStartButtom.Content = Text)
    End Sub

    Private Const START_PROCESS_TIME As Integer = 2000
    Private _StartProcess As Double = 0
    Public Property StartProcess As Double
        Get
            Return _StartProcess
        End Get
        Set(value As Double)
            '设置 0 进度会被视为初始化，所以在属性不变时不会取消事件执行
            frmHomeRight.Dispatcher.Invoke(Sub()
                                               Select Case value
                                                   Case 0
                                                       '初始化控件（主要是复原完成动画改变的属性）
                                                       AniStop("StartProcess")
                                                       rectStartProcess.Width = 0
                                                       rectStartProcess.Opacity = 1
                                                       rectStartProcess.Background = FindResource("Color3")
                                                   Case 1
                                                       '启动成功
                                                       '首先按照正常动画的流程进行，让进度条跑满，然后消失结束（进度条消失）
                                                       Dim Delta As Double = rectStart.ActualWidth - rectStartProcess.Width
                                                       AniStart({
                                                                     AaWidth(rectStartProcess, Delta, START_PROCESS_TIME * (Math.Abs(Delta) / rectStart.ActualWidth)),
                                                                     AaCode(Sub() labStartTop.Content = "游戏启动成功", , True),
                                                                     AaCode(Sub() StartButtomHide()),
                                                                     AaBackGround(rectStartProcess, New MyColor(0, 166, 0) - rectStartProcess.Background, 70),
                                                                     AaOpacity(rectStartProcess, -rectStartProcess.Opacity, 70, 1530,, True),
                                                                     AaCode(Sub() frmHomeRight.StartButtonIsLaunching = False, 1600)
                                                            }, "StartProcess")
                                                   Case -1
                                                       '取消
                                                       AniStart({
                                                                         AaWidth(rectStartProcess, -rectStartProcess.ActualWidth, 200),
                                                                         AaOpacity(rectStartProcess, -rectStartProcess.Opacity, 70, 180,, True),
                                                                         AaCode(Sub() frmHomeRight.StartButtonIsLaunching = False, 250)
                                                                }, "StartProcess")
                                                   Case -2
                                                       '启动失败
                                                       AniStart({
                                                                         AaWidth(rectStartProcess, -rectStartProcess.ActualWidth, 200),
                                                                         AaCode(Sub() labStartTop.Content = "游戏启动失败", , True),
                                                                         AaWidth(rectStartProcess, rectStart.ActualWidth, 0),
                                                                         AaCode(Sub() StartButtomHide()),
                                                                         AaBackGround(rectStartProcess, New MyColor(223, 56, 0) - rectStartProcess.Background, 70),
                                                                         AaOpacity(rectStartProcess, -rectStartProcess.Opacity, 70, 1530,, True),
                                                                         AaCode(Sub() frmHomeRight.StartButtonIsLaunching = False, 1600)
                                                                }, "StartProcess")
                                                   Case Else
                                                       If _StartProcess = value Then Exit Sub
                                                       '正常动画
                                                       Dim Delta As Double = rectStart.ActualWidth * value - rectStartProcess.Width
                                                       '预计 2s 完成整个动画，用差值进行匀速处理，让进度条匀速增长
                                                       AniStart({
                                                                     AaWidth(rectStartProcess, Delta, START_PROCESS_TIME * (Math.Abs(Delta) / rectStart.ActualWidth))
                                                            }, "StartProcess")
                                               End Select
                                           End Sub)

            _StartProcess = value
        End Set
    End Property

#End Region

    Public Sub ClickStartButton() Handles btnStart.MouseLeftButtonUp
        Select Case StartButtonCurrent
            Case StartButtonState.NoVersion
                If MODE_OFFLINE Then
                    ShowHint(New HintConverter("没有网络连接，无法下载", HintState.Warn))
                Else
                    If frmMain.btnTopDown.Visibility = Visibility.Visible Then
                        frmMain.PageChange("下载")
                    Else
                        ShowHint("下载页面已被隐藏")
                    End If
                End If
            Case StartButtonState.Normal
                StartButtonIsLaunching = True
                Dim th As New Thread(Sub()
                                         GameStart(New GameStartInfo With {
                                                   .LoginMethod = LoginMethods.Mojang,
                                                   .UserName = ReadJson(LoginResult)("selectedProfile")("name").ToString(),
                                                   .SelectVersion = SelectVersion
                                                   })
                                     End Sub)
                th.Start()
            Case StartButtonState.Login
                If MODE_OFFLINE Then
                    ShowHint(New HintConverter("没有网络连接，无法登录", HintState.Warn))
                Else
                    ' 改为微软账号设备码登录，不再需要邮箱与密码。
                    ' 登录流程放在后台线程里跑，中途会弹窗让用户去 microsoft.com/link 输入代码。
                    Pool.Add(New Thread(Sub() MSALoginInteractiveFlow()))
                End If
        End Select
    End Sub

#End Region

#Region "Update | 更新"

    ''' <summary>
    ''' 显示更新。
    ''' </summary>
    ''' <param name="Info">显示的提示信息。</param>
    ''' <param name="Type">提示信息的种类。</param>
    ''' <remarks></remarks>
    Public Sub ShowUpdate(ByVal Info As String, ByVal Type As UpdateType)
        btnUpdate.Content = Info
        ShowUpdateType = Type
        Dim AniArray As New ArrayList From {
                    AaHeight(panLaunch, 304 - panLaunch.Height, , , New AniEaseEnd),
                    AaOpacity(btnUpdate, 0.6)
                }
        AniStart(AniArray, "HomeRightShowUpdate")
    End Sub

    Private ShowUpdateType As UpdateType = UpdateType.PCL
    ''' <summary>
    ''' 显示的更新种类。
    ''' </summary>
    ''' <remarks></remarks>
    Public Enum UpdateType As Byte
        ''' <summary>
        ''' Minecraft 更新。
        ''' </summary>
        ''' <remarks></remarks>
        MINECRAFT = 0
        ''' <summary>
        ''' PCL 更新。
        ''' </summary>
        ''' <remarks></remarks>
        PCL = 1
    End Enum

    Private Sub btnUpdate_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnUpdate.MouseEnter
        If sender.Opacity < 0.5 Then Exit Sub '不小心点到的？
        AniStart({
                 AaOpacity(btnUpdate, 1 - btnUpdate.Opacity, 150)
             }, "btnUpdateMouse")
    End Sub
    Private Sub btnUpdate_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnUpdate.MouseLeave
        If sender.Opacity < 0.5 Then Exit Sub '不小心点到的？

        AniStart({
                 AaOpacity(btnUpdate, 0.6 - btnUpdate.Opacity, 150)
             }, "btnUpdateMouse")
    End Sub

    Private Sub btnUpdate_MouseLeftButtonUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles btnUpdate.MouseLeftButtonUp
        If sender.Opacity < 0.5 Then Exit Sub '不小心点到的？

        Select Case ShowUpdateType
            Case UpdateType.MINECRAFT
                frmMain.PageChange("下载")
                AniStart({
                        AaHeight(panLaunch, 277 - panLaunch.Height, , , New AniEaseEnd),
                        AaOpacity(btnUpdate, -btnUpdate.Opacity, 200)
                    }, "HomeRightHideUpdate")
            Case UpdateType.PCL
                ' 更新改为「仅提示」：不再自动下载更新包并覆盖程序文件
                ' （更新包地址已失效，且自替换在杀毒软件下极易被拦截），
                ' 点击提示后直接打开 GitHub Releases 页面，由使用者自行下载。
                Dim UpdateMessage As String = "检测到启动器新版本。" & vbCrLf &
                    "当前版本：" & VERSION_NAME & vbCrLf &
                    "最新版本：" & LatestLauncherVersion & vbCrLf & vbCrLf &
                    "是否现在打开下载页面？" & vbCrLf & UPDATE_URL
                If MyMsgbox(UpdateMessage, "发现启动器更新", "打开下载页", "稍后再说") = 1 Then
                    Process.Start(UPDATE_URL)
                    AniStart({
                            AaHeight(panLaunch, 277 - panLaunch.Height, , , New AniEaseEnd),
                            AaOpacity(btnUpdate, -btnUpdate.Opacity, 200)
                        }, "HomeRightHideUpdate")
                End If
        End Select
    End Sub

#End Region

#Region "Version | 版本选择"

#Region "鼠标动画"

    Private Sub btnVersionBack_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnVersionBack.MouseEnter
        AniStart({
                AaOpacity(pathVersionBack, 1 - pathVersionBack.Opacity, 100),
                AaOpacity(labVersionBack, 1 - labVersionBack.Opacity, 100)
            }, "btnVersionBackMouse")
    End Sub
    Private Sub btnVersionBack_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles btnVersionBack.MouseLeave
        AniStart({
                AaOpacity(pathVersionBack, 0.8 - pathVersionBack.Opacity, 100),
                AaOpacity(labVersionBack, 0.8 - labVersionBack.Opacity, 100)
            }, "btnVersionBackMouse")
    End Sub

    Private Sub panVersionHost_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles panVersionHostSWAP.MouseEnter, panVersionHostOLD.MouseEnter, panVersionHostWRONG.MouseEnter
        AniStart({
                AaBackGround(sender, New MyColor(25, Application.Current.Resources("ColorE3")) - New MyColor(CType(sender.Background, SolidColorBrush).Color), 100)
            }, sender.Name & "Mouse")
    End Sub
    Private Sub panVersionHost_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles panVersionHostSWAP.MouseLeave, panVersionHostOLD.MouseLeave, panVersionHostWRONG.MouseLeave
        AniStart({
                AaBackGround(sender, New MyColor(0, Application.Current.Resources("ColorE3")) - New MyColor(CType(sender.Background, SolidColorBrush).Color), 100)
            }, sender.Name & "Mouse")
    End Sub

#End Region

    ''' <summary>
    ''' 显示版本选择列表。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub ShowVersionList() Handles btnStartMore.MouseLeftButtonUp
        Try

            '处理动画
            panVersion.IsHitTestVisible = True
            Dim AniArray As New ArrayList From {
            AaOpacity(panLaunch, -panLaunch.Opacity, 150),
            AaOpacity(panVersion, 1 - panVersion.Opacity, 150, 110),
            AaCode({"Visible", panLaunch, "False"}, 150)
        }
            Dim AniDelay As Integer = 150
            For Each Item As ListItem In panVersionNORMAL.Children
                Item.Opacity = 0
                AniArray.Add(AaOpacity(Item, 1, 100, AniDelay))
                AniDelay = AniDelay + 25
            Next
            panVersionHostSWAP.Opacity = 0
            AniArray.Add(AaOpacity(panVersionHostSWAP, 1, 100, AniDelay))
            AniDelay = AniDelay + 25
            panVersionHostOLD.Opacity = 0
            AniArray.Add(AaOpacity(panVersionHostOLD, 1, 100, AniDelay))
            AniDelay = AniDelay + 25
            panVersionHostWRONG.Opacity = 0
            AniArray.Add(AaOpacity(panVersionHostWRONG, 1, 100, AniDelay))
            AniStart(AniArray, "HomeRightShowVersionList")

            '背景处理
            panBackBounds = panVersion
            panBackHeightChange(panVersion, Nothing)

            '收回展开
            If Not pathVersionHostOLD.Data.ToString.Equals("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8") Then panVersionHost_MouseLeftButtonUp(panVersionHostOLD, Nothing)
            If Not pathVersionHostSWAP.Data.ToString.Equals("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8") Then panVersionHost_MouseLeftButtonUp(panVersionHostSWAP, Nothing)
            If Not pathVersionHostWRONG.Data.ToString.Equals("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8") Then panVersionHost_MouseLeftButtonUp(panVersionHostWRONG, Nothing)

            '自动展开
            If panVersionNORMAL.Children.Count = 0 Then
                If Not panVersionSWAP.Children.Count = 0 Then panVersionHost_MouseLeftButtonUp(panVersionHostSWAP, Nothing) : Exit Sub
                If Not panVersionOLD.Children.Count = 0 Then panVersionHost_MouseLeftButtonUp(panVersionHostOLD, Nothing) : Exit Sub
                If Not panVersionWRONG.Children.Count = 0 Then panVersionHost_MouseLeftButtonUp(panVersionHostWRONG, Nothing) : Exit Sub
            End If

            '提示信息
            If ReadReg("VersionDeleteHint") = "" Then
                WriteReg("VersionDeleteHint", "True")
                ShowHint("你可以通过右键一个版本进行删除")
            End If

        Catch ex As Exception
            ExShow(ex, "展开版本列表失败", ErrorLevel.Barrier)
        End Try
    End Sub

    Private _SelectVersion As New MCVersion("")
    ''' <summary>
    ''' 当前选择的MC版本。
    ''' </summary>
    ''' <remarks></remarks>
    Public Property SelectVersion As MCVersion
        Get
            Return _SelectVersion
        End Get
        Set(ByVal value As MCVersion)
            _SelectVersion = value
            labStartButtom.UpdateLayout()
            If Not IsNothing(SelectVersion.Name) Then If StartButtonCurrent = StartButtonState.Normal Then labStartButtom.Content = SelectVersion.Name
        End Set
    End Property

    ''' <summary>
    ''' 将VersionsList的MCVersion对象加载为UI支持库。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub LoadVersionList()
        log("[HomeRight] UI线程加载版本列表开始")
        panVersion.UpdateLayout() '防止控件未加载导致炸掉
        Try

            '读取应该选择的版本。参数高于历史记录。
            Dim ShouldSelectVersion As String = GetProgramArgument("Version", ReadIni("setup", "History"))

            '清空控件
            panVersionNORMAL.Children.Clear()
            panVersionSWAP.Children.Clear()
            panVersionOLD.Children.Clear()
            panVersionWRONG.Children.Clear()
            panVersionSWAP.Height = 0
            panVersionOLD.Height = 0
            panVersionWRONG.Height = 0

            '添加控件
            For Each VerType As VersionSwapState In VersionsList.Keys

                '获取对应控件
                Dim GridHost As Grid = FindName("panVersionHost" & GetStringFromEnum(VerType))
                Dim LabelHost As Label = FindName("labVersionHost" & GetStringFromEnum(VerType))
                Dim PathHost As Shapes.Path = FindName("pathVersionHost" & GetStringFromEnum(VerType))
                Dim PanelHost As StackPanel = FindName("panVersion" & GetStringFromEnum(VerType))

                '初始化
                If IsNothing(PanelHost) Then GoTo NextKey

                If VersionsList(VerType).Count > 0 Then

                    GridHost.Height = 27
                    PathHost.Data = (New GeometryConverter).ConvertFromString("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8")
                    Select Case VerType
                        Case VersionSwapState.SWAP
                            LabelHost.Content = "折叠的版本 (" & VersionsList(VerType).Count & ")"
                            PanelHost.Height = 0
                        Case VersionSwapState.OLD
                            LabelHost.Content = "过时的版本 (" & VersionsList(VerType).Count & ")"
                            PanelHost.Height = 0
                        Case VersionSwapState.WRONG
                            LabelHost.Content = "错误的版本 (" & VersionsList(VerType).Count & ")"
                            PanelHost.Height = 0
                    End Select
                    For Each Ver As MCVersion In VersionsList(VerType)
                        '增加控件
                        Dim Con = New ListItem With {.Name = "listitemVersion" & GetUUID(), .SubText = Ver.Description, .MainText = Ver.Name, .ShowButton = False, .UseLayoutRounding = True, .AtLeastCheck = False,
                                                                    .Logo = New BitmapImage(New Uri(Ver.Logo, UriKind.Absolute)), .Version = Ver, .Opacity = 0, .CanCheck = Not Ver.SwapType = VersionSwapState.WRONG}
                        PanelHost.Children.Add(Con)
                        '确认历史记录
                        If Ver.Name = ShouldSelectVersion Then ChangeVersion(Con, Nothing)
                        '增加事件
                        AddHandler Con.MouseLeftButtonUp, AddressOf ChangeVersion
                        AddHandler Con.MouseRightButtonUp, AddressOf DeleteVersion
                        AddHandler Con.MouseEnter, Sub()
                                                       Con.ToolTip = If(Con.labMain.ActualWidth >= Con.ActualWidth - 47, Con.labMain.Content, Nothing)
                                                   End Sub
                    Next

                Else
                    GridHost.Height = 0
                End If
NextKey:
            Next

            If SelectVersion.Name = "" Then
                '选择第一项
                For Each List As UIElementCollection In {panVersionNORMAL.Children, panVersionSWAP.Children, panVersionOLD.Children}
                    For Each Version In List
                        ChangeVersion(Version, Nothing)
                        GoTo EndSelect
                    Next
                Next
            End If
EndSelect:

            log("[HomeRight] UI线程加载版本列表结束")

        Catch ex As Exception

            log("[HomeRight] UI线程加载版本列表失败：" & GetStringFromException(ex, True), True)
            ShowHint(New HintConverter("显示版本列表失败：" & GetStringFromException(ex), HintState.Critical))

        Finally

            StartButtonIsLoading = False
            IsPoolVersionListRunning = False

        End Try
    End Sub

    ''' <summary>
    ''' 改变选择的版本。
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub ChangeVersion(ByVal sender As ListItem, ByVal e As System.Windows.Input.MouseButtonEventArgs)
        If sender.Version.VersionCheckResult = VersionCheckState.NO_PROBLEM Then

            '选择版本
            If Not IsNothing(e) Then HideVersion() '如果不是代码调用就返回
            For Each Item As ListItem In panVersionNORMAL.Children
                Item.Checked = False
            Next
            For Each Item As ListItem In panVersionOLD.Children
                Item.Checked = False
            Next
            For Each Item As ListItem In panVersionSWAP.Children
                Item.Checked = False
            Next
            sender.Checked = True
            SelectVersion = sender.Version
            log("[HomeRight] 当前选择的MC版本：" & sender.MainText)
            WriteIni("setup", "History", sender.MainText)

        Else
            If IsNothing(e) Then Exit Sub '如果不是代码调用就不提示

            '查看报错原因
            Select Case sender.Version.VersionCheckResult
                Case VersionCheckState.EMPTY_FOLDER
                    ShowHint(New HintConverter("不存在任何版本文件，建议删除或重新下载", HintState.Warn))
                Case VersionCheckState.INHERITS_EXCEPTION
                    ShowHint(New HintConverter("依赖版本（" & sender.Version.InheritVersion & "）存在问题", HintState.Warn))
                Case VersionCheckState.INHERITS_NOT_EXIST
                    ShowHint(New HintConverter("该版本需要依赖版本（" & sender.Version.InheritVersion & "）才能运行", HintState.Warn))
                Case VersionCheckState.JAR_NOT_EXIST
                    ShowHint(New HintConverter("未找到版本主文件，建议重新下载安装该版本", HintState.Warn))
                Case VersionCheckState.JSON_CANT_READ
                    ShowHint(New HintConverter("Json文件无法读取，建议删除或重新下载", HintState.Warn))
                Case VersionCheckState.JSON_NOT_EXIST
                    ShowHint(New HintConverter("Json文件不存在，建议重新下载（较老启动器下载的版本均存在此问题）", HintState.Warn))
            End Select

        End If
    End Sub

    ''' <summary>
    ''' 删除选择的版本。
    ''' </summary>
    Private Sub DeleteVersion(ByVal sender As ListItem, ByVal e As System.Windows.Input.MouseButtonEventArgs)
        If MyMsgbox("你确定要删除这个版本吗？该操作不可撤销！", "删除确认", "确认", "取消", , True) = 1 Then
            Try
                My.Computer.FileSystem.DeleteDirectory(PATH_MC & "versions\" & sender.MainText, FileIO.DeleteDirectoryOption.DeleteAllContents)
                '重新载入
                HideVersion() '返回
                Dim th As New Thread(Sub() PoolVersionList())
                th.Start()
                log("[HomeRight] 删除版本：" & sender.MainText)
                ShowHint(New HintConverter("Minecraft " & sender.MainText & " 删除成功", HintState.Finish))
            Catch ex As Exception
                ExShow(ex, "Minecraft " & sender.MainText & " 删除失败", ErrorLevel.AllUsers)
            End Try
        End If
    End Sub

    '切换显示/隐藏
    Private Sub panVersionHost_MouseLeftButtonUp(ByVal sender As Grid, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles panVersionHostOLD.MouseLeftButtonUp, panVersionHostSWAP.MouseLeftButtonUp, panVersionHostWRONG.MouseLeftButtonUp

        '获取对应控件
        Dim PathHost As Shapes.Path = FindName("pathVersionHost" & sender.Tag)
        Dim PanelHost As StackPanel = FindName("panVersion" & sender.Tag)

        If PathHost.Data.ToString.Equals("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8") Then

            '未展开
            PathHost.Data = (New GeometryConverter).ConvertFromString("M10,1A9,9,0,1,0,10.001,1 M6,12L10,8 14,12")
            PanelHost.Opacity = 1
            AniStart({
                     AaStack(PanelHost),
                     AaHeight(PanelHost, PanelHost.Children.Count * 40 - PanelHost.Height, 250, , New AniEaseEnd)
                 }, "Show" & sender.Name)

        Else

            '展开
            PathHost.Data = (New GeometryConverter).ConvertFromString("M10,1A9,9,0,1,0,10.001,1 M6,8L10,12 14,8")
            AniStart({
                     AaHeight(PanelHost, -PanelHost.ActualHeight, PanelHost.Children.Count * 30, , New AniEaseEnd),
                     AaOpacity(PanelHost, 1 - PanelHost.Opacity)
                 }, "Show" & sender.Name)

        End If

    End Sub

    '返回
    Public Sub HideVersion() Handles btnVersionBack.MouseLeftButtonUp

        panVersion.IsHitTestVisible = False
        panLaunch.Visibility = Visibility.Visible
        AniStart({
                AaOpacity(panLaunch, 1 - panLaunch.Opacity, 150, 110),
                AaOpacity(panVersion, -panVersion.Opacity, 150),
                AaValue(scrollVersion, -scrollVersion.Value, 1, , , True)
            }, "HomeRightShowVersionList")
        panBackBounds = panLaunch
        panBackHeightChange(panLaunch, Nothing)

    End Sub

    '绑定滚动条
    Private Sub panVersionList_Loaded(ByVal sender As Object, ByVal e As System.Windows.RoutedEventArgs) Handles panVersionList.Loaded
        If scrollVersion.SetControl(panVersionList, False) Then AddHandler panVersion.MouseWheel, AddressOf scrollVersion.RunMouseWheel
    End Sub

    '背景高度改变
    Private panBackBounds As Grid = panLaunch
    Private Sub panBackHeightChange(ByVal sender As Grid, ByVal e As EventArgs) Handles panVersion.SizeChanged, panLaunch.SizeChanged
        If Not sender.Equals(panBackBounds) Then Exit Sub

        AniStart({
                 AaHeight(panBack, sender.ActualHeight - panBack.ActualHeight, 100, , New AniEaseEnd)
            }, "HomeRightpanBackHeightChange")

    End Sub

#End Region

    '#Region "版本设置 | panSetup"

    '    Private SetupVersion As ListItem  '选择的版本
    '    Private CanWrite As Boolean = True '是否可以写入设置

    '    '版本设置
    '    Private Sub VersionSetup(ByVal sender As ListItem, ByVal e As System.Windows.Input.MouseButtonEventArgs)
    '        CanWrite = False
    '        UseControlAnimation = False
    '        '禁止进入其它版本设置
    '        For Each item In lisVer.Children
    '            If item.GetType.Name = "ListItem" Then item.ShowButton = False
    '        Next
    '        '刷新基础信息
    '        SetupVersion = sender
    '        imgSetupLogo.Source = sender.imgLeft.Source
    '        textSetupFolder.Text = sender.MainText
    '        '初始化设置信息
    '        textSetupServer.Text = "无"
    '        textSetupDes.Text = If(SetupVersion.SubText = "", SetupVersion.MainText, SetupVersion.SubText)
    '        checkSetupIcon.Checked = File.Exists(PATH_MC & "\versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '        '刷新设置信息
    '        If File.Exists(PATH_MC & "versions\" & sender.MainText & "\PCL\Setup.ini") Then
    '            Dim cfg As Array = ReadFileToEnd(PATH_MC & "versions\" & sender.MainText & "\PCL\Setup.ini", Encoding.Default).Split(vbCrLf)
    '            '循环每行读取
    '            checkSetupOffline.Checked = ReadIni(PATH_MC & "versions\" & sender.MainText & "\PCL\Setup.ini", "Offline", "False")
    '            textSetupServer.Text = ReadIni(PATH_MC & "versions\" & sender.MainText & "\PCL\Setup.ini", "Server", "")
    '            textSetupDes.Text = ReadIni(PATH_MC & "versions\" & sender.MainText & "\PCL\Setup.ini", "Des", "")
    '        End If
    '        '显示动画
    '        AniStop("VersionShow")
    '        AniStart({
    '                 AaCode({"Visible", panSetup, True}),
    '                 AaOpacity(panVersion, -1, 200),
    '                 AaOpacity(panSetup, 1, 200, 200),
    '                 AaCode({"Visible", panVersion, False}, 400),
    '                 AaWidth(lineSetup1, 246 - lineSetup1.Width, 400, 300, AniAdditionType.FadeOut),
    '                 AaWidth(lineSetup2, 246 - lineSetup2.Width, 400, 300, AniAdditionType.FadeOut)
    '             }, "VersionShow")
    '        CanWrite = True
    '        UseControlAnimation = True
    '        log("[HomeRight] 加载版本设置页面：" & sender.MainText)
    '    End Sub

    '    '重命名
    '    Private Sub textSetupFolder_TextChanged(ByVal sender As Object, ByVal e As System.Windows.Controls.TextChangedEventArgs) Handles textSetupFolder.TextChanged
    '        '如果变更了名字就显示确认按钮
    '        If Not IsNothing(SetupVersion) Then
    '            AniStop("SetupRename")
    '            AniStart({
    '                     AaCode({"Visible", btnSetupDelete, True}),
    '                     AaOpacity(btnSetupFolderEnter, If(textSetupFolder.Text = SetupVersion.MainText, 0, 0.7) - btnSetupFolderEnter.Opacity, 200)
    '                 }, "SetupRename")
    '        End If
    '    End Sub
    '    Private Sub textSetupFolder_KeyUp(ByVal sender As Object, ByVal e As System.Windows.Input.KeyEventArgs) Handles textSetupFolder.KeyUp
    '        '回车处理
    '        If e.Key = Key.Enter Then btnSetupFolderEnter_MouseUp(sender, Nothing)
    '    End Sub
    '    Private Sub btnSetupFolderEnter_MouseUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles btnSetupFolderEnter.MouseUp
    '        If btnSetupFolderEnter.Opacity > 0.1 Then '已经显示这个按钮（0.1是因为浮点运算bug）
    '            If MyMsgbox("重命名", "你确定要重命名这个版本吗？重命名后可能会导致游戏无法启动！", , "取消") = 1 Then
    '                Try
    '                    log("[HomeRight] 重命名版本：" & textSetupFolder.Text)
    '                    '重命名主文件夹
    '                    Dim BaseDir As String = PATH_MC & "versions\" & textSetupFolder.Text
    '                    Microsoft.VisualBasic.FileSystem.Rename(SetupVersion.Version.Folder, BaseDir)
    '                    '重命名Json
    '                    Using Writter As New StreamWriter(BaseDir & "\" & textSetupFolder.Text & ".json", False)
    '                        Writter.Write(ReadFileToEnd(BaseDir & "\" & SetupVersion.MainText & ".json").Replace("""id"": """ & SetupVersion.MainText, """id"": """ & textSetupFolder.Text))
    '                        Writter.Flush()
    '                    End Using
    '                    File.Delete(BaseDir & "\" & SetupVersion.MainText & ".json")
    '                    '重命名Jar
    '                    If File.Exists(BaseDir & "\" & textSetupFolder.Text & ".jar") Then
    '                        File.Delete(BaseDir & "\" & textSetupFolder.Text & ".jar")
    '                        Microsoft.VisualBasic.FileSystem.Rename(BaseDir & "\" & SetupVersion.MainText & ".jar", BaseDir & "\" & textSetupFolder.Text & ".jar")
    '                    End If
    '                    '重命名natives
    '                    If Directory.Exists(BaseDir & "\" & SetupVersion.MainText & "-natives") Then Microsoft.VisualBasic.FileSystem.Rename(BaseDir & "\" & SetupVersion.MainText & "-natives", BaseDir & "\" & textSetupFolder.Text & "-natives")
    '                Catch ex As Exception
    '                    log("[HomeRight] 重命名版本失败：" & GetStringFromException(ex, True), True)
    '                    MyMsgbox("重命名失败", "详细的错误信息：" & GetStringFromException(ex) & vbCrLf & "该版本可能已经损坏。")
    '                End Try
    '                '重命名结束处理
    '                btnSetupBack_MouseUp() '返回版本列表
    '                LoadVersionList() '重新加载版本列表
    '            End If
    '        End If
    '    End Sub

    '    '自定义图标
    '    Private Sub checkSetupIcon_MouseUp() Handles checkSetupIcon.MouseUp
    '        If checkSetupIcon.Checked Then
    '            '试图启用自定义图标
    '            Dim fileName As String = SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.jpeg)|*.png;*.jpg;*.gif;*.jpeg", "选择作为图标的文件")
    '            '选择文件结束
    '            If fileName = "" Then checkSetupIcon.Checked = False : Exit Sub
    '            Try
    '                '设置当前显示
    '                imgSetupLogo.Source = (New ImageSourceConverter).ConvertFromString(fileName)
    '                SetupVersion.Logo = (New ImageSourceConverter).ConvertFromString(fileName)
    '                '拷贝文件
    '                File.Delete(PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '                File.Copy(fileName, PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '            Catch ex As Exception
    '                '清除只需要改变Checked，会自动调用禁用代码
    '                checkSetupIcon.Checked = False
    '            End Try
    '        Else
    '            '还原当前显示
    '            Select Case SetupVersion.Version.Type
    '                Case MCVersionType.FORGE
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-Anvil.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-Anvil.png", UriKind.Relative))
    '                Case MCVersionType.OLD
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-CobbleStone.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-CobbleStone.png", UriKind.Relative))
    '                Case MCVersionType.SNAPSHOT
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-CommandBlock.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-CommandBlock.png", UriKind.Relative))
    '                Case Else 'OptiFine、原版、未知
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-Grass.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-Grass.png", UriKind.Relative))
    '            End Select
    '            '删除
    '            File.Delete(PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '        End If
    '    End Sub
    '    Private Sub imgSetupLogo_MouseUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles imgSetupLogo.MouseUp
    '        '试图启用自定义图标
    '        Dim fileName As String = SelectFile("常用图片文件(*.png;*.jpg;*.gif;*.jpeg)|*.png;*.jpg;*.gif;*.jpeg", "选择作为图标的文件")
    '        '选择文件结束
    '        If fileName = "" Then Exit Sub
    '        '选择了一个文件
    '        Try
    '            '设置当前显示
    '            imgSetupLogo.Source = (New ImageSourceConverter).ConvertFromString(fileName)
    '            SetupVersion.Logo = (New ImageSourceConverter).ConvertFromString(fileName)
    '            '拷贝文件
    '            File.Delete(PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '            File.Copy(fileName, PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '            checkSetupIcon.Checked = True
    '        Catch ex As Exception
    '            checkSetupIcon.Checked = False
    '            '还原当前显示
    '            Select Case SetupVersion.Version.Type
    '                Case MCVersionType.FORGE
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-Anvil.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-Anvil.png", UriKind.Relative))
    '                Case MCVersionType.OLD
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-CobbleStone.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-CobbleStone.png", UriKind.Relative))
    '                Case MCVersionType.SNAPSHOT
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-CommandBlock.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-CommandBlock.png", UriKind.Relative))
    '                Case Else 'OptiFine、原版、未知
    '                    imgSetupLogo.Source = New BitmapImage(New Uri("/Images/Block-Grass.png", UriKind.Relative))
    '                    SetupVersion.Logo = New BitmapImage(New Uri("/Images/Block-Grass.png", UriKind.Relative))
    '            End Select
    '            '删除
    '            File.Delete(PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\icon.png")
    '        End Try
    '    End Sub

    '    '自定义描述
    '    Private Sub textSetupDes_TextChanged(ByVal sender As Object, ByVal e As System.Windows.Controls.TextChangedEventArgs) Handles textSetupDes.TextChanged
    '        If CanWrite Then
    '            '改变当前显示
    '            SetupVersion.SubText = If(textSetupDes.Text = SetupVersion.MainText, "", textSetupDes.Text)
    '        End If
    '    End Sub

    '    '写入设置
    '    Public Sub SetupWrite() Handles checkSetupOffline.MouseUp, textSetupServer.TextChanged, textSetupDes.TextChanged
    '        If CanWrite Then
    '            Using iniWriter As New StreamWriter(PATH_MC & "versions\" & SetupVersion.MainText & "\PCL\Setup.ini", False, Encoding.Unicode)
    '                iniWriter.WriteLine()
    '                iniWriter.WriteLine("Offline:" & checkSetupOffline.Checked)
    '                iniWriter.WriteLine("Des:" & textSetupDes.Text)
    '                iniWriter.WriteLine("Server:" & If(Len(textSetupServer.Text) < 5, "无", textSetupServer.Text))
    '                iniWriter.Flush()
    '            End Using
    '        End If
    '    End Sub

    '    '返回
    '    Private Sub btnSetupBack_MouseUp() Handles btnSetupBack.MouseUp
    '        '允许进入其它版本设置
    '        For Each item In lisVer.Children
    '            If item.GetType.Name = "ListItem" Then item.ShowButton = True
    '        Next
    '        '显示动画
    '        AniStop("VersionShow")
    '        AniStart({
    '                 AaCode({"Visible", panVersion, True}),
    '                 AaOpacity(panSetup, -1, 200),
    '                 AaOpacity(panVersion, 1, 200, 200),
    '                 AaCode({"Visible", panSetup, False}, 400),
    '                 AaWidth(lineSetup1, -lineSetup1.Width, 0, 400),
    '                 AaWidth(lineSetup2, -lineSetup2.Width, 0, 400)
    '             }, "VersionShow")
    '    End Sub

    '    '高级版本设置
    '    Private Sub btnSetupAdvance_Click(ByVal sender As Object, ByVal e As System.EventArgs) Handles btnSetupAdvance.Click
    '        Dim setupPage As New msgVersion(SetupVersion.Version)
    '        If Not IsNothing(setupPage.panMain.Parent) Then setupPage.panMain.Parent.SetValue(ContentPresenter.ContentProperty, Nothing)
    '        MyMsgbox("高级版本设置", setupPage.panMain, "完成")
    '        LoadVersionList()
    '    End Sub

    '#End Region

End Class

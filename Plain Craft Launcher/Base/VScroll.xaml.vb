Public Class VScroll

    Public Event Change(ByVal sender As Object)

#Region "属性"

    Private _ParentControl As Grid = Nothing
    Private _ParentSubControl As Grid = Nothing
    ''' <summary>
    ''' 对应的控件的容器。
    ''' </summary>
    ''' <remarks></remarks>
    Public ReadOnly Property ParentControl As Grid
        Get
            Return _ParentControl
        End Get
    End Property

    Private _Control As Object = Nothing
    ''' <summary>
    ''' 对应的控件。它是需要被滚动的容器，并且被一个Grid包裹。
    ''' </summary>
    ''' <remarks></remarks>
    Public ReadOnly Property Control As Object
        Get
            Return _Control
        End Get
    End Property

    ''' <summary>
    ''' 按钮离滚动条顶部的距离。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    ''' <summary>
    ''' 【修复】滑块可移动的距离，即滑块能到达的最高位置。
    ''' </summary>
    Public ReadOnly Property DragRange As Double
        Get
            Return Math.Max(0, MaxValueMe - QuoteValueMe)
        End Get
    End Property

    ''' <summary>
    ''' 【修复】内容可滚动的距离。
    ''' </summary>
    Public ReadOnly Property ScrollRange As Double
        Get
            Return Math.Max(0, MaxValue - QuoteValue)
        End Get
    End Property

    Public Property ValueMe As Double
        Get
            Return btnDrag.Margin.Top
        End Get
        Set(ByVal ValueMe As Double)
            If QuoteValue = 0 Then Exit Property
            ' 【修复】原先滑块位置与内容偏移之间存在两套不同的换算公式：拖动滑块时使用
            ' MaxValue / QuoteValue，而滚轮经由 Value 属性又使用 MaxValueMe / MaxValue。
            ' 两者只有在滚动条高度恰好等于列表可视高度时才互为逆运算，而滚动条带有 Margin，
            ' 通常并不相等。于是「把滑块拖到底」与「滚轮滚到底」算出的位置并不一致，
            ' 表现为拖到底之后滚轮仍能继续往下翻。这里统一按比例映射，两个方向严格互逆。
            Dim NewTop As Double = MathRange(ValueMe, 0, DragRange)
            SetTop(btnDrag, NewTop)
            SetTop(Control, -If(DragRange = 0, 0, NewTop / DragRange * ScrollRange))
        End Set
    End Property


    ''' <summary>
    ''' 按钮高度。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property QuoteValueMe As Double
        Get
            Return btnDrag.ActualHeight
        End Get
        Set(ByVal ValueMe As Double)
            btnDrag.Height = ValueMe
        End Set
    End Property

    ''' <summary>
    ''' 滚动条高度。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property MaxValueMe As Double
        Get
            Return Me.ActualHeight
        End Get
    End Property

    ''' <summary>
    ''' 控件滚动的距离。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property Value As Double
        Get
            Return If(IsNothing(Control), 0, -Control.Margin.Top)
        End Get
        Set(ByVal NewValue As Double)
            ' 【修复】与 ValueMe 使用同一套映射的逆运算，并在此完成范围限制，
            ' 保证 Value 与滑块位置始终指向同一个位置。
            Dim Clamped As Double = MathRange(NewValue, 0, ScrollRange)
            ValueMe = If(ScrollRange = 0, 0, Clamped / ScrollRange * DragRange)
        End Set
    End Property

    ''' <summary>
    ''' 控件可以显示的最大高度。等同于容器的高度。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property QuoteValue As Double
        Get
            Return If(IsNothing(ParentControl), 0, ParentControl.ActualHeight)
        End Get
    End Property

    ''' <summary>
    ''' 最大值。等同于控件的高度。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property MaxValue As Double
        Get
            Return If(IsNothing(Control), 0, Control.ActualHeight)
        End Get
    End Property

    Private _InCenter As Boolean
    ''' <summary>
    ''' 是否在控件高度不足最高高度时把它居中。
    ''' </summary>
    ''' <ValueMe></ValueMe>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public ReadOnly Property InCenter As Boolean
        Get
            Return _InCenter
        End Get
    End Property

    ''' <summary>
    ''' 在判断滚动条是否显示时的容错范围。
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property WrongRange As Double = 2

#End Region

#Region "鼠标动画"

    Private Sub VScroll_MouseEnter(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles Me.MouseEnter
        If Len(Me.Name) < 1 Then Me.Name = "Aniamtioner" & GetUUID() '确保这个控件有名称
        AniStart({
                 AaOpacity(btnDrag, 1 - btnDrag.Opacity, 200),
                 AaOpacity(btnBack, 0.3 - btnBack.Opacity, 200)
             }, Me.Name & "Mouse")
    End Sub

    Private Sub VScroll_MouseLeave(ByVal sender As Object, ByVal e As System.Windows.Input.MouseEventArgs) Handles Me.MouseLeave
        If IsNothing(DragingScroll) Then DragMouseLeave()
    End Sub
    ''' <summary>
    ''' 触发离开动画。
    ''' </summary>
    ''' <remarks></remarks>
    Public Sub DragMouseLeave()
        AniStart({
                 AaOpacity(btnDrag, 0.4 - btnDrag.Opacity, 200),
                 AaOpacity(btnBack, 0.01 - btnBack.Opacity, 200)
             }, Me.Name & "Mouse")
    End Sub

#End Region

#Region "鼠标拖动"

    Private ClickedY As Integer '鼠标指针在按钮上的相对Y坐标
    Private ShouldTop As Integer '按钮到顶部的距离
    Private Sub btnDrag_MouseDown(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles btnDrag.MouseDown
        ShouldTop = btnDrag.Margin.Top
        ClickedY = e.GetPosition(btnDrag).Y
        DragingScroll = Me
    End Sub
    Public Sub Draging(ByVal Y As Integer)
        ShouldTop = MathRange(Y - ClickedY + ShouldTop, 0, MaxValueMe - QuoteValueMe) '实际应该在的位置
        If Not ValueMe = ShouldTop Then ValueMe = ShouldTop
    End Sub

#End Region

    ''' <summary>
    ''' 设置绑定的控件。这个方法务必只能执行一次。
    ''' </summary>
    ''' <param name="Control">需要被拖动的控件。它需要被一个Grid覆盖。</param>
    ''' <param name="InCenter">是否在控件高度不足最高高度时把它居中。</param>
    ''' <remarks></remarks>
    Public Function SetControl(ByVal Control As StackPanel, ByVal InCenter As Boolean) As Boolean
        '可靠性检查
        If IsNothing(Control) Then Return False
        If IsNothing(Control.Parent) Then Return False
        If Not Control.Parent.GetType.Name = "Grid" Then Return False
        If Not IsNothing(ParentControl) Then Return False

        Try
            '初始化
            _ParentControl = Control.Parent
            _ParentControl.UpdateLayout()
            _ParentSubControl = New Grid With {.Width = _ParentControl.ActualWidth + 0.51, .VerticalAlignment = VerticalAlignment.Top}
            _Control = Control
            _InCenter = InCenter
            SetTop(btnDrag, 0)
            '初始化控件
            _ParentControl.SetValue(ContentPresenter.ContentProperty, Nothing)
            _ParentControl.Children.Clear()
            _ParentControl.Children.Add(_ParentSubControl)
            _ParentSubControl.Children.Add(_Control)
            _Control.VerticalAlignment = VerticalAlignment.Top
            '添加委托
            AddHandler Me.SizeChanged, AddressOf Refresh
            AddHandler Control.SizeChanged, AddressOf Refresh
            AddHandler ParentControl.SizeChanged, AddressOf Refresh
            AddHandler btnDrag.SizeChanged, AddressOf Refresh
        Catch ex As Exception
            ExShow(ex, "滚动条绑定失败", ErrorLevel.MsgboxAndFeedback)
            Return False
        End Try

        '刷新滚动条
        Refresh(Nothing, Nothing)
        Return True
    End Function

    ''' <summary>
    ''' 刷新QuoteValue与高度等。
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function Refresh(ByVal sender, ByVal e) As Boolean
        Try

            '可靠性检查
            If IsNothing(_Control) Or IsNothing(ParentControl) Then Return False
            If QuoteValue = 0 Or MaxValue = 0 Or Not MathCheck(QuoteValueMe) Then Return False

            _ParentSubControl.Width = _ParentControl.ActualWidth + 0.51

            Dim OldValueMe = ValueMe
            Dim OldValue = Value

            QuoteValueMe = MathRange(MaxValueMe / MaxValue * QuoteValue, 15, MaxValueMe)
            If MaxValueMe - QuoteValueMe < WrongRange Then
                '过短
                AniStart({AaOpacity(Me, -Me.Opacity, 100)}, Me.Name & "Opacity", False)
                _Control.Margin = New Thickness(0)
                If InCenter Then _ParentSubControl.VerticalAlignment = VerticalAlignment.Center
                Value = 0
            Else
                '正常
                AniStart({AaOpacity(Me, 1 - Me.Opacity, 100)}, Me.Name & "Opacity", False)
                _ParentSubControl.VerticalAlignment = VerticalAlignment.Top
                Value = OldValue
            End If

            If Not ValueMe = OldValueMe Then RaiseEvent Change(Me)
            Return True
        Catch ex As Exception
            ExShow(ex, "刷新滚动条失败", ErrorLevel.MsgboxAndFeedback)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 委托处理鼠标滚轮事件。将试图接管滚轮的控件添加一个Handler到这个事件。
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Public Sub RunMouseWheel(ByVal sender As Object, ByVal e As System.Windows.Input.MouseWheelEventArgs)
        If Me.Visibility = Visibility.Visible Then
            ' 【修复】这里原先给 Value 属性挂了一个滚动动画。该动画是逐帧累加增量的，
            ' 当动画被新的滚动替换、再叠加缓动收尾与帧计时时，增量会失真，个别帧甚至算出负值，
            ' 于是出现「滚动结束时轻微上下抖动」，乃至「往下滚却向上跑」。
            ' 滚轮本身是离散输入，这里直接设置目标位置并完成范围限制，行为与系统原生滚动一致，
            ' 也就不再需要靠动画组名去停止上一次滚动。
            Value = MathRange(Value - e.Delta, 0, ScrollRange)
        End If
    End Sub

    ''' <summary>
    ''' 点击滚动条空白部分改变值。
    ''' </summary>
    ''' <param name="sender"></param>
    ''' <param name="e"></param>
    ''' <remarks></remarks>
    Private Sub btnBack_MouseUp(ByVal sender As Object, ByVal e As System.Windows.Input.MouseButtonEventArgs) Handles btnBack.MouseUp
        If btnDrag.IsMouseOver Then Exit Sub '点在空白部分
        ' 【修复】与滚轮同样的问题：动画组名每次用 GetUUID() 都会新建一个动画组，
        ' 而 AniStart 只在名称重复时才会停止上一个动画组，导致翻页动画互相叠加。改为固定名称。
        If Len(Me.Name) < 1 Then Me.Name = "Aniamtioner" & GetUUID() '确保这个控件有名称
        If e.GetPosition(btnDrag).Y < 0 Then
            AniStart({
                     AaValue(Me, -QuoteValue, 200, , New AniEaseEnd)
                 }, "ScrollClick" & Me.Name, False)
        Else
            AniStart({
                     AaValue(Me, QuoteValue, 200, , New AniEaseEnd)
                 }, "ScrollClick" & Me.Name, False)
        End If
    End Sub

End Class

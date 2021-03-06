Imports StaxRip.UI

Class SourceFilesForm
    Inherits DialogBase

#Region " Designer "
    Protected Overloads Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private components As System.ComponentModel.IContainer

    Friend WithEvents lb As ListBoxEx
    Friend WithEvents bnDown As ButtonEx
    Friend WithEvents bnUp As ButtonEx
    Friend WithEvents bnRemove As ButtonEx
    Friend WithEvents bnAdd As ButtonEx
    Friend WithEvents cbDemuxAndIndex As System.Windows.Forms.CheckBox
    Friend WithEvents bnCancel As StaxRip.UI.ButtonEx
    Friend WithEvents tlpMain As TableLayoutPanel
    Friend WithEvents pnLB As Panel
    Friend WithEvents bnOK As StaxRip.UI.ButtonEx

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lb = New StaxRip.UI.ListBoxEx()
        Me.bnDown = New StaxRip.UI.ButtonEx()
        Me.bnRemove = New StaxRip.UI.ButtonEx()
        Me.bnUp = New StaxRip.UI.ButtonEx()
        Me.bnAdd = New StaxRip.UI.ButtonEx()
        Me.cbDemuxAndIndex = New System.Windows.Forms.CheckBox()
        Me.bnCancel = New StaxRip.UI.ButtonEx()
        Me.bnOK = New StaxRip.UI.ButtonEx()
        Me.tlpMain = New System.Windows.Forms.TableLayoutPanel()
        Me.pnLB = New System.Windows.Forms.Panel()
        Me.tlpMain.SuspendLayout()
        Me.pnLB.SuspendLayout()
        Me.SuspendLayout()
        '
        'lb
        '
        Me.lb.AllowDrop = True
        Me.lb.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lb.DownButton = Me.bnDown
        Me.lb.FormattingEnabled = True
        Me.lb.IntegralHeight = False
        Me.lb.ItemHeight = 48
        Me.lb.Location = New System.Drawing.Point(0, 0)
        Me.lb.Name = "lb"
        Me.lb.RemoveButton = Me.bnRemove
        Me.lb.Size = New System.Drawing.Size(1166, 668)
        Me.lb.TabIndex = 2
        Me.lb.UpButton = Me.bnUp
        '
        'bnDown
        '
        Me.bnDown.Anchor = System.Windows.Forms.AnchorStyles.Top
        Me.bnDown.Location = New System.Drawing.Point(1198, 404)
        Me.bnDown.Margin = New System.Windows.Forms.Padding(8)
        Me.bnDown.Size = New System.Drawing.Size(250, 80)
        Me.bnDown.Text = "    &Down"
        '
        'bnRemove
        '
        Me.bnRemove.Location = New System.Drawing.Point(1198, 112)
        Me.bnRemove.Margin = New System.Windows.Forms.Padding(8)
        Me.bnRemove.Size = New System.Drawing.Size(250, 80)
        Me.bnRemove.Text = "   &Remove"
        '
        'bnUp
        '
        Me.bnUp.Anchor = System.Windows.Forms.AnchorStyles.Bottom
        Me.bnUp.Location = New System.Drawing.Point(1198, 308)
        Me.bnUp.Margin = New System.Windows.Forms.Padding(8)
        Me.bnUp.Size = New System.Drawing.Size(250, 80)
        Me.bnUp.Text = "&Up"
        '
        'bnAdd
        '
        Me.bnAdd.Location = New System.Drawing.Point(1198, 16)
        Me.bnAdd.Margin = New System.Windows.Forms.Padding(8)
        Me.bnAdd.Size = New System.Drawing.Size(250, 80)
        Me.bnAdd.Text = "&Add..."
        '
        'cbDemuxAndIndex
        '
        Me.cbDemuxAndIndex.Anchor = System.Windows.Forms.AnchorStyles.Left
        Me.cbDemuxAndIndex.AutoSize = True
        Me.cbDemuxAndIndex.Location = New System.Drawing.Point(16, 714)
        Me.cbDemuxAndIndex.Margin = New System.Windows.Forms.Padding(8)
        Me.cbDemuxAndIndex.Name = "cbDemuxAndIndex"
        Me.cbDemuxAndIndex.Size = New System.Drawing.Size(672, 52)
        Me.cbDemuxAndIndex.TabIndex = 3
        Me.cbDemuxAndIndex.Text = "Demux and index before creating jobs"
        '
        'bnCancel
        '
        Me.bnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel
        Me.bnCancel.Location = New System.Drawing.Point(1198, 700)
        Me.bnCancel.Margin = New System.Windows.Forms.Padding(8)
        Me.bnCancel.Size = New System.Drawing.Size(250, 80)
        Me.bnCancel.Text = "Cancel"
        '
        'bnOK
        '
        Me.bnOK.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.bnOK.Location = New System.Drawing.Point(932, 700)
        Me.bnOK.Margin = New System.Windows.Forms.Padding(8)
        Me.bnOK.Size = New System.Drawing.Size(250, 80)
        Me.bnOK.Text = "OK"
        '
        'tlpMain
        '
        Me.tlpMain.ColumnCount = 3
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle())
        Me.tlpMain.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle())
        Me.tlpMain.Controls.Add(Me.bnRemove, 2, 1)
        Me.tlpMain.Controls.Add(Me.bnAdd, 2, 0)
        Me.tlpMain.Controls.Add(Me.bnCancel, 2, 7)
        Me.tlpMain.Controls.Add(Me.bnOK, 1, 7)
        Me.tlpMain.Controls.Add(Me.cbDemuxAndIndex, 0, 7)
        Me.tlpMain.Controls.Add(Me.bnUp, 2, 3)
        Me.tlpMain.Controls.Add(Me.bnDown, 2, 4)
        Me.tlpMain.Controls.Add(Me.pnLB, 0, 0)
        Me.tlpMain.Dock = System.Windows.Forms.DockStyle.Fill
        Me.tlpMain.Location = New System.Drawing.Point(0, 0)
        Me.tlpMain.Name = "tlpMain"
        Me.tlpMain.Padding = New System.Windows.Forms.Padding(8)
        Me.tlpMain.RowCount = 8
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50.0!))
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100.0!))
        Me.tlpMain.RowStyles.Add(New System.Windows.Forms.RowStyle())
        Me.tlpMain.Size = New System.Drawing.Size(1464, 797)
        Me.tlpMain.TabIndex = 7
        '
        'pnLB
        '
        Me.pnLB.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.tlpMain.SetColumnSpan(Me.pnLB, 2)
        Me.pnLB.Controls.Add(Me.lb)
        Me.pnLB.Location = New System.Drawing.Point(16, 16)
        Me.pnLB.Margin = New System.Windows.Forms.Padding(8)
        Me.pnLB.Name = "pnLB"
        Me.tlpMain.SetRowSpan(Me.pnLB, 7)
        Me.pnLB.Size = New System.Drawing.Size(1166, 668)
        Me.pnLB.TabIndex = 9
        '
        'SourceFilesForm
        '
        Me.AcceptButton = Me.bnOK
        Me.AutoScaleDimensions = New System.Drawing.SizeF(288.0!, 288.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi
        Me.CancelButton = Me.bnCancel
        Me.ClientSize = New System.Drawing.Size(1464, 797)
        Me.Controls.Add(Me.tlpMain)
        Me.HelpButton = False
        Me.KeyPreview = True
        Me.Margin = New System.Windows.Forms.Padding(13, 14, 13, 14)
        Me.Name = "SourceFilesForm"
        Me.Text = "Source Files"
        Me.tlpMain.ResumeLayout(False)
        Me.tlpMain.PerformLayout()
        Me.pnLB.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

#End Region

    Property IsMerge As Boolean

    Sub New()
        MyBase.New()
        InitializeComponent()

        bnUp.Image = ImageHelp.GetSymbolImage(Symbol.Up)
        bnDown.Image = ImageHelp.GetSymbolImage(Symbol.Down)
        bnAdd.Image = ImageHelp.GetSymbolImage(Symbol.Add)
        bnRemove.Image = ImageHelp.GetSymbolImage(Symbol.Remove)

        For Each bn In {bnAdd, bnRemove, bnUp, bnDown}
            bn.TextImageRelation = TextImageRelation.Overlay
            bn.ImageAlign = ContentAlignment.MiddleLeft
            Dim pad = bn.Padding
            pad.Left = Control.DefaultFont.Height \ 10
            pad.Right = pad.Left
            bn.Padding = pad
        Next

        ActiveControl = bnOK
    End Sub

    Sub ShowOpenFileDialog()
        Using d As New OpenFileDialog
            d.Multiselect = True
            d.SetFilter(FileTypes.Video)
            d.SetInitDir(s.LastSourceDir)

            If d.ShowDialog() = DialogResult.OK Then
                s.LastSourceDir = d.FileName.Dir
                lb.Items.AddRange(d.FileNames.Sort.ToArray)
                lb.SelectedIndex = lb.Items.Count - 1
            End If
        End Using
    End Sub

    Private Sub bnAdd_Click() Handles bnAdd.Click
        If IsMerge Then
            ShowOpenFileDialog()
            Exit Sub
        End If

        Using td As New TaskDialog(Of String)
            td.AddCommandLink("Add files", "files")
            td.AddCommandLink("Add folder", "folder")
            td.AddCommandLink("Add folder including sub-folders", "sub-folders")

            Select Case td.Show
                Case "files"
                    ShowOpenFileDialog()
                Case "folder", "sub-folders"
                    Using d As New FolderBrowserDialog
                        If d.ShowDialog = DialogResult.OK Then
                            Dim opt = If(td.SelectedValue = "sub-folders", SearchOption.AllDirectories, SearchOption.TopDirectoryOnly)
                            lb.Items.AddRange(Directory.GetFiles(d.SelectedPath, "*.*", opt).Where(Function(val) FileTypes.Video.Contains(val.Ext)).ToArray)
                            lb.SelectedIndex = lb.Items.Count - 1
                        End If
                    End Using
            End Select
        End Using
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If DialogResult = DialogResult.OK Then
            Dim files = GetFiles()
            If g.ShowVideoSourceWarnings(GetFiles) Then e.Cancel = True

            If Not IsMerge Then
                For Each i In files
                    For Each i2 In files
                        Dim a = Filepath.GetDirAndBase(i).ToUpper
                        Dim b = Filepath.GetDirAndBase(i2).ToUpper

                        If a <> b Then
                            If a.StartsWith(b) Then
                                MsgWarn("Files starting with the names of other files can't be used.", b + BR2 + a)
                                e.Cancel = True
                                Exit For
                            End If
                        End If
                    Next
                Next
            End If
        End If

        MyBase.OnFormClosing(e)
    End Sub

    Function GetFiles() As IEnumerable(Of String)
        Return lb.Items.OfType(Of String)
    End Function

    Private Sub lb_DragDrop(sender As Object, e As DragEventArgs) Handles lb.DragDrop
        Dim a = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

        If Not a.NothingOrEmpty Then
            Array.Sort(a)
            lb.Items.AddRange(a.Where(Function(val) File.Exists(val)).ToArray)
        End If
    End Sub

    Private Sub lb_DragEnter(sender As Object, e As DragEventArgs) Handles lb.DragEnter
        e.Effect = DragDropEffects.Copy
    End Sub
End Class
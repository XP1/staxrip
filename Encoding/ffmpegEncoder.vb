﻿Imports System.Globalization
Imports System.Text
Imports StaxRip.UI
Imports StaxRip.CommandLine

<Serializable()>
Class ffmpegEncoder
    Inherits BasicVideoEncoder

    Property ParamsStore As New PrimitiveStore

    Public Overrides ReadOnly Property DefaultName As String
        Get
            Return "ffmpeg | " + Params.Codec.OptionText
        End Get
    End Property

    Sub New()
        Muxer = New ffmpegMuxer("AVI")
    End Sub

    <NonSerialized>
    Private ParamsValue As EncoderParams

    Property Params As EncoderParams
        Get
            If ParamsValue Is Nothing Then
                ParamsValue = New EncoderParams

                ParamsValue.Init(ParamsStore)
            End If

            Return ParamsValue
        End Get
        Set(value As EncoderParams)
            ParamsValue = value
        End Set
    End Property

    Overrides Sub ShowConfigDialog()
        Dim newParams As New EncoderParams
        Dim store = DirectCast(ObjectHelp.GetCopy(ParamsStore), PrimitiveStore)
        newParams.Init(store)

        Using f As New CommandLineForm(newParams)
            Dim saveProfileAction = Sub()
                                        Dim enc = ObjectHelp.GetCopy(Of ffmpegEncoder)(Me)
                                        Dim params2 As New EncoderParams
                                        Dim store2 = DirectCast(ObjectHelp.GetCopy(store), PrimitiveStore)
                                        params2.Init(store2)
                                        enc.Params = params2
                                        enc.ParamsStore = store2
                                        SaveProfile(enc)
                                    End Sub

            f.cms.Items.Add(New ActionMenuItem("Save Profile...", saveProfileAction))
            f.cms.Items.Add(New ActionMenuItem("Codec Help", Sub() g.ShowCode(newParams.Codec.OptionText + " Help", ProcessHelp.GetStdOut(Package.ffmpeg.Path, "-hide_banner -h encoder=" + newParams.Codec.ValueText))))

            If f.ShowDialog() = DialogResult.OK Then
                Params = newParams
                ParamsStore = store
                OnStateChange()
            End If
        End Using
    End Sub

    Overrides ReadOnly Property OutputExt() As String
        Get
            Select Case Params.Codec.OptionText
                Case "Xvid", "ASP"
                    Return "avi"
                Case "ProRes"
                    Return "mov"
                Case Else
                    Return "mkv"
            End Select
        End Get
    End Property

    Overrides ReadOnly Property IsCompCheckEnabled() As Boolean
        Get
            Return False
        End Get
    End Property

    Overrides Sub Encode()
        p.Script.Synchronize()
        Params.RaiseValueChanged(Nothing)

        If Params.Mode.OptionText = "Two Pass" Then
            Encode(Params.GetCommandLine(True, False, 1))
            Encode(Params.GetCommandLine(True, False, 2))
        Else
            Encode(Params.GetCommandLine(True, False))
        End If

        AfterEncoding()
    End Sub

    Overloads Sub Encode(args As String)
        Using proc As New Proc
            proc.Init("Encoding " + Params.Codec.OptionText + " using ffmpeg " + Package.ffmpeg.Version, "frame=")
            proc.Encoding = Encoding.UTF8
            proc.WorkingDirectory = p.TempDir
            proc.File = Package.ffmpeg.Path
            proc.Arguments = args
            proc.Start()
        End Using
    End Sub

    Overrides Function GetMenu() As MenuList
        Dim ret As New MenuList
        ret.Add("Encoder Options", AddressOf ShowConfigDialog)
        ret.Add("Container Configuration", AddressOf OpenMuxerConfigDialog)
        Return ret
    End Function

    Overrides Property QualityMode() As Boolean
        Get
            Return Params.Mode.Value = EncodingMode.Quality
        End Get
        Set(Value As Boolean)
        End Set
    End Property

    Public Overrides ReadOnly Property CommandLineParams As CommandLineParams
        Get
            Return Params
        End Get
    End Property

    Class EncoderParams
        Inherits CommandLineParams

        Sub New()
            Title = "ffmpeg Encoding Options"
        End Sub

        Property Codec As New OptionParam With {
            .Switch = "-c:v",
            .Text = "Codec:",
            .AlwaysOn = True,
            .Options = {"x264", "x265", "VP9", "Xvid", "ASP", "Theora", "ProRes", "H.264 Intel", "H.265 Intel", "H.264 NVIDIA", "H.265 NVIDIA"},
            .Values = {"libx264", "libx265", "libvpx-vp9", "libxvid", "mpeg4", "libtheora", "prores", "h264_qsv", "hevc_qsv", "h264_nvenc", "hevc_nvenc"}}

        Property Mode As New OptionParam With {
            .Name = "Mode",
            .Text = "Mode:",
            .VisibleFunc = Function() Not Codec.OptionText.EqualsAny("ProRes"),
            .Options = {"Quality", "One Pass", "Two Pass"}}

        Property Decoder As New OptionParam With {
            .Text = "Decoder:",
            .Options = {"AviSynth/VapourSynth", "Intel", "DXVA2"},
            .Values = {"avs", "qsv", "dxva2"}}

        Private ItemsValue As List(Of CommandLineParam)

        Overrides ReadOnly Property Items As List(Of CommandLineParam)
            Get
                If ItemsValue Is Nothing Then
                    ItemsValue = New List(Of CommandLineParam)

                    ItemsValue.AddRange({Decoder, Codec, Mode,
                                        New OptionParam With {.Name = "x264/x265 preset", .Text = "Preset:", .Switch = "-preset", .InitValue = 5, .Options = {"ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow", "placebo"}, .VisibleFunc = Function() Codec.OptionText.EqualsAny("x264", "x265")},
                                        New OptionParam With {.Name = "x264/x265 tune", .Text = "Tune:", .Switch = "-tune", .Options = {"none", "film", "animation", "grain", "stillimage", "psnr", "ssim", "fastdecode", "zerolatency"}, .VisibleFunc = Function() Codec.OptionText.EqualsAny("x264", "x265")},
                                        New OptionParam With {.Switch = "-profile:v", .Text = "Profile:", .VisibleFunc = Function() Codec.OptionText = "ProRes", .InitValue = 3, .IntegerValue = True, .Options = {"Proxy", "LT", "Normal", "HQ"}},
                                        New OptionParam With {.Switch = "-speed", .Text = "Speed:", .AlwaysOn = True, .VisibleFunc = Function() Codec.OptionText = "VP9", .Options = {"6 - Fastest", "5 - Faster", "4 - Fast", "3 - Medium", "2 - Slow", "1 - Slower", "0 - Slowest"}, .Values = {"6", "5", "4", "3", "2", "1", "0"}, .Value = 5},
                                        New OptionParam With {.Switch = "-aq-mode", .Text = "AQ Mode:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Options = {"Disabled", "0", "1", "2", "3"}, .Values = {"Disabled", "0", "1", "2", "3"}},
                                        New OptionParam With {.Name = "h264_nvenc profile", .Switch = "-profile", .Text = "Profile:", .Options = {"baseline", "main", "high", "high444p"}, .InitValue = 1, .VisibleFunc = Function() Codec.ValueText = "h264_nvenc"},
                                        New OptionParam With {.Name = "h264_nvenc preset", .Switch = "-preset", .Text = "Preset:", .Options = {"default", "slow", "medium", "fast", "hp", "hq", "bd", "ll", "llhq", "llhp", "lossless", "losslesshp"}, .InitValue = 2, .VisibleFunc = Function() Codec.ValueText = "h264_nvenc"},
                                        New OptionParam With {.Name = "h264_nvenc level", .Switch = "-level", .Text = "Level:", .Options = {"auto", "1", "1.0", "1b", "1.0b", "1.1", "1.2", "1.3", "2", "2.0", "2.1", "2.2", "3", "3.0", "3.1", "3.2", "4", "4.0", "4.1", "4.2", "5", "5.0", "5.1"}, .VisibleFunc = Function() Codec.ValueText = "h264_nvenc"},
                                        New OptionParam With {.Name = "h264_nvenc rc", .Switch = "-rc", .Text = "Rate Control:", .Options = {"preset", "constqp", "vbr", "cbr", "vbr_minqp", "ll_2pass_quality", "ll_2pass_size", "vbr_2pass"}, .VisibleFunc = Function() Codec.ValueText = "h264_nvenc"},
                                        New NumParam With {.Name = "Quality", .Text = "Quality:", .VisibleFunc = Function() Mode.Value = EncodingMode.Quality AndAlso Not Codec.OptionText.EqualsAny("ProRes"), .ArgsFunc = AddressOf GetQualityArgs, .MinMaxStep = {0, 63, 1}},
                                        New NumParam With {.Switch = "-threads", .Text = "Decoding Threads:", .MinMaxStep = {0, 64, 1}},
                                        New NumParam With {.Name = "VP9 enc threads", .Switch = "-threads", .Text = "Encoding Threads:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Value = 8, .DefaultValue = -1},
                                        New NumParam With {.Switch = "-tile-columns", .Text = "Tile Columns:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Value = 6, .DefaultValue = -1},
                                        New NumParam With {.Switch = "-frame-parallel", .Text = "Frame Parallel:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Value = 1, .DefaultValue = -1},
                                        New NumParam With {.Switch = "-auto-alt-ref", .Text = "Auto Alt Ref:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Value = 1, .DefaultValue = -1},
                                        New NumParam With {.Switch = "-lag-in-frames", .Text = "Lag In Frames:", .VisibleFunc = Function() Codec.OptionText = "VP9", .Value = 25, .DefaultValue = -1},
                                        New BoolParam With {.Name = "h264_qsv Lookahead", .Text = "Lookahead", .Switch = "-look_ahead 1", .NoSwitch = "-look_ahead 0", .Value = False, .DefaultValue = True, .VisibleFunc = Function() Codec.ValueText = "h264_qsv"},
                                        New BoolParam With {.Name = "h264_qsv VCM", .Text = "VCM", .Switch = "-vcm 1", .NoSwitch = "-vcm 0", .Value = False, .DefaultValue = True, .VisibleFunc = Function() Codec.ValueText = "h264_qsv"},
                                        New StringParam With {.Text = "Custom Switches:"}})
                End If

                Return ItemsValue
            End Get
        End Property

        Overloads Overrides Function GetCommandLine(includePaths As Boolean,
                                                    includeExecutable As Boolean,
                                                    Optional pass As Integer = 1) As String
            Dim sourcePath = p.Script.Path
            Dim ret As String

            If includePaths AndAlso includeExecutable Then ret = Package.ffmpeg.Path.Quotes

            Select Case Decoder.ValueText
                Case "qsv"
                    sourcePath = p.LastOriginalSourceFile
                    ret += " -hwaccel qsv"
                Case "dxva2"
                    sourcePath = p.LastOriginalSourceFile
                    ret += " -hwaccel dxva2"
            End Select

            If includePaths Then ret += " -i " + sourcePath.Quotes
            Dim items = From i In Me.Items Where i.GetArgs <> ""
            If items.Count > 0 Then ret += " " + items.Select(Function(item) item.GetArgs).Join(" ")

            If Calc.IsARSignalingRequired Then ret += " -aspect " + Calc.GetTargetDAR.ToInvariantString.Shorten(8)

            Select Case Mode.Value
                Case EncodingMode.TwoPass
                    ret += " -pass " & pass
                    ret += $" -b:v {p.VideoBitrate}k"
                    If pass = 1 Then ret += " -f rawvideo"
                Case EncodingMode.OnePass
                    ret += $" -b:v {p.VideoBitrate}k"
            End Select

            If Codec.OptionText = "Xvid" Then ret += " -tag:v xvid"

            Dim targetPath As String

            If Mode.OptionText = "Two Pass" AndAlso pass = 1 Then
                targetPath = "NUL"
            Else
                targetPath = p.VideoEncoder.OutputPath.ChangeExt(p.VideoEncoder.OutputExt).Quotes
            End If

            ret += " -an -y -hide_banner"
            If includePaths Then ret += " " + targetPath
            Return ret.Trim
        End Function

        Public Overrides Function GetPackage() As Package
            Return Package.ffmpeg
        End Function

        Function GetQualityArgs() As String
            If Mode.Value = EncodingMode.Quality Then
                Dim param = GetNumParamByName("Quality")

                If param.Value <> param.DefaultValue Then
                    If Codec.OptionText = "VP9" Then
                        Return "-crf " & param.Value & " -b:v 0"
                    ElseIf Codec.OptionText.EqualsAny("x264", "x265") Then
                        Return "-crf " & param.Value
                    Else
                        Return "-q:v " & param.Value
                    End If
                End If
            End If
        End Function
    End Class

    Enum EncodingMode
        Quality
        OnePass
        TwoPass
    End Enum
End Class
﻿Imports System.Text
Imports StaxRip.CommandLine
Imports StaxRip.UI

<Serializable()>
Public Class x265Encoder
    Inherits BasicVideoEncoder

    Property ParamsStore As New PrimitiveStore

    Sub New()
        Name = "x265"
        AutoCompCheckValue = 50
        Params.ApplyPresetDefaultValues()
        Params.ApplyPresetValues()
    End Sub

    <NonSerialized>
    Private ParamsValue As x265Params

    Property Params As x265Params
        Get
            If ParamsValue Is Nothing Then
                ParamsValue = New x265Params
                ParamsValue.Init(ParamsStore)
            End If

            Return ParamsValue
        End Get
        Set(value As x265Params)
            ParamsValue = value
        End Set
    End Property

    Overrides ReadOnly Property OutputExt As String
        Get
            Return "hevc"
        End Get
    End Property

    Overrides Sub Encode()
        p.Script.Synchronize()
        Encode("Encoding video using x265 " + Package.x265.Version, GetArgs(1, p.Script), s.ProcessPriority)

        If Params.Mode.Value = x265RateMode.TwoPass Then
            Encode("Encoding video second pass using x265 " + Package.x265.Version, GetArgs(2, p.Script), s.ProcessPriority)
        ElseIf Params.Mode.Value = x265RateMode.ThreePass Then
            Encode("Encoding video second pass using x265 " + Package.x265.Version, GetArgs(3, p.Script), s.ProcessPriority)
            Encode("Encoding video third pass using x265 " + Package.x265.Version, GetArgs(2, p.Script), s.ProcessPriority)
        End If

        AfterEncoding()
    End Sub

    Overloads Sub Encode(passName As String,
                         batchCode As String,
                         priority As ProcessPriorityClass)

        p.Script.Synchronize()
        Dim batchPath = p.TempDir + p.TargetFile.Base + "_encode.bat"
        batchCode = Proc.WriteBatchFile(batchPath, batchCode)

        Using proc As New Proc
            proc.Init(passName)
            proc.Encoding = Encoding.UTF8
            proc.Priority = priority
            proc.SkipStrings = {"%] "}
            proc.WriteLine(batchCode + BR2)
            proc.File = "cmd.exe"
            proc.Arguments = "/C call " + batchPath.Quotes
            proc.Start()
        End Using
    End Sub

    Overrides Sub RunCompCheck()
        If Not g.VerifyRequirements Then Exit Sub
        If Not g.IsValidSource Then Exit Sub

        Dim newParams As New x265Params
        Dim newStore = DirectCast(ObjectHelp.GetCopy(ParamsStore), PrimitiveStore)
        newParams.Init(newStore)

        Dim enc As New x265Encoder
        enc.Params = newParams
        enc.Params.Mode.Value = x265RateMode.SingleCRF
        enc.Params.Quant.Value = enc.Params.CompCheck.Value

        Dim script As New VideoScript
        script.Engine = p.Script.Engine
        script.Filters = p.Script.GetFiltersCopy
        Dim code As String
        Dim every = ((100 \ p.CompCheckRange) * 14).ToString

        If script.Engine = ScriptEngine.AviSynth Then
            code = "SelectRangeEvery(" + every + ",14)"
        Else
            code = "fpsnum = clip.fps_num" + BR + "fpsden = clip.fps_den" + BR +
                "clip = core.std.SelectEvery(clip = clip, cycle = " + every + ", offsets = range(14))" + BR +
                "clip = core.std.AssumeFPS(clip = clip, fpsnum = fpsnum, fpsden = fpsden)"
        End If

        script.Filters.Add(New VideoFilter("aaa", "aaa", code))
        script.Path = p.TempDir + p.Name + "_CompCheck." + script.FileType
        script.Synchronize()

        Log.WriteLine(BR + script.GetFullScript + BR)

        Dim commandLine = enc.Params.GetArgs(0, script, p.TempDir + p.Name + "_CompCheck." + OutputExt, True, True)

        Try
            Encode("Compressibility Check", commandLine, ProcessPriorityClass.Normal)
        Catch ex As AbortException
            ProcessForm.CloseProcessForm()
            Exit Sub
        Catch ex As Exception
            ProcessForm.CloseProcessForm()
            g.ShowException(ex)
            Exit Sub
        End Try

        Dim bits = (New FileInfo(p.TempDir + p.Name + "_CompCheck." + OutputExt).Length) * 8
        p.Compressibility = (bits / script.GetFrames) / (p.TargetWidth * p.TargetHeight)

        OnAfterCompCheck()
        g.MainForm.Assistant()

        Log.WriteLine("Quality: " & CInt(Calc.GetPercent).ToString() + " %")
        Log.WriteLine("Compressibility: " + p.Compressibility.ToString("f3"))
        Log.Save()

        ProcessForm.CloseProcessForm()
    End Sub

    Overloads Function GetArgs(pass As Integer, script As VideoScript, Optional includePaths As Boolean = True) As String
        Return Params.GetArgs(pass, script, OutputPath.DirAndBase + OutputExtFull, includePaths, True)
    End Function

    Overrides Sub ShowConfigDialog()
        Dim newParams As New x265Params
        Dim store = DirectCast(ObjectHelp.GetCopy(ParamsStore), PrimitiveStore)
        newParams.Init(store)
        newParams.ApplyPresetDefaultValues()
        newParams.ApplyTuneDefaultValues()

        Using f As New CommandLineForm(newParams)
            Dim saveProfileAction = Sub()
                                        Dim enc = ObjectHelp.GetCopy(Of x265Encoder)(Me)
                                        Dim params2 As New x265Params
                                        Dim store2 = DirectCast(ObjectHelp.GetCopy(store), PrimitiveStore)
                                        params2.Init(store2)
                                        enc.Params = params2
                                        enc.ParamsStore = store2
                                        SaveProfile(enc)
                                    End Sub

            ActionMenuItem.Add(f.cms.Items, "Save Profile...", saveProfileAction).SetImage(Symbol.Save)

            If f.ShowDialog() = DialogResult.OK Then
                Params = newParams
                ParamsStore = store
                OnStateChange()
            End If
        End Using
    End Sub

    Overrides Property QualityMode() As Boolean
        Get
            Return Params.Mode.Value = x265RateMode.SingleQuant OrElse
                Params.Mode.Value = x265RateMode.SingleCRF
        End Get
        Set(Value As Boolean)
        End Set
    End Property

    Public Overrides ReadOnly Property CommandLineParams As CommandLineParams
        Get
            Return Params
        End Get
    End Property

    Sub SetMuxer()
        Muxer = New MkvMuxer()
    End Sub

    Overrides Function CreateEditControl() As Control
        Return New x265Control(Me) With {.Dock = DockStyle.Fill}
    End Function
End Class

Public Class x265Params
    Inherits CommandLineParams

    Sub New()
        Title = "x265 Options"
    End Sub

    Property Decoder As New OptionParam With {
        .Text = "Decoder:",
        .Options = {"AviSynth/VapourSynth", "QSVEncC (Intel)", "ffmpeg (Intel)", "ffmpeg (DXVA2)"},
        .Values = {"avs", "qs", "ffqsv", "ffdxva"}}

    Property Quant As New NumParam With {
        .Switch = "--crf",
        .Switches = {"--qp"},
        .Name = "Quant",
        .Text = "Quality:",
        .ArgsFunc = Function() Nothing,
        .Value = 22,
        .DefaultValue = 28,
        .MinMaxStepDec = {0D, 51D, 1D, 1D}}

    Property Preset As New OptionParam With {
        .Switch = "--preset",
        .Text = "Preset:",
        .Options = {"Ultra Fast", "Super Fast", "Very Fast", "Faster", "Fast", "Medium", "Slow", "Slower", "Very Slow", "Placebo"},
        .Convert = True,
        .InitValue = 5}

    Property Tune As New OptionParam With {
        .Switch = "--tune",
        .Text = "Tune:",
        .Options = {"None", "PSNR", "SSIM", "Grain", "Fast Decode", "Zero Latency"},
        .Convert = True}

    Property Mode As New OptionParam With {
        .Name = "Mode",
        .Text = "Mode:",
        .Switches = {"--bitrate", "--qp", "--crf", "--pass"},
        .Options = {"Bitrate", "Quantizer", "Quality", "Two Pass", "Three Pass"},
        .Value = 2}

    Property SSIM As New BoolParam With {
        .Switch = "--ssim",
        .Text = "SSIM"}

    Property PSNR As New BoolParam With {
        .Switch = "--psnr",
        .Text = "PSNR"}

    Property BFrames As New NumParam With {
        .Switch = "--bframes",
        .Text = "B-Frames:",
        .MinMaxStep = {0, 16, 1}}

    Property BFrameBias As New NumParam With {
        .Switch = "--bframe-bias",
        .Text = "B-Frame Bias:",
        .MinMaxStep = {-90, 100, 1}}

    Property BAdapt As New OptionParam With {
        .Switch = "--b-adapt",
        .Text = "B-Adapt:",
        .IntegerValue = True,
        .Options = {"None", "Fast", "Full"}}

    Property RCLookahead As New NumParam With {
        .Switch = "--rc-lookahead",
        .Text = "RC Lookahead:",
        .MinMaxStep = {0, 250, 5}}

    Property LookaheadSlices As New NumParam With {
        .Switch = "--lookahead-slices",
        .Text = "Lookahead Slices:",
        .MinMaxStep = {0, 16, 1}}

    Property Scenecut As New NumParam With {
        .Switch = "--scenecut",
        .Text = "Scenecut:",
        .MinMaxStep = {0, 900, 10}}

    Property Ref As New NumParam With {
        .Switch = "--ref",
        .Text = "Ref Frames:",
        .MinMaxStep = {1, 16, 1}}

    Property [Me] As New OptionParam With {
        .Switch = "--me",
        .Text = "Motion Search Method:",
        .Options = {"Diamond", "Hexagon", "Uneven Multi-Hexegon", "Star", "SEA", "Full"},
        .Values = {"dia", "hex", "umh", "star", "sea", "full"}}

    Property MErange As New NumParam With {
        .Switch = "--merange",
        .Text = "ME Range (0=auto):",
        .MinMaxStep = {0, 32768, 1},
        .ArgsFunc = Function() If(MErange.Value = 0, "--merange " & CInt(Calc.GetYFromTwoPointForm(480, 16, 2160, 57, p.TargetHeight)), If(MErange.Value <> MErange.DefaultValue, "--merange " & CInt(MErange.Value), Nothing))}

    Property SubME As New OptionParam With {
        .Switch = "--subme",
        .Text = "Subpel Refinement:",
        .IntegerValue = True,
        .Options = {"0 - HPEL 1/4 - QPEL 0/4 - HPEL SATD false",
                    "1 - HPEL 1/4 - QPEL 1/4 - HPEL SATD false",
                    "2 - HPEL 1/4 - QPEL 1/4 - HPEL SATD true",
                    "3 - HPEL 2/4 - QPEL 1/4 - HPEL SATD true",
                    "4 - HPEL 2/4 - QPEL 2/4 - HPEL SATD true",
                    "5 - HPEL 1/8 - QPEL 1/8 - HPEL SATD true",
                    "6 - HPEL 2/8 - QPEL 1/8 - HPEL SATD true",
                    "7 - HPEL 2/8 - QPEL 2/8 - HPEL SATD true"},
        .Expand = True}

    Property MaxMerge As New NumParam With {
        .Switch = "--max-merge",
        .Text = "Max Merge:",
        .MinMaxStep = {1, 5, 1}}

    Property SAOnonDeblock As New BoolParam With {
        .Switch = "--sao-non-deblock",
        .Text = "Specify how to handle depencency between SAO and deblocking filter"}

    Property SAO As New BoolParam With {
        .Switch = "--sao",
        .NoSwitch = "--no-sao",
        .Text = "Sample Adaptive Offset"}

    Property SignHide As New BoolParam With {
        .Switch = "--signhide",
        .NoSwitch = "--no-signhide",
        .Text = "Hide sign bit of one coeff per TU (rdo)"}

    Property CompCheck As New NumParam With {
        .Name = "CompCheckQuant",
        .Text = "Comp. Check:",
        .Value = 18,
        .MinMaxStep = {1, 50, 1}}

    Property Weightp As New BoolParam With {
        .Switch = "--weightp",
        .NoSwitch = "--no-weightp",
        .Text = "Enable weighted prediction in P slices"}

    Property Weightb As New BoolParam With {
        .Switch = "--weightb",
        .NoSwitch = "--no-weightb",
        .Text = "Enable weighted prediction in B slices"}

    Property AQmode As New OptionParam With {
        .Switch = "--aq-mode",
        .Text = "AQ Mode:",
        .IntegerValue = True,
        .Options = {"Disabled", "Enabled", "Auto-Variance", "Auto-Variance and bias to dark scenes"}}

    Property Videoformat As New OptionParam With {
        .Switch = "--videoformat",
        .Text = "Videoformat:",
        .Options = {"Undefined", "Component", "PAL", "NTSC", "SECAM", "MAC"},
        .Convert = True}

    Property AQStrength As New NumParam With {
        .Switch = "--aq-strength",
        .Text = "AQ Strength:",
        .MinMaxStepDec = {0D, 3D, 0.05D, 2D}}

    Property CUtree As New BoolParam With {
        .Switch = "--cutree",
        .NoSwitch = "--no-cutree",
        .Text = "CU Tree",
        .Value = True}

    Property RD As New OptionParam With {
        .Switch = "--rd",
        .Text = "RD:",
        .IntegerValue = True,
        .Expand = True,
        .Options = {"0 - SA8D mode and split decisions, intra w/ source pixels",
                    "1 - Recon generated (better intra), RDO merge/skip selection",
                    "2 - RDO splits and merge/skip selection",
                    "3 - RDO mode and split decisions, chroma residual used for sa8d",
                    "4 - Currently same as 3",
                    "5 - Adds RDO prediction decisions",
                    "6 - Currently same as 5"}}

    Property MinCuSize As New OptionParam With {
        .Switch = "--min-cu-size",
        .Text = "Min CU size:",
        .Options = {"32", "16", "8"}}

    Property MaxCuSize As New OptionParam With {
        .Switch = "--ctu",
        .Text = "Max CU size:",
        .Options = {"64", "32", "16"}}

    Property qgSize As New OptionParam With {
        .Switch = "--qg-size",
        .Text = "QG Size:",
        .Options = {"64", "32", "16", "8"},
        .InitValue = 1}

    Property qpstep As New NumParam With {
        .Switch = "--qpstep",
        .Text = "QP Step:",
        .InitValue = 4}

    Property qpmin As New NumParam With {
        .Switch = "--qpmin",
        .Text = "QP Minimum:"}

    Property qpmax As New NumParam With {
        .Switch = "--qpmax",
        .Text = "QP Maximum:",
        .InitValue = 69}

    Property TUintra As New NumParam With {
        .Switch = "--tu-intra-depth",
        .Text = "TU Intra Depth:",
        .MinMaxStep = {1, 4, 1}}

    Property TUinter As New NumParam With {
        .Switch = "--tu-inter-depth",
        .Text = "TU Inter Depth:",
        .MinMaxStep = {1, 4, 1}}

    Property rdoqLevel As New NumParam With {
        .Switch = "--rdoq-level",
        .Switches = {"--rdoq"},
        .Text = "RDOQ Level:",
        .MinMaxStep = {0, 2, 1}}

    Property Rect As New BoolParam With {
        .Switch = "--rect",
        .NoSwitch = "--no-rect",
        .Text = "Enable analysis of rectangular motion partitions Nx2N and 2NxN"}

    Property rcGrain As New BoolParam With {
        .Switch = "--rc-grain",
        .NoSwitch = "--no-rc-grain",
        .Text = "Specialised ratecontrol for film grain content"}

    Property AMP As New BoolParam With {
        .Switch = "--amp",
        .NoSwitch = "--no-amp",
        .Text = "Enable analysis of asymmetric motion partitions"}

    Property EarlySkip As New BoolParam With {
        .Switch = "--early-skip",
        .NoSwitch = "--no-early-skip",
        .Text = "Early Skip"}

    Property FastIntra As New BoolParam With {
        .Switch = "--fast-intra",
        .NoSwitch = "--no-fast-intra",
        .Text = "Fast Intra"}

    Property BIntra As New BoolParam With {
        .Switch = "--b-intra",
        .NoSwitch = "--no-b-intra",
        .Text = "Evaluate intra modes in B slices"}

    Property CUlossless As New BoolParam With {
        .Switch = "--cu-lossless",
        .Text = "CU Lossless"}

    Property TskipFast As New BoolParam With {
        .Switch = "--tskip-fast",
        .Text = "Only evaluate transform skip for NxN intra predictions (4x4 blocks)"}

    Property PsyRD As New NumParam With {
        .Switch = "--psy-rd",
        .Text = "Psy RD:",
        .MinMaxStepDec = {0D, 5D, 0.05D, 2D}}

    Property PsyRDOQ As New NumParam With {
        .Switch = "--psy-rdoq",
        .Text = "Psy RDOQ:",
        .MinMaxStepDec = {0D, 50D, 0.05D, 2D}}

    Property CRFmax As New NumParam With {
        .Switch = "--crf-max",
        .Text = "Maximum CRF:",
        .MinMaxStepDec = {0D, 51D, 1D, 1D},
        .Value = 51,
        .DefaultValue = 51}

    Property CRFmin As New NumParam With {
        .Switch = "--crf-min",
        .Text = "Minimum CRF:",
        .MinMaxStepDec = {0D, 51D, 1D, 1D}}

    Property PBRatio As New NumParam With {
        .Switch = "--pbratio",
        .Switches = {"--pb-factor"},
        .Text = "PB Ratio:",
        .MinMaxStepDec = {0D, 1000D, 0.05D, 2D}}

    Property IPRatio As New NumParam With {
        .Switch = "--ipratio",
        .Switches = {"--ip-factor"},
        .Text = "IP Ratio:",
        .MinMaxStepDec = {0D, 1000D, 0.05D, 2D}}

    Property QComp As New NumParam With {
        .Switch = "--qcomp",
        .Text = "qComp:",
        .MinMaxStepDec = {0D, 1000D, 0.05D, 2D}}

    Property QBlur As New NumParam With {
        .Switch = "--qblur",
        .Text = "Q Blur:",
        .MinMaxStepDec = {Decimal.MinValue, Decimal.MaxValue, 0.05D, 2D},
        .Value = 0.5,
        .DefaultValue = 0.5}

    Property Cplxblur As New NumParam With {
        .Switch = "--cplxblur",
        .Text = "Blur Complexity:",
        .MinMaxStepDec = {Decimal.MinValue, Decimal.MaxValue, 0.05D, 2D},
        .Value = 20,
        .DefaultValue = 20}

    Property Colorprim As New OptionParam With {
        .Switch = "--colorprim",
        .Text = "Colorprim:",
        .Options = {"undefined", "bt709", "bt470m", "bt470bg", "smpte170m", "smpte240m", "film", "bt2020"}}

    Property Transfer As New OptionParam With {
        .Switch = "--transfer",
        .Text = "Transfer:",
        .Options = {"undefined", "bt709", "bt470m", "bt470bg", "smpte170m", "smpte240m", "linear", "log100", "log316", "iec61966-2-4", "bt1361e", "iec61966-2-1", "bt2020-10", "bt2020-12", "smpte-st-2084", "smpte-st-428", "arib-std-b67"}}

    Property Colormatrix As New OptionParam With {
        .Switch = "--colormatrix",
        .Text = "Colormatrix:",
        .Options = {"undefined", "GBR", "bt709", "fcc", "bt470bg", "smpte170m", "smpte240m", "YCgCo", "bt2020nc", "bt2020c"}}

    Property WPP As New BoolParam With {
        .Switch = "--wpp",
        .NoSwitch = "--no-wpp",
        .Text = "Wavefront Parallel Processing",
        .Value = True,
        .DefaultValue = True}

    Property Pmode As New BoolParam With {
        .Switch = "--pmode",
        .NoSwitch = "--no-pmode",
        .Text = "Parallel Mode Decision"}

    Property PME As New BoolParam With {
        .Switch = "--pme",
        .NoSwitch = "--no-pme",
        .Text = "Parallel Motion Estimation"}

    Property minLuma As New NumParam With {
        .Switch = "--min-luma",
        .Text = "Minimum Luma:"}

    Property maxLuma As New NumParam With {
        .Switch = "--max-luma",
        .Text = "Maximum Luma:"}

    Property Profile As New OptionParam With {
        .Switch = "--profile",
        .Text = "Profile:",
        .Options = {"Unrestricted", "main", "main-intra", "mainstillpicture", "main422-8", "main444-intra", "main444-stillpicture", "main444-8", "main10", "main10-intra", "main422-10", "main422-10-intra", "main444-10", "main444-10-intra"}}

    Property OutputDepth As New OptionParam With {
        .Switch = "--output-depth",
        .Text = "Depth:",
        .Options = {"8", "10", "12"}}

    Property Hash As New OptionParam With {
        .Switch = "--hash",
        .Text = "Hash:",
        .IntegerValue = True,
        .Options = {"None", "MD5", "CRC", "Checksum"}}

    Property TemporalMVP As New BoolParam With {
        .Switch = "--temporal-mvp",
        .NoSwitch = "--no-temporal-mvp",
        .Text = "Enable temporal motion vector predictors in P and B slices",
        .Value = True,
        .DefaultValue = True}

    Property StrongIntraSmoothing As New BoolParam With {
        .Switch = "--strong-intra-smoothing",
        .NoSwitch = "--no-strong-intra-smoothing",
        .Text = "Enable strong intra smoothing for 32x32 intra blocks",
        .Value = True,
        .DefaultValue = True}

    Property RDpenalty As New NumParam With {
        .Switch = "--rdpenalty",
        .Text = "RD Penalty (0=disabled):",
        .MinMaxStep = {0, 2, 1}}

    Property OpenGop As New BoolParam With {
        .Switch = "--open-gop",
        .NoSwitch = "--no-open-gop",
        .Text = "Open GOP",
        .InitValue = True}

    Property Bpyramid As New BoolParam With {
        .Switch = "--b-pyramid",
        .NoSwitch = "--no-b-pyramid",
        .Text = "B-Pyramid",
        .Value = True,
        .DefaultValue = True}

    Property Lossless As New BoolParam With {
        .Switch = "--lossless",
        .Text = "Lossless"}

    Property SlowFirstpass As New BoolParam With {
        .Switch = "--slow-firstpass",
        .NoSwitch = "--no-slow-firstpass",
        .Text = "Slow Firstpass",
        .InitValue = True}

    Property StrictCBR As New BoolParam With {
        .Switch = "--strict-cbr",
        .Text = "Strict CBR"}

    Property CBQPoffs As New NumParam With {
        .Switch = "--cbqpoffs",
        .Text = "Cb QP Offset:",
        .MinMaxStep = {-12, 12, 1}}

    Property CRQPoffs As New NumParam With {
        .Switch = "--crqpoffs",
        .Text = "CR QP Offset:",
        .MinMaxStep = {-12, 12, 1}}

    Property NRintra As New NumParam With {
        .Switch = "--nr-intra",
        .Text = "Intra Noise Reduct.:",
        .MinMaxStep = {0, 2000, 50}}

    Property NRinter As New NumParam With {
        .Switch = "--nr-inter",
        .Text = "Inter Noise Reduct.:",
        .MinMaxStep = {0, 2000, 50}}

    Property Keyint As New NumParam With {
        .Switch = "--keyint",
        .Text = "Max GOP Size:",
        .MinMaxStep = {0, 10000, 10},
        .Value = 250,
        .DefaultValue = 250}

    Property MinKeyint As New NumParam With {
        .Switch = "--min-keyint",
        .Text = "Min GOP Size:",
        .MinMaxStep = {0, 10000, 10}}

    Property VBVbufsize As New NumParam With {
        .Switch = "--vbv-bufsize",
        .Text = "VBV Bufsize:",
        .LabelMargin = New Padding With {.Right = 6, .Left = 6},
        .MinMaxStep = {0, 1000000, 100}}

    Property VBVmaxrate As New NumParam With {
        .Switch = "--vbv-maxrate",
        .Text = "VBV Maxrate:",
        .LabelMargin = New Padding With {.Right = 7},
        .MinMaxStep = {0, 1000000, 100}}

    Property VBVinit As New NumParam With {
        .Switch = "--vbv-init",
        .Text = "VBV Init:",
        .MinMaxStepDec = {0D, 1D, 0.05D, 2D},
        .Value = 0.9,
        .DefaultValue = 0.9}

    Property Chromaloc As New NumParam With {
        .Switch = "--chromaloc",
        .Text = "Chromaloc:",
        .MinMaxStep = {0, 5, 1}}

    Property FrameThreads As New NumParam With {
        .Switch = "--frame-threads",
        .Text = "Frame Threads:"}

    Property RepeatHeaders As New BoolParam With {
        .Switch = "--repeat-headers",
        .Text = "Repeat Headers"}

    Property Info As New BoolParam With {
        .Switch = "--info",
        .NoSwitch = "--no-info",
        .Text = "Info",
        .Value = True,
        .DefaultValue = True}

    Property HRD As New BoolParam With {
        .Switch = "--hrd",
        .Text = "HRD"}

    Property AUD As New BoolParam With {
        .Switch = "--aud",
        .Text = "AUD"}

    Property LimitModes As New BoolParam With {
        .Switch = "--limit-modes",
        .Text = "Limit Modes"}

    Property RdRefine As New BoolParam With {
        .Switch = "--rd-refine",
        .Text = "RD Refine"}

    Property MaxCLL As New NumParam With {
        .Text = "Maximum CLL:",
        .Switch = "--max-cll",
        .MinMaxStep = {0, Integer.MaxValue, 50},
        .ImportAction = Sub(arg As String)
                            Dim a = arg.Split(","c)
                            MaxCLL.Value = a(0).ToInt
                            MaxFALL.Value = a(1).ToInt
                        End Sub,
        .ArgsFunc = Function() If(MaxCLL.Value <> 0 OrElse MaxFALL.Value <> 0, "--max-cll """ & MaxCLL.Value & "," & MaxFALL.Value & """", "")}

    Property MaxFALL As New NumParam With {
        .MinMaxStep = {0, Integer.MaxValue, 50},
        .ArgsFunc = Function() "",
        .Text = "Maximum FALL:"}

    Property IntraRefresh As New BoolParam With {
        .Switch = "--intra-refresh",
        .Text = "Intra Refresh"}

    Property Custom As New StringParam With {
        .Text = "Custom:",
        .InitAction = Sub(tb)
                          tb.Edit.Expandet = True
                          tb.Edit.TextBox.Multiline = True
                          tb.Edit.MultilineHeightFactor = 6
                          tb.Edit.TextBox.Font = New Font("Consolas", 10 * s.UIScaleFactor)
                      End Sub}

    Property CustomFirstPass As New StringParam With {
        .Text = "Custom" + BR + "First Pass:",
        .ArgsFunc = Function() Nothing,
        .InitAction = Sub(tb)
                          tb.Edit.Expandet = True
                          tb.Edit.TextBox.Multiline = True
                          tb.Edit.MultilineHeightFactor = 6
                          tb.Edit.TextBox.Font = New Font("Consolas", 10 * s.UIScaleFactor)
                      End Sub}

    Property CustomSecondPass As New StringParam With {
        .Text = "Custom" + BR + "Second Pass:",
        .ArgsFunc = Function() Nothing,
        .InitAction = Sub(tb)
                          tb.Edit.Expandet = True
                          tb.Edit.TextBox.Multiline = True
                          tb.Edit.MultilineHeightFactor = 6
                          tb.Edit.TextBox.Font = New Font("Consolas", 10 * s.UIScaleFactor)
                      End Sub}

    Property Deblock As New BoolParam With {
        .Switch = "--deblock",
        .Text = "Deblocking",
        .ArgsFunc = AddressOf GetDeblockArgs}

    Property DeblockA As New NumParam With {
        .Text = "      Strength:",
        .MinMaxStep = {-6, 6, 1}}

    Property DeblockB As New NumParam With {
        .Text = "      Threshold:",
        .MinMaxStep = {-6, 6, 1}}

    Property MaxTuSize As New OptionParam With {
        .Switch = "--max-tu-size",
        .Text = "Max TU Size:",
        .Options = {"32", "16", "8", "4"}}

    Property LimitRefs As New OptionParam With {
        .Switch = "--limit-refs",
        .Text = "Limit References:",
        .Options = {"0", "1", "2", "3"},
        .InitValue = 3}

    Property CSV As New BoolParam With {
        .Switch = "--csv",
        .Text = "Write encoding results to a comma separated value log file",
        .ArgsFunc = Function() If(CSV.Value, "--csv """ + Filepath.GetDirAndBase(p.TargetFile) + ".csv""", Nothing)}

    Property RecursionSkip As New BoolParam With {
        .Switch = "--rskip",
        .NoSwitch = "--no-rskip",
        .Text = "Early CU depth recursion skip",
        .InitValue = True}

    Property csvloglevel As New OptionParam With {
        .Switch = "--csv-log-level",
        .Text = "CSV Log Level:",
        .IntegerValue = True,
        .Options = {"Default", "Summary", "Frame"}}

    Property multi_pass_opt_analysis As New BoolParam() With {
        .Switch = "--multi-pass-opt-analysis",
        .Text = "Multipass analysis refinement along with multipass ratecontrol"}

    Property multi_pass_opt_distortion As New BoolParam() With {
        .Switch = "--multi-pass-opt-distortion",
        .Text = "Multipass refinement of qp based on distortion data"}

    Property LimitTU As New NumParam With {
        .Switch = "--limit-tu",
        .Text = "Limit TU:", .MinMaxStep = {0, 4, 1}}

    Private ItemsValue As List(Of CommandLineParam)

    Overrides ReadOnly Property Items As List(Of CommandLineParam)
        Get
            If ItemsValue Is Nothing Then
                ItemsValue = New List(Of CommandLineParam)

                Add("Basic", Quant, Preset, Tune, Profile, OutputDepth,
                    New OptionParam With {.Switch = "--level-idc", .Switches = {"--level"}, .Text = "Level:", .Options = {"Unrestricted", "1", "2", "2.1", "3", "3.1", "4", "4.1", "5", "5.1", "5.2", "6", "6.1", "6.2", "8.5"}},
                    Mode)
                Add("Analysis 1", RD,
                    New StringParam With {.Switch = "--analysis-file", .Text = "Analysis File:", .Quotes = True, .BrowseFileFilter = "*.*|*.*"},
                    New OptionParam With {.Switch = "--analysis-mode", .Text = "Analysis Mode:", .Options = {"off", "save", "load"}},
                    MinCuSize, MaxCuSize, MaxTuSize, LimitRefs,
                    New NumParam With {.Switch = "--refine-level", .Text = "Refine Level:", .MinMaxStep = {1, 10, 1}, .InitValue = 5},
                    New NumParam With {.Switch = "--scale-factor", .Text = "Scale Factor:"},
                    LimitTU,
                    TUintra, TUinter, rdoqLevel,
                    New NumParam() With {.Switch = "--dynamic-rd", .Text = "Dynamic RD:", .MinMaxStepDec = {0, 4, 1, 1}})
                Add("Analysis 2", Rect, AMP,
                    New BoolParam With {.Switch = "--tskip", .Text = "Enable evaluation of transform skip coding for 4x4 TU coded blocks"},
                    New BoolParam With {.Switch = "--refine-inter", .Text = "Enable refinement of inter blocks"},
                    New BoolParam With {.Switch = "--refine-intra", .Text = "Enable refinement of intra blocks "},
                    New BoolParam With {.Switch = "--refine-mv", .Text = "Enable refinement of motion vector for scaled video"},
                    EarlySkip, FastIntra, BIntra,
                    CUlossless,
                    TskipFast, LimitModes, RdRefine,
                    New BoolParam With {.Switch = "--cu-stats", .Text = "CU Stats"},
                    RecursionSkip,
                    New BoolParam With {.Switch = "--ssim-rd", .Text = "SSIM RDO"})
                Add("Rate Control 1",
                    New StringParam With {.Switch = "--zones", .Text = "Zones:"},
                    AQmode, qgSize, AQStrength, QComp, CBQPoffs,
                    QBlur, NRintra, NRinter, qpmin, qpmax, qpstep)
                Add("Rate Control 2",
                    CRFmin, CRFmax, VBVbufsize, VBVmaxrate, VBVinit,
                    IPRatio, PBRatio, Cplxblur)
                Add("Rate Control 3",
                    CUtree, Lossless, StrictCBR, rcGrain,
                    multi_pass_opt_analysis,
                    multi_pass_opt_distortion,
                    New BoolParam() With {.Switch = "--aq-motion", .Text = "AQ Motion"})
                Add("Motion Search", SubME, [Me], MErange, MaxMerge, Weightp, Weightb, TemporalMVP,
                    New BoolParam With {.Switch = "--analyze-src-pics", .NoSwitch = "--no-analyze-src-pics", .Text = "Analyze SRC Pics"})
                Add("Slice Decision 1", BAdapt,
                    New OptionParam With {.Switch = "--ctu-info", .Text = "CTU Info", .Options = {"0", "1", "2", "4", "6"}},
                    BFrames, BFrameBias,
                    RCLookahead,
                    LookaheadSlices,
                    New NumParam() With {.Switch = "--lookahead-threads", .Text = "Lookahead Threads:"},
                    Scenecut,
                    New NumParam() With {.Switch = "--scenecut-bias", .Text = "Scenecut Bias:", .InitValue = 5, .MinMaxStepDec = {0, 100, 1, 1}},
                    Ref, MinKeyint, Keyint)
                Add("Slice Decision 2", Bpyramid, OpenGop, IntraRefresh)
                Add("Spatial/Intra", StrongIntraSmoothing,
                    New BoolParam With {.Switch = "--constrained-intra", .NoSwitch = "--no-constrained-intra", .Switches = {"--cip"}, .Text = "Constrained Intra Prediction", .InitValue = True},
                    RDpenalty)
                Add("Performance",
                    New StringParam With {.Switch = "--pools", .Switches = {"--numa-pools"}, .Text = "Pools:", .Quotes = True},
                    New NumParam With {.Switch = "--slices", .Text = "Slices:", .InitValue = 1},
                    FrameThreads, WPP, Pmode, PME,
                    New BoolParam With {.Switch = "--asm", .NoSwitch = "--no-asm", .Text = "ASM", .InitValue = True})
                Add("Statistic",
                    New OptionParam With {.Switch = "--log-level", .Switches = {"--log"}, .Text = "Log Level:", .Options = {"none", "error", "warning", "info", "debug", "full"}, .Value = 3, .DefaultValue = 3},
                    csvloglevel, CSV, SSIM, PSNR)
                Add("VUI 1",
                    New StringParam With {.Switch = "--master-display", .Text = "Master Display:", .Quotes = True},
                    New StringParam With {.Switch = "--sar", .Text = "Sample Aspect Ratio:", .InitValue = "auto", .Menu = s.ParMenu, .ArgsFunc = AddressOf GetSAR},
                    Videoformat, Colorprim, Colormatrix, Transfer,
                    New OptionParam With {.Switch = "--overscan", .Text = "Overscan", .Options = {"undefined", "show", "crop"}},
                    New OptionParam With {.Switch = "--range", .Text = "Range", .Options = {"undefined", "full", "limited"}})
                Add("VUI 2",
                    minLuma, maxLuma, MaxCLL, MaxFALL,
                    New BoolParam With {.Switch = "--hdr", .Text = "Force signalling of HDR parameters in SEI packets"},
                    New BoolParam With {.Switch = "--hdr-opt", .Text = "Add luma and chroma offsets for HDR/WCG content"},
                    New BoolParam With {.Switch = "--dhdr10-opt", .Text = "Limit frames for which tone mapping information is inserted as SEI message"})
                Add("Bitstream",
                    Hash,
                    New NumParam With {.Switch = "--log2-max-poc-lsb", .Text = "log2-max-poc-lsb:", .InitValue = 8},
                    RepeatHeaders, Info, HRD, AUD,
                    New BoolParam With {.Switch = "--temporal-layers", .Text = "Temporal Layers"},
                    New BoolParam With {.Switch = "--vui-timing-info", .Text = "VUI Timing Info"},
                    New BoolParam With {.Switch = "--vui-hrd-info", .Text = "VUI HRD Info"},
                    New BoolParam With {.Switch = "--opt-cu-delta-qp", .Text = "Optimize CU level QPs pulling up lower QPs close to meanQP"},
                    New BoolParam With {.Switch = "--multi-pass-opt-rps", .Text = "Enable storing commonly used RPS in SPS in multi pass mode"})
                Add("Input/Output",
                    New OptionParam With {.Switch = "--input-depth", .Text = "Input Depth:", .Options = {"Automatic", "8", "10", "12", "14", "16"}},
                    New OptionParam With {.Switch = "--input-csp", .Text = "Input CSP:", .Options = {"Automatic", "i400", "i420", "i422", "i444", "nv12", "nv16"}},
                    New OptionParam With {.Switch = "--interlace", .Text = "Interlace:", .Options = {"Progressive", "Top Field First", "Bottom Field First"}, .Values = {"", "tff", "bff"}},
                    New OptionParam With {.Switch = "--fps", .Text = "FPS:", .Options = {"Automatic", "24", "24000/1001", "25", "30000/1001", "50", "60000/1001"}},
                    New NumParam With {.Switch = "--seek", .Text = "Seek:"},
                    New BoolParam With {.Switch = "--dither", .Text = "Dither (High Quality Downscaling)"})
                Add("Loop Filter", Deblock, DeblockA, DeblockB, SAO,
                    New BoolParam With {.Switch = "--limit-sao", .Text = "Limit Sample Adaptive Offset"},
                    SAOnonDeblock)
                Add("Other",
                    New StringParam With {.Switch = "--lambda-file", .Text = "Lambda File:", .Quotes = True, .BrowseFileFilter = "*.*|*.*"},
                    New StringParam With {.Switch = "--qpfile", .Text = "QP File:", .Quotes = True, .BrowseFileFilter = "*.*|*.*"},
                    New StringParam With {.Switch = "--recon", .Text = "Recon File:", .Quotes = True, .BrowseFileFilter = "*.*|*.*"},
                    New StringParam With {.Switch = "--scaling-list", .Text = "Scaling List:", .Quotes = True},
                    Decoder, PsyRD, PsyRDOQ, CompCheck,
                    New NumParam With {.Switch = "--recon-depth", .Text = "Recon Depth:"},
                    New BoolParam With {.Switch = "--high-tier", .Text = "High Tier", .Name = "HighTierV2", .InitValue = True},
                    SlowFirstpass, SignHide,
                    New BoolParam With {.Switch = "--allow-non-conformance", .Text = "Allow non conformance"},
                    New BoolParam With {.Switch = "--uhd-bd", .Text = "Ultra HD Blu-ray"})
                Add("Custom", Custom, CustomFirstPass, CustomSecondPass)

                For Each i In ItemsValue
                    If i.Switch <> "" Then i.URL =
                        "http://x265.readthedocs.io/en/latest/cli.html#cmdoption" +
                        i.Switch?.Substring(1)
                Next
            End If

            Return ItemsValue
        End Get
    End Property

    Private Sub Add(path As String, ParamArray items As CommandLineParam())
        For Each i In items
            i.Path = path
            ItemsValue.Add(i)
        Next
    End Sub

    Private BlockValueChanged As Boolean

    Protected Overrides Sub OnValueChanged(item As CommandLineParam)
        If BlockValueChanged Then Exit Sub

        If item Is Preset OrElse item Is Tune Then
            BlockValueChanged = True
            ApplyPresetValues()
            ApplyTuneValues()
            BlockValueChanged = False
        End If

        DeblockA.NumEdit.Enabled = Deblock.Value
        DeblockB.NumEdit.Enabled = Deblock.Value

        MyBase.OnValueChanged(item)
    End Sub

    Overloads Overrides Function GetCommandLine(includePaths As Boolean,
                                                includeExecutable As Boolean,
                                                Optional pass As Integer = 1) As String

        Return GetArgs(1, p.Script, p.VideoEncoder.OutputPath.DirAndBase +
                       p.VideoEncoder.OutputExtFull, includePaths, includeExecutable)
    End Function

    Overloads Function GetArgs(pass As Integer,
                               script As VideoScript,
                               targetPath As String,
                               includePaths As Boolean,
                               includeExecutable As Boolean) As String

        ApplyPresetDefaultValues()
        ApplyTuneDefaultValues()

        Dim sb As New StringBuilder

        If includePaths AndAlso includeExecutable Then
            Dim isCropped = CInt(p.CropLeft Or p.CropTop Or p.CropRight Or p.CropBottom) <> 0 AndAlso
                Decoder.ValueText <> "avs" AndAlso p.Script.IsFilterActive("Crop")

            Select Case Decoder.ValueText
                Case "avs"
                    If p.Script.Engine = ScriptEngine.VapourSynth Then
                        sb.Append(Package.vspipe.Path.Quotes + " " + script.Path.Quotes + " - --y4m | " + Package.x265.Path.Quotes)
                    Else
                        sb.Append(Package.avs2pipemod.Path.Quotes + " -y4mp " + script.Path.Quotes + " | " + Package.x265.Path.Quotes)
                    End If
                Case "qs"
                    Dim crop = If(isCropped, " --crop " & p.CropLeft & "," & p.CropTop & "," & p.CropRight & "," & p.CropBottom, "")
                    sb.Append(Package.QSVEncC.Path.Quotes + " -o - -c raw" + crop + " -i " + p.SourceFile.Quotes + " | " + Package.x265.Path.Quotes)
                Case "ffqsv"
                    Dim crop = If(isCropped, $" -vf ""crop={p.SourceWidth - p.CropLeft - p.CropRight}:{p.SourceHeight - p.CropTop - p.CropBottom}:{p.CropLeft}:{p.CropTop}""", "")
                    sb.Append(Package.ffmpeg.Path.Quotes + " -threads 1 -hwaccel qsv -i " + p.SourceFile.Quotes + " -f yuv4mpegpipe" + crop + " -loglevel error -hide_banner - | " + Package.x265.Path.Quotes)
                Case "ffdxva"
                    Dim crop = If(isCropped, $" -vf ""crop={p.SourceWidth - p.CropLeft - p.CropRight}:{p.SourceHeight - p.CropTop - p.CropBottom}:{p.CropLeft}:{p.CropTop}""", "")
                    sb.Append(Package.ffmpeg.Path.Quotes + " -threads 1 -hwaccel dxva2 -i " + p.SourceFile.Quotes + " -f yuv4mpegpipe" + crop + " -loglevel error -hide_banner - | " + Package.x265.Path.Quotes)
            End Select
        End If

        If Mode.Value = x265RateMode.TwoPass OrElse Mode.Value = x265RateMode.ThreePass Then
            sb.Append(" --pass " & pass)

            If pass = 1 Then
                If CustomFirstPass.Value <> "" Then sb.Append(" " + CustomFirstPass.Value)
            Else
                If CustomSecondPass.Value <> "" Then sb.Append(" " + CustomSecondPass.Value)
            End If

            If includePaths AndAlso (multi_pass_opt_distortion.Value OrElse multi_pass_opt_analysis.Value) Then
                sb.Append(" --analysis-file " + targetPath.ChangeExt("analysis").Quotes)
            End If
        End If

        If Mode.Value = x265RateMode.SingleQuant Then
            If Not IsCustom(pass, "--qp") Then sb.Append(" --qp " + CInt(Quant.Value).ToString)
        ElseIf Mode.Value = x265RateMode.SingleCRF Then
            If Quant.Value <> 28 AndAlso Not IsCustom(pass, "--crf") Then
                sb.Append(" --crf " + Quant.Value.ToInvariantString)
            End If
        Else
            If Not IsCustom(pass, "--bitrate") Then sb.Append(" --bitrate " & p.VideoBitrate)
        End If

        Dim q = From i In Items Where i.GetArgs <> "" AndAlso Not IsCustom(pass, i.Switch)
        If q.Count > 0 Then sb.Append(" " + q.Select(Function(item) item.GetArgs).Join(" "))

        If includePaths Then
            sb.Append(" --frames " & script.GetFrames)
            sb.Append(" --y4m")

            If Mode.Value = x265RateMode.TwoPass OrElse Mode.Value = x265RateMode.ThreePass Then
                sb.Append(" --stats " + (p.TempDir + p.Name + ".stats").Quotes)
            End If

            If (Mode.Value = x265RateMode.ThreePass AndAlso pass < 3) OrElse
                Mode.Value = x265RateMode.TwoPass AndAlso pass = 1 Then

                sb.Append(" --output NUL -")
            Else
                sb.Append(" --output " + targetPath.Quotes + " - ")
            End If
        End If

        Return Macro.Expand(sb.ToString.Trim.FixBreak.Replace(BR, " "))
    End Function

    Function IsCustom(pass As Integer, switch As String) As Boolean
        If switch = "" Then Return False

        If Mode.Value = x265RateMode.TwoPass OrElse Mode.Value = x265RateMode.ThreePass Then
            If pass = 1 Then
                If CustomFirstPass.Value?.Contains(switch + " ") OrElse
                    CustomFirstPass.Value?.EndsWith(switch) Then Return True
            Else
                If CustomSecondPass.Value?.Contains(switch + " ") OrElse
                    CustomSecondPass.Value?.EndsWith(switch) Then Return True
            End If
        End If

        If Custom.Value?.Contains(switch + " ") OrElse Custom.Value?.EndsWith(switch) Then Return True
    End Function

    Function GetDeblockArgs() As String
        If Deblock.Value Then
            If DeblockA.Value = DeblockA.DefaultValue AndAlso
                DeblockB.Value = DeblockB.DefaultValue AndAlso
                Deblock.DefaultValue Then

                Return ""
            Else
                Return "--deblock " & DeblockA.Value & ":" & DeblockB.Value
            End If
        ElseIf Deblock.DefaultValue Then
            Return "--no-deblock"
        End If
    End Function

    Sub ApplyPresetValues()
        AQmode.Value = 1
        AQStrength.Value = 1
        DeblockA.Value = 0
        DeblockB.Value = 0
        FrameThreads.Value = 0
        IPRatio.Value = 1.4
        LimitModes.Value = False
        LimitRefs.Value = 3
        LimitTU.Value = 0
        LookaheadSlices.Value = 8
        MaxCuSize.Value = 0
        MinCuSize.Value = 3
        PBRatio.Value = 1.3
        PsyRD.Value = 2.0
        PsyRDOQ.Value = 1
        QComp.Value = 0.6
        RecursionSkip.Value = True

        Select Case Preset.Value
            Case 0 'ultrafast
                [Me].Value = 0
                AMP.Value = False
                AQmode.Value = 0
                AQStrength.Value = 0
                BAdapt.Value = 0
                BFrames.Value = 3
                BIntra.Value = False
                CUtree.Value = False
                Deblock.Value = True
                EarlySkip.Value = True
                FastIntra.Value = True
                LimitRefs.Value = 0
                MaxCuSize.Value = 1
                MaxMerge.Value = 2
                MErange.Value = 25
                MinCuSize.Value = 2
                RCLookahead.Value = 5
                RD.Value = 2
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 1
                SAO.Value = False
                Scenecut.Value = 0
                SignHide.Value = False
                SubME.Value = 0
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = False
            Case 1 'superfast
                [Me].Value = 1
                AMP.Value = False
                AQmode.Value = 0
                AQStrength.Value = 0
                BAdapt.Value = 0
                BFrames.Value = 3
                BIntra.Value = False
                CUtree.Value = False
                Deblock.Value = True
                EarlySkip.Value = True
                FastIntra.Value = True
                LimitRefs.Value = 0
                MaxCuSize.Value = 1
                MaxMerge.Value = 2
                MErange.Value = 44
                RCLookahead.Value = 10
                RD.Value = 2
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 1
                SAO.Value = False
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 1
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = False
            Case 2 'veryfast
                [Me].Value = 1
                AMP.Value = False
                BAdapt.Value = 0
                BFrames.Value = 4
                BIntra.Value = False
                CUtree.Value = False
                Deblock.Value = True
                EarlySkip.Value = True
                FastIntra.Value = True
                MaxCuSize.Value = 1
                MaxMerge.Value = 2
                MErange.Value = 57
                RCLookahead.Value = 15
                RD.Value = 2
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 2
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 1
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = True
            Case 3 'faster
                [Me].Value = 1
                AMP.Value = False
                BAdapt.Value = 0
                BFrames.Value = 4
                BIntra.Value = False
                CUtree.Value = False
                Deblock.Value = True
                EarlySkip.Value = True
                FastIntra.Value = True
                MaxMerge.Value = 2
                MErange.Value = 57
                RCLookahead.Value = 15
                RD.Value = 2
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 2
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 2
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = True
            Case 4 'fast
                [Me].Value = 1
                AMP.Value = False
                BAdapt.Value = 2
                BFrames.Value = 4
                BIntra.Value = False
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = True
                MaxMerge.Value = 2
                MErange.Value = 57
                RCLookahead.Value = 15
                RD.Value = 2
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 3
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 2
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = True
            Case 5 'medium
                [Me].Value = 1
                AMP.Value = False
                BAdapt.Value = 2
                BFrames.Value = 4
                BIntra.Value = False
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = False
                MaxMerge.Value = 2
                MErange.Value = 57
                RCLookahead.Value = 20
                RD.Value = 3
                rdoqLevel.Value = 0
                Rect.Value = False
                Ref.Value = 3
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 2
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = True
            Case 6 'slow
                [Me].Value = 3
                AMP.Value = False
                BAdapt.Value = 2
                BFrames.Value = 4
                BIntra.Value = False
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = False
                LimitModes.Value = True
                LookaheadSlices.Value = 4
                MaxMerge.Value = 3
                MErange.Value = 57
                RCLookahead.Value = 25
                RD.Value = 4
                rdoqLevel.Value = 2
                Rect.Value = True
                Ref.Value = 4
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 3
                TUinter.Value = 1
                TUintra.Value = 1
                Weightb.Value = False
                Weightp.Value = True
            Case 7 'slower
                [Me].Value = 3
                AMP.Value = True
                BAdapt.Value = 2
                BFrames.Value = 8
                BIntra.Value = True
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = False
                LimitModes.Value = True
                LimitRefs.Value = 2
                LimitTU.Value = 4
                LookaheadSlices.Value = 4
                MaxMerge.Value = 3
                MErange.Value = 57
                RCLookahead.Value = 30
                RD.Value = 6
                rdoqLevel.Value = 2
                Rect.Value = True
                Ref.Value = 4
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 3
                TUinter.Value = 2
                TUintra.Value = 2
                Weightb.Value = True
                Weightp.Value = True
            Case 8 'veryslow
                [Me].Value = 3
                AMP.Value = True
                BAdapt.Value = 2
                BFrames.Value = 8
                BIntra.Value = True
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = False
                LimitModes.Value = True
                LimitTU.Value = 4
                LookaheadSlices.Value = 4
                MaxMerge.Value = 4
                MErange.Value = 57
                RCLookahead.Value = 40
                RD.Value = 6
                rdoqLevel.Value = 2
                Rect.Value = True
                RecursionSkip.Value = False
                Ref.Value = 5
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 4
                TUinter.Value = 3
                TUintra.Value = 3
                Weightb.Value = True
                Weightp.Value = True
            Case 9 'placebo
                [Me].Value = 3
                AMP.Value = True
                BAdapt.Value = 2
                BFrames.Value = 8
                BIntra.Value = True
                CUtree.Value = True
                Deblock.Value = True
                EarlySkip.Value = False
                FastIntra.Value = False
                LimitRefs.Value = 0
                LookaheadSlices.Value = 0
                MaxMerge.Value = 5
                MErange.Value = 92
                RCLookahead.Value = 60
                RD.Value = 6
                rdoqLevel.Value = 2
                Rect.Value = True
                RecursionSkip.Value = False
                Ref.Value = 5
                SAO.Value = True
                Scenecut.Value = 40
                SignHide.Value = True
                SubME.Value = 5
                TUinter.Value = 4
                TUintra.Value = 4
                Weightb.Value = True
                Weightp.Value = True
        End Select
    End Sub

    Sub ApplyPresetDefaultValues()
        AQmode.DefaultValue = 1
        AQStrength.DefaultValue = 1
        DeblockA.DefaultValue = 0
        DeblockB.DefaultValue = 0
        FrameThreads.DefaultValue = 0
        IPRatio.DefaultValue = 1.4
        LimitModes.DefaultValue = False
        LimitRefs.DefaultValue = 3
        LimitTU.DefaultValue = 0
        LookaheadSlices.DefaultValue = 8
        MaxCuSize.DefaultValue = 0
        MinCuSize.DefaultValue = 3
        PBRatio.DefaultValue = 1.3
        PsyRD.DefaultValue = 2.0
        PsyRDOQ.DefaultValue = 1
        QComp.DefaultValue = 0.6
        RecursionSkip.DefaultValue = True

        Select Case Preset.Value
            Case 0 'ultrafast
                [Me].DefaultValue = 0
                AMP.DefaultValue = False
                AQmode.DefaultValue = 0
                AQStrength.DefaultValue = 0
                BAdapt.DefaultValue = 0
                BFrames.DefaultValue = 3
                BIntra.DefaultValue = False
                CUtree.DefaultValue = False
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = True
                FastIntra.DefaultValue = True
                LimitRefs.DefaultValue = 0
                MaxCuSize.DefaultValue = 1
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 25
                MinCuSize.DefaultValue = 2
                RCLookahead.DefaultValue = 5
                RD.DefaultValue = 2
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 1
                SAO.DefaultValue = False
                Scenecut.DefaultValue = 0
                SignHide.DefaultValue = False
                SubME.DefaultValue = 0
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = False
            Case 1 'superfast
                [Me].DefaultValue = 1
                AMP.DefaultValue = False
                AQmode.DefaultValue = 0
                AQStrength.DefaultValue = 0
                BAdapt.DefaultValue = 0
                BFrames.DefaultValue = 3
                BIntra.DefaultValue = False
                CUtree.DefaultValue = False
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = True
                FastIntra.DefaultValue = True
                LimitRefs.DefaultValue = 0
                MaxCuSize.DefaultValue = 1
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 44
                RCLookahead.DefaultValue = 10
                RD.DefaultValue = 2
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 1
                SAO.DefaultValue = False
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 1
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = False
            Case 2 'veryfast
                [Me].DefaultValue = 1
                AMP.DefaultValue = False
                BAdapt.DefaultValue = 0
                BFrames.DefaultValue = 4
                BIntra.DefaultValue = False
                CUtree.DefaultValue = False
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = True
                FastIntra.DefaultValue = True
                MaxCuSize.DefaultValue = 1
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 15
                RD.DefaultValue = 2
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 2
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 1
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = True
            Case 3 'faster
                [Me].DefaultValue = 1
                AMP.DefaultValue = False
                BAdapt.DefaultValue = 0
                BFrames.DefaultValue = 4
                BIntra.DefaultValue = False
                CUtree.DefaultValue = False
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = True
                FastIntra.DefaultValue = True
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 15
                RD.DefaultValue = 2
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 2
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 2
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = True
            Case 4 'fast
                [Me].DefaultValue = 1
                AMP.DefaultValue = False
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 4
                BIntra.DefaultValue = False
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = True
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 15
                RD.DefaultValue = 2
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 3
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 2
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = True
            Case 5 'medium
                [Me].DefaultValue = 1
                AMP.DefaultValue = False
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 4
                BIntra.DefaultValue = False
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = False
                MaxMerge.DefaultValue = 2
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 20
                RD.DefaultValue = 3
                rdoqLevel.DefaultValue = 0
                Rect.DefaultValue = False
                Ref.DefaultValue = 3
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 2
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = True
            Case 6 'slow
                [Me].DefaultValue = 3
                AMP.DefaultValue = False
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 4
                BIntra.DefaultValue = False
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = False
                LimitModes.DefaultValue = True
                LookaheadSlices.DefaultValue = 4
                MaxMerge.DefaultValue = 3
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 25
                RD.DefaultValue = 4
                rdoqLevel.DefaultValue = 2
                Rect.DefaultValue = True
                Ref.DefaultValue = 4
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 3
                TUinter.DefaultValue = 1
                TUintra.DefaultValue = 1
                Weightb.DefaultValue = False
                Weightp.DefaultValue = True
            Case 7 'slower
                [Me].DefaultValue = 3
                AMP.DefaultValue = True
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 8
                BIntra.DefaultValue = True
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = False
                LimitModes.DefaultValue = True
                LimitRefs.DefaultValue = 2
                LimitTU.DefaultValue = 4
                LookaheadSlices.DefaultValue = 4
                MaxMerge.DefaultValue = 3
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 30
                RD.DefaultValue = 6
                rdoqLevel.DefaultValue = 2
                Rect.DefaultValue = True
                Ref.DefaultValue = 4
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 3
                TUinter.DefaultValue = 2
                TUintra.DefaultValue = 2
                Weightb.DefaultValue = True
                Weightp.DefaultValue = True
            Case 8 'veryslow
                [Me].DefaultValue = 3
                AMP.DefaultValue = True
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 8
                BIntra.DefaultValue = True
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = False
                LimitModes.DefaultValue = True
                LimitTU.DefaultValue = 4
                LookaheadSlices.DefaultValue = 4
                MaxMerge.DefaultValue = 4
                MErange.DefaultValue = 57
                RCLookahead.DefaultValue = 40
                RD.DefaultValue = 6
                rdoqLevel.DefaultValue = 2
                Rect.DefaultValue = True
                RecursionSkip.DefaultValue = False
                Ref.DefaultValue = 5
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 4
                TUinter.DefaultValue = 3
                TUintra.DefaultValue = 3
                Weightb.DefaultValue = True
                Weightp.DefaultValue = True
            Case 9 'placebo
                [Me].DefaultValue = 3
                AMP.DefaultValue = True
                BAdapt.DefaultValue = 2
                BFrames.DefaultValue = 8
                BIntra.DefaultValue = True
                CUtree.DefaultValue = True
                Deblock.DefaultValue = True
                EarlySkip.DefaultValue = False
                FastIntra.DefaultValue = False
                LimitRefs.DefaultValue = 0
                LookaheadSlices.DefaultValue = 0
                MaxMerge.DefaultValue = 5
                MErange.DefaultValue = 92
                RCLookahead.DefaultValue = 60
                RD.DefaultValue = 6
                rdoqLevel.DefaultValue = 2
                Rect.DefaultValue = True
                RecursionSkip.DefaultValue = False
                Ref.DefaultValue = 5
                SAO.DefaultValue = True
                Scenecut.DefaultValue = 40
                SignHide.DefaultValue = True
                SubME.DefaultValue = 5
                TUinter.DefaultValue = 4
                TUintra.DefaultValue = 4
                Weightb.DefaultValue = True
                Weightp.DefaultValue = True
        End Select
    End Sub

    Sub ApplyTuneValues()
        rcGrain.Value = False
        qpstep.Value = 4

        Select Case Tune.Value
            Case 1 '"psnr"
                AQStrength.Value = 0.0
                PsyRD.Value = 0.0
                PsyRDOQ.Value = 0.0
            Case 2 '"ssim"
                AQmode.Value = 2
                PsyRD.Value = 0.0
                PsyRDOQ.Value = 0.0
            Case 3 'grain
                IPRatio.Value = 1.1
                PBRatio.Value = 1
                CUtree.Value = False
                AQmode.Value = 0
                qpstep.Value = 1
                rcGrain.Value = True
                RecursionSkip.Value = False
                PsyRD.Value = 4
                PsyRDOQ.Value = 10
                SAO.Value = False
            Case 4 '"fastdecode"
                Deblock.Value = False
                SAO.Value = False
                Weightp.Value = False
                Weightb.Value = False
                BIntra.Value = False
            Case 5 '"zerolatency"
                BAdapt.Value = 0
                BFrames.Value = 0
                RCLookahead.Value = 0
                Scenecut.Value = 0
                CUtree.Value = False
                FrameThreads.Value = 1
        End Select
    End Sub

    Sub ApplyTuneDefaultValues()
        rcGrain.DefaultValue = False
        qpstep.DefaultValue = 4

        Select Case Tune.Value
            Case 1 '"psnr"
                AQStrength.DefaultValue = 0.0
                PsyRD.DefaultValue = 0.0
                PsyRDOQ.DefaultValue = 0.0
            Case 2 '"ssim"
                AQmode.DefaultValue = 2
                PsyRD.DefaultValue = 0.0
                PsyRDOQ.DefaultValue = 0.0
            Case 3 'grain
                IPRatio.DefaultValue = 1.1
                PBRatio.DefaultValue = 1
                CUtree.DefaultValue = False
                AQmode.DefaultValue = 0
                qpstep.DefaultValue = 1
                rcGrain.DefaultValue = True
                RecursionSkip.DefaultValue = False
                PsyRD.DefaultValue = 4
                PsyRDOQ.DefaultValue = 10
                SAO.DefaultValue = False
            Case 4 '"fastdecode"
                Deblock.DefaultValue = False
                SAO.DefaultValue = False
                Weightp.DefaultValue = False
                Weightb.DefaultValue = False
                BIntra.DefaultValue = False
            Case 5 '"zerolatency"
                BAdapt.DefaultValue = 0
                BFrames.DefaultValue = 0
                RCLookahead.DefaultValue = 0
                Scenecut.DefaultValue = 0
                CUtree.DefaultValue = False
                FrameThreads.DefaultValue = 1
        End Select
    End Sub

    Public Overrides Function GetPackage() As Package
        Return Package.x265
    End Function
End Class

Public Enum x265RateMode
    SingleBitrate
    SingleQuant
    SingleCRF
    TwoPass
    ThreePass
End Enum
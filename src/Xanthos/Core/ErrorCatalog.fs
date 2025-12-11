namespace Xanthos.Core

open System
open System.Collections.Generic

/// Auto-generated from design/specs/error_codes.md; do not edit by hand.
type JvErrorCategory =
    | Input
    | Authentication
    | Maintenance
    | Download
    | Internal
    | State
    | Other

type JvErrorOverride =
    { Category: JvErrorCategory option
      Message: string option
      Documentation: string option }

type JvErrorBase =
    { Code: int
      Category: JvErrorCategory
      Message: string
      Documentation: string }

type JvErrorInfo =
    { Base: JvErrorBase
      Methods: string list
      Overrides: Map<string, JvErrorOverride> }

module ErrorCatalog =
    let entries: JvErrorInfo array =
        [| { Base =
               { Code = -504
                 Category = JvErrorCategory.Maintenance
                 Message = "Service is currently under maintenance."
                 Documentation = "サーバーメンテナンス中サーバーがメンテナンス中です。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -503
                 Category = JvErrorCategory.State
                 Message = "Required file or temporary file was deleted before JVLink could process it."
                 Documentation =
                   "ファイルが見つからないJVOpenからJVRead/JVGetsまでの間に読み出すべきファイルが削除された、または該当ファイルが使用中と思われます。JVOpenからやりなおせば解消しますが、削除された原因を除去する必要があります。" }
             Methods = [ "JVCLOSE"; "JVMVREAD"; "JVOPEN" ]
             Overrides =
               Map.ofList
                   [ ("JVCLOSE",
                      { Category = Some JvErrorCategory.Other
                        Message = Some "Target file was already removed; closure can continue."
                        Documentation =
                          Some "ファイルが見つからない指定されたファイルがみつかりません。他のソフトから削除された可能性があります。ファイルの削除が目的ですから、そのまま処理を続行してください。" }) ] }
           { Base =
               { Code = -502
                 Category = JvErrorCategory.Download
                 Message = "Download failed because of a communication or disk error."
                 Documentation = "ダウンロード失敗(通信エラーやディスクエラーダウンロード処理に失敗しました。エラーの原因を除" }
             Methods = [ "JVCLOSE"; "JVCOURSEFILE"; "JVMVREAD"; "JVOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -501
                 Category = JvErrorCategory.Other
                 Message = "Setup media (CD/DVD) is invalid or missing."
                 Documentation =
                   "セットアップ処理においてスタートキット(CD/DVD-ROM)が無効JRA-VANが提供した正しいスタートキット(CD/DVD-ROM)をセットしていないと思われます。正しいスタートキット(CD/DVD-ROM)をセットしてください。 スタートキット(CD/DVD-ROM)の提供は2022年3月をもちまして終了いたしました。" }
             Methods = [ "JVOPEN"; "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -431
                 Category = JvErrorCategory.Download
                 Message = "Server reported an internal error."
                 Documentation = "サーバーエラー(サーバーアプリケーション内部エラー)" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -421
                 Category = JvErrorCategory.Download
                 Message = "Server returned a malformed response."
                 Documentation = "サーバーエラー(サーバーの応答が不正)" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -413
                 Category = JvErrorCategory.Download
                 Message = "Server returned HTTP 403/other restricted status."
                 Documentation =
                   "サーバーエラー(HTTPステータス403 Forbidden)サーバーエラー(HTTPステータス200,403,404以Data Lab.用サーバーに問題が発生したと思われます。このエラーが続く場合はJRA-VANへご連絡ください。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -412
                 Category = JvErrorCategory.Download
                 Message = "Server returned HTTP 403 Forbidden."
                 Documentation =
                   "サーバーエラー(HTTPステータス403 Forbidden)サーバーエラー(HTTPステータス200,403,404以Data Lab.用サーバーに問題が発生したと思われます。このエラーが続く場合はJRA-VANへご連絡ください。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -411
                 Category = JvErrorCategory.Maintenance
                 Message = "Server returned HTTP 404 or registry contents are invalid."
                 Documentation =
                   "サーバーエラー(HTTPステータス404 NotFound)レジストリが直接変更されたか、Data Lab.用サーバーに問題が発生したと思われます。JRA-VANのメンテナンス中でない場合で、このエラーが続く場合はJRA-VANへご連絡ください。" }
             Methods =
               [ "JVFUKU"
                 "JVFUKUFILE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides =
               Map.ofList
                   [ ("JVFUKU",
                      { Category = None
                        Message = Some "Server returned HTTP 404/Not Found for the requested resource."
                        Documentation =
                          Some
                              "JV-Link内部エラーサーバーエラー(HTTPステータス404 NotFound)JV-Link内部でエラーが発生したと思われます。 JRA-VANへご連絡ください。 レジストリが直接変更されたか、Data Lab.用サーバーに問題が発生したと思われます。JRA-VANのメンテナンス中でない場合で、このエラーが続く場合はJRA-VANへご連絡ください。" })
                     ("JVMVPLAY",
                      { Category = None
                        Message = Some "Server returned HTTP 404/Not Found for the requested movie."
                        Documentation = None })
                     ("JVMVPLAYWITHTYPE",
                      { Category = None
                        Message = Some "Server returned HTTP 404/Not Found for the requested movie."
                        Documentation = None }) ] }
           { Base =
               { Code = -403
                 Category = JvErrorCategory.Download
                 Message = "Downloaded data is corrupted."
                 Documentation = "ダウンロードしたファイルが異常(データ内容)" }
             Methods = [ "JVCLOSE"; "JVOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -402
                 Category = JvErrorCategory.Download
                 Message = "Downloaded file has an invalid size."
                 Documentation = "ダウンロードしたファイルが異常(ファイルサイダウンロード中に何らかの問題が発生しファイルが異" }
             Methods = [ "JVCLOSE"; "JVMVPLAY"; "JVMVPLAYWITHTYPE"; "JVOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -401
                 Category = JvErrorCategory.Internal
                 Message = "JV-Link reported an internal error."
                 Documentation = "JV-Link内部エラーJV-Link内部でエラーが発生したと思われます。 JRA-VANへご連絡ください。" }
             Methods =
               [ "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides =
               Map.ofList
                   [ ("JVFUKU",
                      { Category = Some JvErrorCategory.Maintenance
                        Message = None
                        Documentation =
                          Some
                              "JV-Link内部エラーサーバーエラー(HTTPステータス404 NotFound)JV-Link内部でエラーが発生したと思われます。 JRA-VANへご連絡ください。 レジストリが直接変更されたか、Data Lab.用サーバーに問題が発生したと思われます。JRA-VANのメンテナンス中でない場合で、このエラーが続く場合はJRA-VANへご連絡ください。" })
                     ("JVFUKUFILE",
                      { Category = Some JvErrorCategory.Maintenance
                        Message = None
                        Documentation =
                          Some
                              "JV-Link内部エラーサーバーエラー(HTTPステータス404 NotFound)JV-Link内部でエラーが発生したと思われます。 JRA-VANへご連絡ください。 レジストリが直接変更されたか、Data Lab.用サーバーに問題が発生したと思われます。JRA-VANのメンテナンス中でない場合で、このエラーが続く場合はJRA-VANへご連絡ください。" }) ] }
           { Base =
               { Code = -305
                 Category = JvErrorCategory.Authentication
                 Message = "User agreement has not been accepted."
                 Documentation = "利用規約に同意していない利用規約に同意していないため、JVOpen処理で蓄積系データを取得することができません。利用規約同意画面にて利用規約を一読し同意してください。" }
             Methods = [ "JVOPEN"; "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -304
                 Category = JvErrorCategory.Authentication
                 Message = "Movie license state is invalid."
                 Documentation =
                   "JRAレーシングビュアー連携機能認証エラーJRAレーシングビュアー連携機能利用申請が行われていないと思われます。 JVMVPlay/JVMVPlayWithTypeを使用するには、利用申請が必要になります。詳細についてはJRA-VANホームページを参照下さい。" }
             Methods = [ "JVMVOPEN"; "JVMVPLAY"; "JVMVPLAYWITHTYPE" ]
             Overrides = Map.empty }
           { Base =
               { Code = -303
                 Category = JvErrorCategory.Authentication
                 Message = "Service key is not configured."
                 Documentation = "利用キーが設定されていない(利用キーが空値)利用キーを設定していないと思われます。JV-Linkインストール直後は利用キーが空なので必ず設定する必要があります。" }
             Methods =
               [ "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVOPEN"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -302
                 Category = JvErrorCategory.Authentication
                 Message = "Service key has expired."
                 Documentation =
                   "利用キーの有効期限切れData Lab.サービスの有効期限が切れています。サービス権の自動延長が停止していると思われます。解消するにはサービス権の再購入が必要です。現在ソフト作者様に配布している利用キーではこのエラーは発生しません。" }
             Methods =
               [ "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVRTOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -301
                 Category = JvErrorCategory.Authentication
                 Message = "Authentication failure (invalid or duplicated service key)."
                 Documentation =
                   "認証エラー利用キーが正しくない。あるいは複数のマシンで同一利用キーを使用した場合に発生します。複数のマシンで同じ利用キーをしようした場合には、このエラーが発生したマシンのJV-Linkをアンインストールし、再インストール後、利用キーの再発行が必要となります。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVREAD" ]
             Overrides = Map.empty }
           { Base =
               { Code = -211
                 Category = JvErrorCategory.Authentication
                 Message = "Registry values are invalid or JVInit has not been executed."
                 Documentation =
                   "JVInitが行なわれていないレジストリ内容が不正(レジストリ内容が不正に変更された)JVFukuFileに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。 JV-Linkはレジストリに値をセットする際に値のチェックを行います(例えば利用キーの桁数など)が、レジストリから値を読み出して使用する際に問題が発生するとこのエラーが発生します。レジストリが直接書き換えられたなどの状況が考えられない場合にはJRA-VANへご連絡ください。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVSETUIPROPERTIES" ]
             Overrides =
               Map.ofList
                   [ ("JVCOURSEFILE",
                      { Category = Some JvErrorCategory.Internal
                        Message = None
                        Documentation = Some "レジストリ内容が不正(レジストリ内容が不正にJV-Linkはレジストリに値をセットする際に値のチェッ" }) ] }
           { Base =
               { Code = -203
                 Category = JvErrorCategory.State
                 Message = "JVOpen was not executed before the current call."
                 Documentation = "JVOpenが行なわれていないJVStatusに先立ってJVOpenが呼ばれていないと思われます。必ずJVOpenを先に呼び出してください。" }
             Methods = [ "JVCLOSE"; "JVMVPLAY"; "JVMVPLAYWITHTYPE"; "JVOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -202
                 Category = JvErrorCategory.State
                 Message = "Previous JVOpen/JVRTOpen/JVMVOpen session is still open."
                 Documentation =
                   "前回のJVOpen/JVRTOpen/JVMVOpenに対してJVCloseが呼ばれていない(オープン中)前回呼び出したJVOpen/JVRTOpen/JVMVOpenがJVCloseによってクローズされていないと思われます。JVOpen/JVRTOpen/JVMVOpenを呼び出した後は次に呼び出すまでの間にJVCloseを必ず呼び出してください。" }
             Methods =
               [ "JVCLOSE"
                 "JVCOURSEFILE"
                 "JVFUKU"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -201
                 Category = JvErrorCategory.Authentication
                 Message = "JVInit was not executed before the current call."
                 Documentation =
                   "JVInitが行なわれていないレジストリ内容が不正(レジストリ内容が不正に変更された)JVFukuFileに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。 JV-Linkはレジストリに値をセットする際に値のチェックを行います(例えば利用キーの桁数など)が、レジストリから値を読み出して使用する際に問題が発生するとこのエラーが発生します。レジストリが直接書き換えられたなどの状況が考えられない場合にはJRA-VANへご連絡ください。" }
             Methods =
               [ "JVCLOSE"
                 "JVCOURSEFILE"
                 "JVCOURSEFILE2"
                 "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVSETUIPROPERTIES" ]
             Overrides =
               Map.ofList
                   [ ("JVCLOSE",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before JVClose."
                        Documentation =
                          Some "JVInitが行なわれていないJVFiledeleteに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVCOURSEFILE",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before requesting course data."
                        Documentation =
                          Some "正常JVInitが行なわれていないJVWatchEventに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVCOURSEFILE2",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before requesting course data."
                        Documentation =
                          Some "正常JVInitが行なわれていないJVWatchEventに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVFUKU",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before requesting silks data."
                        Documentation = Some "JVInitが行なわれていないJVMVCheckに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVMVCHECK",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before calling movie APIs."
                        Documentation =
                          Some "JVInitが行なわれていないJVMVPlay/JVMVPlayWithTypeに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVMVCHECKWITHTYPE",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before calling movie APIs."
                        Documentation =
                          Some "JVInitが行なわれていないJVMVPlay/JVMVPlayWithTypeに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVMVOPEN",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before opening movie lists."
                        Documentation =
                          Some "JVInitが行なわれていないJVCourseFileに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVMVPLAY",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before playing movies."
                        Documentation =
                          Some
                              "JVInitが行なわれていないJVMVReadに先立ってJVInit/JVMVOpenが呼ばれていないと思われます。必ずJVInit/JVMVOpenを先に呼び出してください。" })
                     ("JVMVPLAYWITHTYPE",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before playing movies."
                        Documentation =
                          Some
                              "JVInitが行なわれていないJVMVReadに先立ってJVInit/JVMVOpenが呼ばれていないと思われます。必ずJVInit/JVMVOpenを先に呼び出してください。" })
                     ("JVMVREAD",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before reading movie lists."
                        Documentation =
                          Some "JVInitが行なわれていないJVCourseFileに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVOPEN",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before JVOpen."
                        Documentation =
                          Some
                              "JVInitが行なわれていないJVRead/JVGetsに先立ってJVInit/JVOpenが呼ばれていないと思われます。必ずJVInit/JVOpenを先に呼び出してください。" })
                     ("JVSETUIPROPERTIES",
                      { Category = Some JvErrorCategory.State
                        Message = Some "JVInit must be executed before configuring UI settings."
                        Documentation =
                          Some "JVInitが行なわれていないJVOpen/JVRTOpenに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" }) ] }
           { Base =
               { Code = -118
                 Category = JvErrorCategory.Input
                 Message = "File path parameter is invalid or the directory does not exist."
                 Documentation =
                   "filepathパラメータが不正filepathパラメータの渡し方かパラメータの内容に問題があるか、または、filepathパラメータで設定されたフォルダが存在していません。正しくパラメータがJV-Linkに渡っているか確認してください。" }
             Methods = [ "JVCOURSEFILE"; "JVFILEDELETE"; "JVINIT" ]
             Overrides = Map.empty }
           { Base =
               { Code = -116
                 Category = JvErrorCategory.Input
                 Message = "Option and dataspec combination is invalid."
                 Documentation = "optionパラメータが不正dataspecとoptionの組み合わせが不正" }
             Methods = [ "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -115
                 Category = JvErrorCategory.Input
                 Message = "Option parameter is invalid."
                 Documentation = "optionパラメータが不正dataspecとoptionの組み合わせが不正" }
             Methods = [ "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -114
                 Category = JvErrorCategory.Input
                 Message = "Key parameter is invalid."
                 Documentation = "keyパラメータが不正。の渡し方かパラメータの内容に問題があると思われます。サンプルプログラム等を参照し、正しくパラメータがJV-Linkに渡っているか確認してください。" }
             Methods =
               [ "JVCOURSEFILE"
                 "JVFUKU"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -113
                 Category = JvErrorCategory.Input
                 Message = "Fromtime (end) parameter is invalid."
                 Documentation =
                   "fromtimeパラメータが不正(読み出し終了ポイント時刻不正)パラメータ(読み出し終了ポイント時刻)の渡し方かパラメータ(読み出し終了ポイント時刻)の内容に問題があると思われます。サンプルプログラム等を参照し、正しくパラメータがJV-Linkに渡っているか確認してください。" }
             Methods = [ "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -112
                 Category = JvErrorCategory.Input
                 Message = "Fromtime (start) parameter is invalid."
                 Documentation =
                   "fromtimeパラメータが不正(読み出し開始ポイント時刻不正)パラメータ(読み出し開始ポイント時刻)の渡し方かパラメータ(読み出し開始ポイント時刻)の内容に問題があると思われます。サンプルプログラム等を参照し、正しくパラメータがJV-Linkに渡っているか確認してください。" }
             Methods = [ "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -111
                 Category = JvErrorCategory.Input
                 Message = "Dataspec parameter is invalid."
                 Documentation =
                   "dataspecパラメータが不正。の渡し方かパラメータの内容に問題があると思われます。サンプルプログラム等を参照し、正しくパラメータがJV-Linkに渡っているか確認してください。" }
             Methods =
               [ "JVFUKU"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -103
                 Category = JvErrorCategory.Input
                 Message = "SID begins with a space."
                 Documentation = "sidが不正(sidの1桁目がスペース)sidパラメータの内容に問題があると思われます。 sidの1桁目は必ずスペース以外である必要があります。" }
             Methods = [ "GENERAL" ]
             Overrides = Map.empty }
           { Base =
               { Code = -102
                 Category = JvErrorCategory.Input
                 Message = "SID exceeds 64 bytes."
                 Documentation = "sidが64byteを超えているsidパラメータの渡し方に問題があるか、渡した内容に問題があると思われます。64byte以内の正しいsidを設定してください。" }
             Methods = [ "GENERAL" ]
             Overrides = Map.empty }
           { Base =
               { Code = -101
                 Category = JvErrorCategory.Input
                 Message = "SID is missing."
                 Documentation = "パラメータが不正あるいはレジストリへの保存に失敗。利用キーが登録されている" }
             Methods = [ "GENERAL"; "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -100
                 Category = JvErrorCategory.Input
                 Message = "UI configuration was cancelled or could not be persisted."
                 Documentation = "パラメータが不正あるいはレジストリへの保存に失敗。利用キーが登録されている" }
             Methods = [ "GENERAL"; "JVMVCHECK"; "JVMVCHECKWITHTYPE"; "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -3
                 Category = JvErrorCategory.Download
                 Message = "Target files are still downloading."
                 Documentation = "ファイルダウンロード中読み出そうとするファイルがダウンロードの最中です。少し待ってから読み込みを再開してください。" }
             Methods = [ "JVCLOSE"; "JVMVPLAY"; "JVMVPLAYWITHTYPE"; "JVOPEN" ]
             Overrides = Map.empty }
           { Base =
               { Code = -2
                 Category = JvErrorCategory.Other
                 Message = "Setup dialog was cancelled by the user."
                 Documentation =
                   "セットアップダイアログでキャンセルが押されたセットアップ用データの取り込み時にユーザーがダイアログでキャンセルを押しました。JVCloseを呼び出して取り込み処理を終了してください。" }
             Methods = [ "JVSETUIPROPERTIES" ]
             Overrides = Map.empty }
           { Base =
               { Code = -1
                 Category = JvErrorCategory.Input
                 Message = "No matching data exists for the current parameters."
                 Documentation = "正常該当データ無しpatternパラメータから、勝負服画像を作成することができませんでした。(No Image画像を出力しました。)" }
             Methods =
               [ "JVCLOSE"
                 "JVCOURSEFILE"
                 "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVSETUIPROPERTIES" ]
             Overrides =
               Map.ofList
                   [ ("JVCLOSE",
                      { Category = Some JvErrorCategory.Other
                        Message = Some "File boundary reached; continue with the next file."
                        Documentation =
                          Some "ファイル切り替わりエラーではありません。物理ファイルの終わりを示しています。バッファーにはデータが返されませんのでそのまま読み込み処理を続行してください。" })
                     ("JVOPEN",
                      { Category = Some JvErrorCategory.Other
                        Message = Some "File boundary reached; continue reading."
                        Documentation =
                          Some "ファイル切り替わりエラーではありません。物理ファイルの終わりを示しています。バッファーにはデータが返されませんのでそのまま読み込み処理を続行してください。" }) ] }
           { Base =
               { Code = 0
                 Category = JvErrorCategory.Input
                 Message = "Operation completed successfully."
                 Documentation = "正常該当データ無しpatternパラメータから、勝負服画像を作成することができませんでした。(No Image画像を出力しました。)" }
             Methods =
               [ "GENERAL"
                 "JVCLOSE"
                 "JVCOURSEFILE"
                 "JVCOURSEFILE2"
                 "JVFILEDELETE"
                 "JVFUKU"
                 "JVFUKUFILE"
                 "JVGETS"
                 "JVINIT"
                 "JVMVCHECK"
                 "JVMVCHECKWITHTYPE"
                 "JVMVOPEN"
                 "JVMVPLAY"
                 "JVMVPLAYWITHTYPE"
                 "JVMVREAD"
                 "JVOPEN"
                 "JVREAD"
                 "JVSETUIPROPERTIES"
                 "JVWATCHEVENT" ]
             Overrides =
               Map.ofList
                   [ ("GENERAL",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation =
                          Some
                              "正常(キャンセルボタンが押された場合を含む)パラメータが不正あるいはレジストリへの保存に失敗。SetUIPropertiesの内部的なエラーが発生したと思われます。ユーザーがダイアログ内で指定した内容に問題がある場合はダイアログは閉じません。正しく動作した場合かキャンセルボタンをおされた場合だけダイアログが終了します。また、レジストリへのアクセス権限の問題で設定内容のレジストリへの反映に失敗した場合もこのエラーとなります。レジストリのアクセス権限に問題が無い場合はJRA-VANへご連絡ください。" })
                     ("JVCLOSE",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVCOURSEFILE",
                      { Category = Some JvErrorCategory.State
                        Message = None
                        Documentation =
                          Some "正常JVInitが行なわれていないJVWatchEventに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVCOURSEFILE2",
                      { Category = Some JvErrorCategory.State
                        Message = None
                        Documentation =
                          Some "正常JVInitが行なわれていないJVWatchEventに先立ってJVInitが呼ばれていないと思われます。必ずJVInitを先に呼び出してください。" })
                     ("JVFUKU",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常(公開動画なし)" })
                     ("JVGETS",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVINIT",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVMVCHECK",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVMVCHECKWITHTYPE",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVMVOPEN",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVMVPLAY",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常(バッファにセットしたデータのサイズ)" })
                     ("JVMVPLAYWITHTYPE",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常(バッファにセットしたデータのサイズ)" })
                     ("JVMVREAD",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVOPEN",
                      { Category = Some JvErrorCategory.State
                        Message = Some "All files processed successfully."
                        Documentation =
                          Some
                              "常な状態になったと思われます。JVFiledeleteで該当ファイル(JVRead/JVGetsから戻されたファイル名)を削除し、再度JVOpenからの処理をやりなおしてください。" })
                     ("JVREAD",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" })
                     ("JVSETUIPROPERTIES",
                      { Category = Some JvErrorCategory.Authentication
                        Message = Some "Settings saved successfully."
                        Documentation =
                          Some
                              "正常該当データ無し指定されたパラメータに合致する新しいデータがサーバーに存在しない。 又は、最新バージョンが公開され、ユーザーが最新バージョンのダウンロードを選択しました。JVCloseを呼び出して取り込み処理を終了してください。" })
                     ("JVWATCHEVENT",
                      { Category = Some JvErrorCategory.Other
                        Message = None
                        Documentation = Some "正常" }) ] }
           { Base =
               { Code = 1
                 Category = JvErrorCategory.Other
                 Message = "Victory silks image was created successfully."
                 Documentation = "正常(公開動画あり)" }
             Methods = [ "JVFUKU" ]
             Overrides = Map.empty } |]

    let private index =
        entries
        |> Array.collect (fun info ->
            info.Methods
            |> List.toArray
            |> Array.map (fun m -> ((m.ToUpperInvariant(), info.Base.Code), info)))
        |> dict

    let tryFind methodName code =
        let normalizedName =
            if String.IsNullOrWhiteSpace methodName then
                "(UNKNOWN)"
            else
                methodName

        let key = (normalizedName.ToUpperInvariant(), code)

        match index.TryGetValue key with
        | true, info -> Some info
        | _ -> entries |> Array.tryFind (fun info -> info.Base.Code = code)

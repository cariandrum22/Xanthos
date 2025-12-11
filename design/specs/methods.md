# JV-Link Method Reference

Source: `JV-Link4901` specification (v4.9.0.1).

> Note: The Xanthos library preserves certain JV-Link raw return codes for advanced control logic:
> - `JVGets`: returns `-1` (file boundary) and `-3` (download pending) unchanged so callers can distinguish boundary transitions and implement retry/backoff. Other negative values are interpreted into `ComError`. Implementation: uses `InvokeMember` to extract bytes from the SAFEARRAY returned by COM, then decodes Shift-JIS to string. The return code indicates actual byte count and is used to trim the buffer (avoiding garbage from COM array reuse). JVGets avoids JV-Link's internal SJIS→Unicode conversion overhead that JVRead incurs.
> - `JVRead`: returns `FileBoundary` (`-1`) and `EndOfStream` (`0`) as discriminated union cases.
> This behaviour matches official semantics and is documented here for contributors extending streaming/retry flows.

## JVMVCheck / JVMVCheckWithType

- **JVMVCheck** — See shared details below.
- **JVMVCheckWithType** — JRA レーシングビュアー 映像公開チェック要求

### Parameters
movietype
再生を行う映像の種類を指定します。
JVMVCheck はJVMVCheckWithType の movietype に“00”を指定したものと同等です。
key
レース映像の公開状況をチェックするレースを指定します。パラメータは、以下のように指定します。
種類 | movietype | 指定するキー(searchkey) | 説明
レース映像 | “00” | “YYYYMMDDJJRR”または “YYYYMMDDJJKKHHRR” | YYYY:開催年MM  :開催月DD  :開催日JJ  :場コードKK  :回次HH  :日次RR  :レース番号
パドック映像 | “01” | “YYYYMMDDJJRR”または “YYYYMMDDJJKKHHRR” | YYYY:開催年MM  :開催月DD  :開催日JJ  :場コードKK  :回次HH  :日次RR  :レース番号
マルチカメラ映像 | “02” | “YYYYMMDDJJRR”または “YYYYMMDDJJKKHHRR” | YYYY:開催年MM  :開催月DD  :開催日JJ  :場コードKK  :回次HH  :日次RR  :レース番号
パトロール映像 | “03” | “YYYYMMDDJJRR”または “YYYYMMDDJJKKHHRR” | YYYY:開催年MM  :開催月DD  :開催日JJ  :場コードKK  :回次HH  :日次RR  :レース番号
種類
movietype
指定するキー(searchkey)
説明
レース映像
“00”
“YYYYMMDDJJRR”
または “YYYYMMDDJJKKHHRR”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
パドック映像
“01”
“YYYYMMDDJJRR”
または “YYYYMMDDJJKKHHRR”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
マルチカメラ映像
“02”
“YYYYMMDDJJRR”
または “YYYYMMDDJJKKHHRR”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
パトロール映像
“03”
“YYYYMMDDJJRR”
または “YYYYMMDDJJKKHHRR”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号

### Syntax
```text
Long JVMVCheck( String 型 key );
Long JVMVCheckWithType( String 型 movietype , String 型 key );
```

### Return Value
動画公開チェック要求が正しく終了した場合、公開状況を0または1で返ります。 公開あり:1
公開なし:0
※:Movietype、:Key で指定したレースが存在しない場合は-1が返ります。
エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)
※JVOpen/JVRTOpen/JVMVOpen 中は本メソッドを使用できません。オープン中の場合、先に JVClose を呼び出してから使用してください。
※1度公開されたパドック動画のレースが中止になった場合、該当レースについて本メソッドを使用すると公開なし:0が返ります。

## JVMVPlay / JVMVPlayWithType

- **JVMVPlay** — See shared details below.
- **JVMVPlayWithType** — JRA レーシングビュアー映像再生要求

### Explanation
key で指定したレース映像の再生を行います。具体的には以下の処理を行います。
・JRA レーシングビュアー連携機能が利用可能なソフトウェアであるかの認証を行います。
・指定した映像が公開されている場合、映像の再生を行います。
映像の再生は HTML5 Player (MP4 形式の場合)を利用して行います。  再生するブラウザは JV-Link 設定画面の動画再生ブラウザにて設定した
「Microsoft Edge」「Google Chrome」「IE」のいずれかになります。
レース映像については、JVMVCheck メソッドもしくは JVMVCheckWithType メソッドを利用する事で、レース映像の公開状況を判別する事が可能となります。JVMVCheck もしくは JVMVCheckWithType と組み合わせてご利用下さい。
パドック映像、マルチカメラ映像、パトロール映像については、JVMVCheckWithType  メソッドを利用する事で、パドック動画、マルチカメラ動画、パトロール動画の公開状況  を判別する事が可能となります。  JVMVCheckWithType  と組み合わせてご利用下さい。
※1度公開されたパドック動画のレースが中止になった場合、該当のレースについて本メソッドを使 用すると該当データ無し:-1が返ります。
調教映像については、JVMVOpen で対象映像を指定し、JVMVRead で公開中の調教映像の keyが取得可能です。取得した key を JVMVPlayWithType に設定して下さい。また、movietype については”11”、”12”、”13”どれを指定しても再生される映像は同じです。
※JVOpen/JVRTOpen/JVMVOpen 中は本メソッドを使用できません。オープン中の場合、先に JVClose を呼び出してから使用してください。

### Syntax
```text
Long JVMVPlay( String 型 key);
Long JVMVPlayWithType( String 型 movietype , String 型 key );
```

### Parameters
movietype
再生を行う映像の種類を指定します。
JVMVPlay はJVMVPlayWithType のmovietype に“00”を指定したものと同等です。
key
再生するレース映像を指定します。
JVMVPlayWithType では、movietype の指定によりパラメータの設定内容が異なります。
種類 | movietype | 指定するキー(key) | 説明
レース映像 | “00” | “YYYYMMDDJJKKHHRRTT” | YYYY:開催年
|  | または | MM  :開催月
|  | “YYYYMMDDJJRRTT” | DD  :開催日
|  | または | JJ  :場コード
|  | “YYYYMMDDJJKKHHRR” | KK  :回次
|  | または | HH  :日次
|  | “YYYYMMDDJJRR” | RR  :レース番号
|  |  | TT  :動画種別
パドック映像 | “01” | “YYYYMMDDJJKKHHRR” | YYYY:開催年
|  | または | MM  :開催月
|  | “YYYYMMDDJJRR” | DD  :開催日
|  |  | JJ  :場コード
|  |  | KK  :回次
|  |  | HH  :日次
|  |  | RR  :レース番号
マルチカメラ映像 | “02” | “YYYYMMDDJJKKHHRR” | YYYY:開催年
|  | または | MM  :開催月
|  | “YYYYMMDDJJRR” | DD  :開催日
|  |  | JJ  :場コード
|  |  | KK  :回次
|  |  | HH  :日次
|  |  | RR  :レース番号
パトロール映像 | “03” | “YYYYMMDDJJKKHHRR” | YYYY:開催年
|  | または | MM  :開催月
|  | “YYYYMMDDJJRR” | DD  :開催日
|  |  | JJ  :場コード
|  |  | KK  :回次
|  |  | HH  :日次
|  |  | RR  :レース番号
種類
movietype
指定するキー(key)
説明
レース映像
“00”
“YYYYMMDDJJKKHHRRTT”
YYYY:開催年
または
MM  :開催月
“YYYYMMDDJJRRTT”
DD  :開催日
または
JJ  :場コード
“YYYYMMDDJJKKHHRR”
KK  :回次
または
HH  :日次
“YYYYMMDDJJRR”
RR  :レース番号
TT  :動画種別
パドック映像
“01”
“YYYYMMDDJJKKHHRR”
YYYY:開催年
または
MM  :開催月
“YYYYMMDDJJRR”
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
マルチカメラ映像
“02”
“YYYYMMDDJJKKHHRR”
YYYY:開催年
または
MM  :開催月
“YYYYMMDDJJRR”
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
パトロール映像
“03”
“YYYYMMDDJJKKHHRR”
YYYY:開催年
または
MM  :開催月
“YYYYMMDDJJRR”
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
調教映像 | “11” | “YYYYMMDDNNNNNNNNNN” | YYYY:調教実施年
| または |  | MM  :調教実施月
| “12” |  | DD  :調教実施日
| または |  | NNNNNNNNNN:血統登録番号
| “13” |  |
調教映像
“11”
“YYYYMMDDNNNNNNNNNN”
YYYY:調教実施年
または
MM  :調教実施月
“12”
DD  :調教実施日
または
NNNNNNNNNN:血統登録番号
“13”
WMV 形式の動画の場合のみ、TTには以下のオプションが指定可能です。
MP4 形式の動画の場合にTT を指定しても再生は可能ですが、画質は常に一定となります。
(MP4 形式の場合、TT 以降はJV-Link では認識されません)
TT=01 | 高解像度版を優先して再生
TT=02 | 通常版を優先して再生
指定なし | 高解像度版を優先して再生
上記以外 | エラー
TT=01
高解像度版を優先して再生
TT=02
通常版を優先して再生
指定なし
高解像度版を優先して再生
上記以外
エラー

### Notes
当メソッドを利用するためには、JRA レーシングビュアー連携機能利用申請が必要になります。 (未申請の場合、戻り値に-304 が返されます。)
詳細については JRA-VANホームページのソフト作者サポートページを参照ください。
※当メソッドを使用した開発を行う際には、JVInit メソッドにてソフトウェア ID に "SA000000/SD000004"をセットしていただくことで当メソッドを利用可能となります。
パトロール映像については、通常、各レース終了からおよそ 40 分後に更新されます。マルチカメラ映像については、通常、レース終了後の翌月曜日の午後に更新されます。

### Return Value
映像再生要求が正しく終了した場合、0が返ります。 エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)

## JVWatchEvent / JVWatchEventClose

- **JVWatchEvent** — イベント通知開始
- **JVWatchEventClose** — イベント通知終了

### Explanation
確定・変更情報が発生した際、イベントを通知するスレッドを開始します。 JVInit を行わずに JVWatchEventメソッドを呼び出すとエラーが返ります。

### Sample Usage
‘WithEvents 付きでインターフェイスを宣言
Friend WithEvents InterfaceJVLink As JVDTLabLib.JVLink
Dim ReturnCode As Long                    ‘JV-Link 返値 InterfaceJVLink = New JVDTLabLib.JVLink ‘オブジェクトインスタンスを作成 ReturnCode = InterfaceJVLink.JVWatchEvent()    ‘イベント通知スレッド開始
※以下のコードはVisualBasic6 での使用例となります。
‘上記の手順を踏みJVWatchEvent メソッドを行うことにより、
Private Sub InterfaceJVLink _JVEvtPay(ByVal bstr As String)
Handles InterfaceJVLink JVEvtPay
‘払戻確定イベントが発生した際に行いたい処理をここに記述
ReturnCode = frmMain.JVLink1.JVRTOpen("0B12", bstr) End Sub
‘下記のようなイベント通知を受信するメソッドを使用することが出来るようになります。 ‘払戻確定イベントが発生した際の例を下記に記述します。
‘イベントが発生した際の処理を上記メソッドの中に記述してください。
‘第一引数のbstr をkey として JVRTOpen※1 に渡すことで対象リアルタイム系データを
‘取得することが出来ます。
※1:各イベントから返されるパラメータを key に JVRTOpen を使用する場合 イベント通知を受信するメソッドから返されるパラメータを key としてJVRTOpen を 使用する場合は、Dataspec を以下のように指定してください。
種類 | Dataspec
払戻確定 | 0B12
騎手変更 | 0B16
天候馬場状態変更 | 0B16
コース変更 | 0B16
出走取消・競走除外 | 0B16
発走時刻変更 | 0B16
馬体重発表 | 0B11
種類
Dataspec
払戻確定
0B12
騎手変更
0B16
天候馬場状態変更
0B16
コース変更
0B16
出走取消・競走除外
0B16
発走時刻変更
0B16
馬体重発表
0B11

### Syntax
```text
Long JVWatchEvent();
【パラメータ】なし
```

### Event Overview
イベントを受理するためのメソッドは下記の通りになります。

### Parameters
bstr
JVRTOpen に渡すためのパラメータが返されます。 確定・変更イベントから返されるパラメータは以下のようになります。
イベントメソッド名 | パラメータ | 説明
JVEvtPay | “YYYYMMDDJJRR” | YYYY:開催年
JVEvtWeight |  | MM  :開催月
|  | DD  :開催日
|  | JJ  :場コード
|  | RR  :レース番号
JVEvtJockeyChange | “TTYYYYMMDDJJRRNNNNNNNNNNNNNN” | TT  :レコード種別 ID
JVEvtWeather |  | YYYY:開催年
JVEvtCourseChange |  | MM  :開催月DD  :開催日
JVEvtAvoid
| JJ  :場コードRR  :レース番号
JVEvtTimeChange
|  | NNNNNNNNNNNNNN:送信年月日
イベントメソッド名
パラメータ
説明
JVEvtPay
“YYYYMMDDJJRR”
YYYY:開催年
JVEvtWeight
MM  :開催月
DD  :開催日
JJ  :場コード
RR  :レース番号
JVEvtJockeyChange
“TTYYYYMMDDJJRRNNNNNNNNNNNNNN”
TT  :レコード種別 ID
JVEvtWeather
YYYY:開催年
JVEvtCourseChange
MM  :開催月
DD  :開催日
JVEvtAvoid
JJ  :場コード
RR  :レース番号
JVEvtTimeChange
NNNNNNNNNNNNNN:送信年月日

### Return Value
処理が正しく終了した場合はコード 0 を返します。 エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)

### Event Callback Signature
```text
Void 各イベントメソッド名(String 型 bstr);
受信可能な確定・変更イベントの種類は以下のようになります。
種類 | イベントメソッド名 | 説明
払戻確定 | JVEvtPay | 払戻確定が発表された際イベントを受理します。
騎手変更 | JVEvtJockeyChange | 騎手変更が発表された際イベントを受理します。
天候馬場状態変更 | JVEvtWeather | 天候馬場状態変更が発表された際イベントを受理します。
コース変更 | JVEvtCourseChange | コース変更が発表された際イベントを受理します。
出走取消・競走除外 | JVEvtAvoid | 出走取消・競走除外が発表された際イベントを受理します。
発走時刻変更 | JVEvtTimeChange | 発走時刻変更が発表された際イベントを受理します。
馬体重発表 | JVEvtWeight | 馬体重が発表された際イベントを受理します。
種類
イベントメソッド名
説明
払戻確定
JVEvtPay
払戻確定が発表された際イベントを受理します。
騎手変更
JVEvtJockeyChange
騎手変更が発表された際イベントを受理します。
天候馬場状態変更
JVEvtWeather
天候馬場状態変更が発表された際イベントを受理します。
コース変更
JVEvtCourseChange
コース変更が発表された際イベントを受理します。
出走取消・競走除外
JVEvtAvoid
出走取消・競走除外が発表された際イベントを受理します。
発走時刻変更
JVEvtTimeChange
発走時刻変更が発表された際イベントを受理します。
馬体重発表
JVEvtWeight
馬体重が発表された際イベントを受理します。
```

## JVInit

- **JVInit** — JV-Linkの初期化

### Syntax
```text
Long JVInit ( String 型 sid );
```

### Parameters
sid
呼出元のソフトを識別するソフトウェアIDを最大64バイトの文字列で指定します。ここにセットされる値はJRA-VAN サーバーにアクセスする際に User-Agent HTTP ヘッダーとして使用されます。ソフトウェア  ID は JRA-VAN に登録済みのものである必要があります。デフォルトで "UNKNOWN"が使用可能です。 また、使用可能な文字は、半角のみとし、アルファベット、数     字、スペース、アンダースコア(_)、 ピリオド(.)、スラッシュ(/)です。

### Return Value
初期化処理が正しく終了した場合は 0、エラーが発生した場合はエラーの理由コードとして負の値が返ります。(「3.コード表」参照)

### Explanation
JVInit  は  JV-Link の初期化のために必ず最初に呼び出さなければなりません(※)。JVInit  は  JV-Link が使用する変数などのリソースを初期化します。サーバーとの通信は行なわれません。 JVInit  は自身のプロパティをレジストリの内容で初期化しますが、JVSetUIProperties  を呼び出した後に変更されたレジストリの内容を読み込ませるという理由で再度JVInit  を呼び出す必要はありません。JVOpen  あるいは JVRTOpen により変更されたレジストリ内容は再度読み込まれます。
※アプリケーションの初期化時に呼び出しを行ってください。JVOpen あるいは JVRTOpen の都度呼び出す必要はありません。

## JVSetUIProperties

- **JVSetUIProperties** — JV-Linkの設定変更(ダイアログ版)

### Syntax
```text
Long JVSetUIProperties( );
【パラメータ】なし
```

### Return Value
プロパティが正しくセットされた場合あるいはキャンセルボタンが押された場合は0、エラー発生により終了した場合は-100が返り、値はレジストリにセットされません。

### Explanation
JV-Link が持つプロパティのうちエンドユーザーによって変更可能な項目について値を変更するためのダイアログ表示と値のセットを行います。
表示されるダイアログは下図の通りです。
JVSetUIProperties で設定可能なプロパティの値は以下のとおりです。(利用キーが設定済の場合は、利用キーは変更不可となります。)
・m_saveflag(保存フラグ)
・m_savepath(保存パス)
・m_servicekey(利用キー)
・m_payflag(払戻ダイアログ表示フラグ)
JVSetUIProperties  メソッドでプロパティを設定するとその値がレジストリに保存され、この値が JV-Link の動作に反映されるのは JVInit,JVOpen,JVRTOpen を呼び出したタイミングです。
JV-Link のインストール直後は利用キーに値が設定されていませんのでJRA-VAN サーバーの認証を受けられません。デフォルト値のまま JVOpen / JVRTOpen メソッドを呼び出すと認証エラーとなります。
m_savepath(保存パス)が存在しないパスであった場合には新規に該当フォルダを作成するかどうか確認するダイアログを表示し、「OK」 ボタンを押すとフォルダを作成します。 JVOpen / JVRTOpen 実行時に該当フォルダが存在しないと「-211」(レジストリ内容が不正)エラーとなります。
「JRA-VAN からのお知らせを表示する」を設定すると、JRA-VANからのお知らせが存在した場合、 JVOpen / JVRTOpen 実行時にお知らせメッセージダイアログが表示されます。 ダイアログ内にある
「確認した」ボタンを押すと再度 JVOpen / JVRTOpen を実行した際には該当の お知らせが表示されなくなります。
「後で確認する」ボタンを押すと、JVOpen の場合は再度 JVOpen を実行した際にお知らせが表示されます。
JVRTOpen の場合は一度競馬ソフトを終了するまで、再度 JVRTOpen を実行してもお知らせは表示されません。
「払戻ダイアログを表示する」を設定すると、払戻ダイアログが表示されます。

## JVSetServiceKey

- **JVSetServiceKey** — JV-Linkの設定変更(利用キー)

### Syntax
```text
Long JVSetServiceKey( String 型 servicekey );
```

### Parameters
servicekey
JRA-VAN サーバーと通信する際に、認証に使用する利用キー(17桁の英数字)を文字列で指定します。

### Return Value
利用キーが正しくセットされた場合は0、  指定された値が不正である場合あるいはエラー発生により終了した場合は-100が返り、値はレジストリにセットされません。(利用キーが設定済の場合は、利用キーは変更不可となります。)

### Explanation
JVSetServiceKey メソッドで利用キーを設定するとその値がレジストリに保存され、それ以降 JVInit,JVOpen,JVRTOpen  を呼び出したタイミングでこのレジストリの値が使用されます。

## JVSetSaveFlag

- **JVSetSaveFlag** — JV-Linkの設定変更(保存フラグ)

### Syntax
```text
Long JVSetSaveFlag( Long 型 saveflag );
```

### Parameters
saveflag
ダウンロードしたファイルを保存パス(m_savepath)に保存するかどうかを数値で指定します。
saveflag = 0 | 保存しない
saveflag = 1 | 保存する
上記以外 | エラー
saveflag = 0
保存しない
saveflag = 1
保存する
上記以外
エラー
「保存する」に設定した場合、データはJRA-VANサーバーで該当データが提供されている間保存されます。

### Return Value
保存フラグが正しくセットされた場合は0、 指定された値が不正である場合あるいはエラー発生により終了した場合は-100が返り、値はレジストリにセットされません。

### Explanation
JVSetSaveFlag メソッドで利用キーを設定するとその値がレジストリに保存され、 それ以降 JVInit,JVOpen,JVRTOpen を呼び出したタイミングでこのレジストリの値が使用されます。

## JVSetSavePath

- **JVSetSavePath** — JV-Linkの設定変更(保存パス)

### Syntax
```text
Long JVSetSavePath( String 型 savepath );
```

### Parameters
savepath
ダウンロードしたファイルを保存するパスを文字列で指定します。デフォルトでは JV-Link のインストールされたパスが設定されています。デフォルトの JV-Linkインストールパスは、OSがインストールされたドライブの”Program Files\JRA-VAN\Data Lab”となります。 引数には実際に存在するパスを指定する必要があります。

### Return Value
保存パスが正しくセットされた場合は0、 指定された値が不正である場合あるいはエラー発生により終了した場合は-100が返り、値はレジストリにセットされません。

### Explanation
JVSetSavePath メソッドで保存パスを設定するとその値がレジストリに保存され、 それ以降 JVInit,JVOpen,JVRTOpen を呼び出したタイミングでこのレジストリの値が使用されます.
実際に  JV-Data  が保存されるのはここで指定されたパスの下に自動的に作成される”cache”と”data”フォルダになります。”cache”と”data”フォルダが存在しない場合には  JVOpen,JVRTOpenが自動的に作成しますが、保存パスそのものが存在しない場合には  JVOpen,JVRTOpen は「-21
1」(レジストリ内容が不正)エラーとなります。したがってこの JVSetSavePath メソッドで設定する保存パスは存在するパスを指定する必要があります。

## JVOpen

- **JVOpen** — 蓄積系データの取得要求

### Syntax
```text
Long JVOpen( String 型 dataspec , String 型 fromtime , Long 型 option ,   Long
型 readcount , Long 型 downloadcount , String 型
lastfiletimestamp );
```

### Parameters
dataspec
読み出したいデータを識別するデータ種別IDを文字列として連結したものを指定します。1つのデータ種別IDは4桁固定ですので dataspec に指定する文字列は4の倍数桁でなければいけません。option パラメータとの組み合わせで指定できないデータ種別 ID があります。
#### 既知の障害について
・dataspec   を複数個指定した場合、個別に指定した場合と比較すると「対象ファイル数が多い場合に  JVRead  の処理時間が遅くなる」という障害が報告されています。処理時間が遅い場合には、   detaspec  を個別に指定、または  Fromtime  に読み出し開始ポイント時刻・終了ポイント時刻を指定して回避してください。また、 セ  ッ トアップデータ取得時は
option=4(ダイアログ無しセットアップ)を指定することで、セットアップ時のダイアログ表示を回避 可能です。
指定可能なデータ種別IDについては「JV-Data仕様書」を参照して下さい。
fromtime
指定方法は以下の 2 通り。
・読み出し開始ポイント時刻のみを指定する場合
dataspec に指定したデータの読み出し開始ポイントを時刻(YYYYMMDDhhmmss の形式)で指定します。ここで指定した時刻より大きくかつ現在時刻までに提供されたデータが読み出しの対象となります。
例) fromtime
YYYYMMDDhhmmss
・読み出し開始ポイント時刻、および読み出し終了ポイント時刻を指定する場合
dataspec  に指定したデータの読み出し開始ポイント(YYYYMMDDhhmmss  の形式)と読み出し終了ポイント時刻(YYYYMMDDhhmmss  の形式)を「-(半角ハイフン)」で結合し指定します。ここで指定した開始時刻より大きくかつ指定した終了時刻までに提供されたデータが読み出しの対象となります。
例) fromtime
YYYYMMDDhhmmss-YYYYMMDDhhmmss
なお、dataspec にて以下のデータ種別 ID を指定する場合、全データを取得するため、読み出し終了ポイント時刻を指定することができません。指定した場合、「戻り値:-1(該当データなし)」が出力されます。
・TOKU(特別登録馬情報)
・DIFF、DIFN(蓄積系ソフト用 蓄積情報)
・HOSE、HOSN(競走馬市場取引価格情報)
・HOYU(馬名の意味由来情報)
・COMM(各種解説情報)
option
option = 1 | 通常データ
option = 2 | 今週データ
option = 3 | セットアップデータ
option = 4 | ダイアログ無しセットアップデータ(初回のみダイアログを表示します。)
上記以外 | エラー
option = 1
通常データ
option = 2
今週データ
option = 3
セットアップデータ
option = 4
ダイアログ無しセットアップデータ
(初回のみダイアログを表示します。)
上記以外
エラー
蓄積系ソフトがデータをメンテナンスする際の差分データの読み出しの場合は1を指定します。非蓄積系ソフトが今週ぶんのデータのみを読み出したい場合には2を指定します。2を指定すると直近の未来のレースに関するデータ(出走予定馬の過去走情報を含む)と直前のレースの成績関連のデータに該当するデータだけを読み出します。1を指定した場合は全てのデータの中から dataspec,fromtime に該当するデータが読み出されます。 また、蓄積系ソフトがセットアップを行なう場合は3または4を指定します。3または4を指定した場合はセットアップ専用のデータを JRA-VAN サーバー から取得し読み出します。 セットアップを分割して行いたい場合(データ種別毎にセットアップを行う場合等)には、4を指定 します。4を指定した場合は、セットアップデータの取得元を指定するダイアログが初回のみ表示され2回目以降は初回に指定した取得元にてセットアップが行われます。
readcount
dataspec,fromtime,option で指定した条件に該当する全ファイル数が返されます。呼び出し時に値をセットする必要はありません。
downloadcount
readcount で返された数のうちダウンロードが必要なファイルの数が返されます。
lastfiletimestamp
dataspec,fromtime,option で指定した条件に該当する全ファイルのうち最も新しいファイルのタイムスタンプが YYYYMMDDhhmmss の形式で返されます。このタイムスタンプは次に JVOpen を呼び出す場合に fromtime として指定するために必要ですのでレジストリやファイルまたはデータベースなどに保存しておく必要があります。
JVRead/JVGetsの中断・再開
・通常データの中断・再開
最後に読込んだファイルの m_CurrentFileTimestamp を保持し、再開時の JVOpen の fromtime に、保持している m_CurrentFileTimestamp を設定することで JVRead/JVGetsを再開することができます。(dataspec を個別に指定した場合は dataspec 毎に保持を行う。)
・セットアップデータの中断・再開
最後に読込んだファイル名を保持し、再開時には前回と同じパラメータにて JVOpen を行い、保持しているファイル名まで JVSkip を行う事で JVRead/JVGets を再開することができます。 (dataspec を個別に指定した場合は dataspec毎に保持を行う。)

### Return Value
オープン処理が正しく終了した場合、0 が返されます。エラーが発生した場合にはエラーの理由コードとして負の数が返されます。(「3.コード表」参照)

### Explanation
データ識別文字列の組み合わせと開始ポイント時刻(fromtime)で指定したデータ(ファイル群)を読み込むための準備をします。具体的には以下の処理を行います。
・dataspec,fromtime,option が正しいか検査を行います。
・dataspec,fromtime,option に該当するファイルのリストをサーバに問い合わせます。
・該当するファイルがローカルディスクに存在するかどうか検査します。
・ローカルディスクに無いデータをサーバーからダウンロードするスレッドを開始します。
・ダウンロードスレッドが開始したら処理をアプリケーションに返します。 option パラメータの指定により JV-Link は以下のデータを返します。
Option | 結果
1 | サーバーが提供している全てのデータの中から dataspec が合致し、fromtime より大きくかつ現在時刻、またはfromtime にて「-」(半角ハイフン)以降で指定した終了時刻までに該当するデータを取得します。
2 | 先週のレース結果情報と次週のレース関連情報を含んだ約1週間ぶんのデータの中からdataspec が合致し、fromtime より大きい時刻に該当するデータ、または fromtime より大きくかつ fromtime にて「-」(半角ハイフン)以降で指定した終了時刻に該当するデータを取得します。
3, 4 | セットアップ用データの中からdataspec が合致するfromtime より大きい時刻を持つ全てのデータ(前月までのデータ)と、今月の通常データの中で dataspec が合致する現在時刻、または fromtime にて「-」(半角ハイフン)以降で指定した終了時刻までに提 供しているデータを取得します。(※1)
Option
結果
1
サーバーが提供している全てのデータの中から dataspec が合致し、fromtime より大きくかつ現
在時刻、またはfromtime にて「-」(半角ハイフン)以降で指定した終了時刻までに該当するデータを取得します。
2
先週のレース結果情報と次週のレース関連情報を含んだ約1週間ぶんのデータの中から
dataspec が合致し、fromtime より大きい時刻に該当するデータ、または fromtime より大きくかつ fromtime にて「-」(半角ハイフン)以降で指定した終了時刻に該当するデータを取得します。
3, 4
セットアップ用データの中からdataspec が合致するfromtime より大きい時刻を持つ全てのデー
タ(前月までのデータ)と、今月の通常データの中で dataspec が合致する現在時刻、または fromtime にて「-」(半角ハイフン)以降で指定した終了時刻までに提 供しているデータを取得します。(※1)
以下の option パラメータとdataspec の組み合わせにて指定が可能です。
Option | dataspecに指定可能なデータ種別ID
1 | TOKU,RACE,DIFF,BLOD,SNAP,SLOP,WOOD,YSCH,HOSE,HOYU,DIFN,BLDN,SNPN,HOSN
2 | TOKU,RACE,TCOV,RCOV,SNAP,TCVN,RCVN,SNPN
3, 4 | TOKU,RACE,DIFF,BLOD,SNAP,SLOP,WOOD,YSCH,HOSE,HOYU, COMM,MING DIFN,BLDN,SNPN,HOSN
Option
dataspecに指定可能なデータ種別ID
1
TOKU,RACE,DIFF,BLOD,SNAP,SLOP,WOOD,YSCH,HOSE,HOYU,
DIFN,BLDN,SNPN,HOSN
2
TOKU,RACE,TCOV,RCOV,SNAP,TCVN,RCVN,SNPN
3, 4
TOKU,RACE,DIFF,BLOD,SNAP,SLOP,WOOD,YSCH,HOSE,HOYU, COMM,MING DIFN,BLDN,SNPN,HOSN
option に3または4を指定した場合、スタートキット(CD/DVD-ROM)からセットアップするか全てのデータをサーバーからダウンロードするかを選択する次のようなダイアログが表示されます。(optionに4を指定した場合は、初回のみ表示されます。)
JRA-VAN 提供のスタートキット(CD/DVD-ROM)を持っている場合はこのダイアログで指定します。
このダイアログで「スタートキット(CD/DVD-ROM)を持っている」を選択すると指定されたドライブから必要なデータをローカルディスクにコピーし、足りないデータをサーバーからダウンロードします。古いスタートキット(CD/DVD-ROM)を使用した場合でも最大限スタートキット(CD/DVD-ROM)に収容されたデータを利用するように動作し、足りないぶんをサーバーからダウンロードします。
「スタートキット(CD/DVD-ROM)を持っていない」を選択するとセットアップのためのデータを全てサーバーからダウンロードします。スタートキット(CD/DVD-ROM)の提供は   2022   年3月をもちまして終了いたしましたので、こちらを選択してセットアップをお願いいたします。
※1:dataspec=TOKU(特別登録馬)に関する注意事項 セットアップデータ取得時には最新分(当週+翌週)の特別登録馬の取得が可能です。 過去分の取得を行うには、通常データ取得にて fromtime を過去日付に設定し取得を行う必要があ ります。
また、新しいお知らせや新しいバージョンの JV-Link がリリースされていれば、告知ダイアログを表示します(新しいお知らせは、JRA-VAN からのお知らせを表示するになっている場合のみ表示されます)。

## JVRTOpen

- **JVRTOpen** — リアルタイム系データの取得要求

### Syntax
```text
Long JVRTOpen( String 型 dataspec , String 型 key);
```

### Parameters
dataspec
読み出したいデータを識別するデータ種別IDを文字列として指定します。1つのデータ種別IDしか指定できませんので4桁固定となります。
指定可能なデータ種別IDについては「JV-Data仕様書」を参照して下さい。
key
該当データを取得するための要求キーを指定します。 要求するデータの提供単位に応じて以下のように指定します。
提供単位 | 指定するキー(key) | 説明
レース毎 | “YYYYMMDDJJKKHHRR”または “YYYYMMDDJJRR” | YYYY:開催年MM  :開催月DD  :開催日JJ  :場コードKK  :回次HH  :日次RR  :レース番号
開催日単位 | “YYYYMMDD” | YYYY:開催年MM  :開催月DD  :開催日
変更情報単位 | 各イベントから返されるパラメータ※1 | 各イベントから返されるパラメータを指定いただくことにより変更情報単位の取得が可能※1
提供単位
指定するキー(key)
説明
レース毎
“YYYYMMDDJJKKHHRR”
または “YYYYMMDDJJRR”


YYYY:開催年
MM  :開催月
DD  :開催日
JJ  :場コード
KK  :回次
HH  :日次
RR  :レース番号
開催日単位
“YYYYMMDD”
YYYY:開催年
MM  :開催月
DD  :開催日
変更情報単位
各イベントから返されるパラメータ
※1
各イベントから返されるパラメータを指定
いただくことにより変更情報単位の取得が可能※1
イベントに関しては「JVWatchEvent」を参照して下さい。 各データの提供単位については「JV-Data仕様書」を参照して下さい。

### Return Value
オープン処理が正しく終了した場合、0が返ります。エラーが発生した場合にはエラーの理由コードとして負の数が返されます。(「3.コード表」参照)

### Explanation
データ識別文字列で指定したデータを読み込むための準備をします。具体的には以下の処理を行います。
・dataspec の検査を行います。
・dataspec およびkey に対応するデータをサーバにリクエストします。
・データの受信を完了した時点で処理をアプリケーションに返します。
※1:各イベントから返されるパラメータを key に JVRTOpen を使用する場合 イベント通知を受信するメソッドから返されるパラメータを key としてJVRTOpen を 使用する場合は、Dataspec を以下のように指定してください。
種類 | Dataspec
払戻確定 | 0B12
騎手変更 | 0B16
天候馬場状態変更 | 0B16
コース変更 | 0B16
出走取消・競走除外 | 0B16
発走時刻変更 | 0B16
馬体重発表 | 0B11
種類
Dataspec
払戻確定
0B12
騎手変更
0B16
天候馬場状態変更
0B16
コース変更
0B16
出走取消・競走除外
0B16
発走時刻変更
0B16
馬体重発表
0B11
イベントから返されるパラメーターを key にJVRTOpen を使用した場合の提供単位は以下のようになります。
種類 | 提供単位
払戻確定 | レース単位
騎手変更 | 変更情報発表単位(複数件の騎手変更情報を提供する場合もあります)
天候馬場状態変更 | 場単位
コース変更 | レース単位
出走取消・競走除外 | 変更情報発表単位(複数件の出走取消・競走除外情報を提供する場合もあります)
発走時刻変更 | 変更情報発表単位(複数件の発走時刻変更情報を提供する場合もあります)
馬体重発表 | レース単位
種類
提供単位
払戻確定
レース単位
騎手変更
変更情報発表単位(複数件の騎手変更情報を提供する場合もあります)
天候馬場状態変更
場単位
コース変更
レース単位
出走取消・競走除外
変更情報発表単位
(複数件の出走取消・競走除外情報を提供する場合もあります)
発走時刻変更
変更情報発表単位(複数件の発走時刻変更情報を提供する場合もあります)
馬体重発表
レース単位

## JVStatus

- **JVStatus** — ダウンロード進捗情報の取得

### Syntax
```text
Long JVStatus( );
【パラメータ】なし
```

### Return Value
ダウンロード完了ファイル数が Long 型で返されます。ダウンロード処理にエラーが発生した場合は負のエラーコードが返されます。(「3.コード表」参照)

### Explanation
JVOpen をコールした際に起動されたダウンロードスレッドがダウンロードを完了したファイル数を返します。 ファイルのダウンロードが完了すると JVStatus の返す値は JVOpen をコールした際 の downloadcount と一致します。この一致をもってダウンロードの終了を判断してください。以降 JVClose がコールされるまでの 間、JVStatus はこの値を返します。ダウンロード処理の完了を待たず JVRead/JVGets を呼び出すと 予期しないエラーが発生する場合があります。
JVOpen が呼び出されていない、またはdataspec に合致するデータが存在せずダウンロードが行われていない時に呼び出された場合や、エラーが発生した場合は負のエラーコードが返りま す。
JVStatus の戻り値をプログレスバー表示に利用する場合は、ダウンロードすべきファイルが存在しないときに、0 による除算が発生する場合があることに注意してください。

## JVRead

- **JVRead** — JV-Dataの読み込み

### Syntax
```text
Long JVRead( String 型 buff , Long 型 size , String 型 filename );
```

### Parameters
buff
呼出側で用意したデータ格納バッファを指定します。1行単位で読み出しますので改行コード
(0x0d,0x0a)を含めたレコードデータが収容可能なサイズ+1(ストリング終端文字 NULL)を用意します。
size
呼出側で用意したデータ格納バッファにコピーするデータの長さを指定します。この値がレコード長よりも小さい場合には残りのデータは切り捨てられ、データ格納バッファの最後の1バイトがストリング終端文字 NULL となります。
filename
現在読み込み中のファイル名が JVReadから返されます。

### Return Value
正常にレコードを読み込んだ場合はバッファーにセットされたデータのバイト数が返ります。読み込んでいるファイルが次のファイルに切り替わる際には-1が返ります。全てのファイルを読み終わると0が返ります。エラーが発生した場合にはエラーの理由コードとして負の数が返されます。(「3.コード表」参照)
-1の戻り値は実際には物理ファイルの終わりに返されるので、全てのファイルの終わりには一度
-1が返った後、次の呼び出しで0が返されます。

### Explanation
JVOpen / JVRTOpen で準備した JV-Data を現在のファイルポインタから1行分読み出します。 JVOpen / JVRTOpen を行なわずにJVRead メソッドを呼び出すとエラーが返ります。
JVOpen ではデータ種別IDが複数指定できるため、JVRead メソッドは物理的には複数のファイルで
あっても1つのファイルであるかのように連続してデータを読み出します。 ただし、ファイル間をまたぐごとに戻り値としてファイル切り替わり(-1)が返され、全てのファイルを読 み終わった際に戻り値としてEOF(0)が返されます。
例えば「2002 年 11 月 10 日以降現在までの RACE データ」を指定して読み出した場合に、レース詳細データが 36 件存在したとすると1回目から36 回目の呼出しまではレース詳細のレコードが 1 行ずつバッファにセットされ、37 回目の呼び出しでファイル切り替わり(-1)が返されます。38 回目以降の呼出しには馬毎レース情報がセットされます。全てのレコードが無くなった時点で EOF(0)が返されます。

### Notes
JVRead では、渡されたデータ格納バッファは JV-Link 内にて解放し、新たに確保したバッファをbuffに割り当てられます。

## JVGets

- **JVGets** — JV-Dataの読み込み

### Syntax
```text
Long JVGets( Byte Array 型 buff , Long 型 size , String 型filename );
```

### Parameters
buff
データが格納されたBYTE型配列がセットされるポインタを指定します。
size
コピーするデータの長さを指定します。この値がレコード長よりも小さい場合には残りのデータは切り捨てられ、データ格納バッファの最後の1バイトがストリング終端文字 NULL となります。
filename
現在読み込み中のファイル名が JVGets から返されます。

### Return Value
正常にレコードを読み込んだ場合はバッファーにセットされたデータのバイト数が返ります。読み込んでいるファイルが次のファイルに切り替わる際には-1が返ります。全てのファイルを読み終わると0が返ります。エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)
-1の戻り値は実際には物理ファイルの終わりに返されるので、全てのファイルの終わりには一度
-1が返った後、次の呼び出しで0が返されます。

### Explanation
JVOpen / JVRTOpen で準備した JV-Data を現在のファイルポインタから1行分読み出します。 JVOpen / JVRTOpen を行なわずにJVGets メソッドを呼び出すとエラーが返ります。
JVOpen  ではデータ種別IDが複数指定できるため、JVGets  メソッドは物理的には複数のファイルであっても1つのファイルであるかのように連続してデータを読み出します。  ただし、ファイル間をまたぐごとに戻り値としてファイル切り替わり(-1)が返り、全てのファイルを読  み終わった際に戻り値としてEOF(0)が返されます。
例えば「2002 年 11 月 10 日以降現在までの RACE データ」を指定して読み出した場合に、レース詳細データが 36 件存在したとすると1回目から36 回目の呼出しまではレース詳細のレコードが 1 行ずつバッファにセットされ、37 回目の呼び出しでファイル切り替わり(-1)が返されます。38 回目以降の呼出しには馬毎レース情報がセットされます。全てのレコードが無くなった時点で EOF(0)が返されます。
JVGets について
JVGets は従来の JVRead と互換性のあるメソッドとして、Ver2.1.0で新たに追加された公開メソッドです。
従来のJVRead は、内部で渡されたメモリを解放し、SJIS で開いたファイルを UNICODE 変換して新たに確保したメモリエリアに渡す処理をしていることから、パフォーマンス低下の原因となっていました。
JVGets では、メモリ受け渡しをバイト配列型のポインタで行い、そのポインタに対してメモリエリアを確保して渡す方法になります。その際、SJIS は SJIS のまま渡すことにより、JV-Link 内部での変換およびアプリケーション側でのUNICODE→SJIS変換が不要になりコード変換におけるオーバーヘッドがなくなりました。
VB以外の言語では、VARIANT*によってバイト型 SafeArray のポインタを受け取り、Return する際にJV-Link 内部で確保したByte SafeArray をセットして返します。
途中の動作はすべて JVRead と共通しているので、JVGets → JVRead → JVGets というように 交互に呼ばれたとしても、矛盾なくレコードが取得できるように構成されています。
また、JVGets ではメモリの解放を行わないので、アプリケーション側で読み出しの度に解放する必要があります。

###  VisualBasic6 での JVGets 読み出し方法
Dim BuffSize As Long               ‘バッファサイズ
Dim BuffName As String              ‘バッファ名
Dim ReturnCode As Long            ‘JV-Link 返値
Dim bytData() As Byte              ‘JVGets 用Byte型配列ポインタ
ReturnCode = frmMain.JVLink1.JVGets(bytData, BuffSize, BuffName) Debug.Print bytData             ‘データを表示します。
Erase bytData              ‘読み込みで確保されたメモリを明示的に削除します

## JVSkip

- **JVSkip** — JV-Dataの読みとばし

### Syntax
```text
void JVSkip( );
【パラメータ】なし
```

### Return Value
なし

### Explanation
JVOpen で準備した JV-Data を読み込み中に不要なレコード種別を読み飛ばすために使用します。 JVSkip メソッドを呼び出すと現在読み込み中のファイルにレコードが残っていても次のファイルの先頭までファイルポインタを進めます。
JVRead/JVGets メソッドは物理的には複数のファイルであっても1つのファイルであるかのように連続してデータを読み出します。蓄積系データは1つのファイルにレコード種別は1種類しか収容されていません。したがって JVRead/JVGets メソッドで読み出されたレコードが処理不要のレコード種別であった場合に JVSkip メソッドを呼び出し、そのファイルに収容されている残りの処理不要レコードを読み飛ばし処理時間を短縮することができます。速報系データは1回の JVRTOpen に対して1ファイルしか返されないので JVSkip には意味がありません。
例えばデータ種別”DIFN”を dataspec に指定してJVRead/JVGets を行なうと
・レース詳細(“RA”)     ・馬毎レース情報(“SE”)
・競走馬(“UM”)       ・騎手(“KS”)
・調教師(“CH”)       ・生産者(“BR”)
・馬主(“BN”)        ・レコード(“RC”)
の8種類のデータを読み出す可能性がありますが、このうち”BR”と”BN”が不要なデータである場合には次のロジックにより処理時間が短縮されます。

### 不要データ読み飛ばし
ダウンロード完了?
No
JVStatus( )
Yes
JVRead( ) /JVGets( )
Yes
戻り値=0
No
Yes
戻り値=-1
No
No
戻り値>0
エラー
Yes
Yes
レコード種別=”BR” or “BN”


No
レコードに対する処理
JVSkip( )

### 注意点
・JVSkip を複数回連続して呼び出しても1回呼び出した場合と同じ動作をします。
・JVOpen 直後にJVSkip を呼び出すと次の JVRead/JVGets では2ファイル目の先頭レコードを読みます。
・JVRead/JVGets で-1 が返った直後にJVSkip を呼び出すと次のファイルを読み飛ばします。
・JVSkip で読み飛ばしたファイルが最後のファイルである場合は次のJVRead/JVGets で0 が返ります。

## JVCancel

- **JVCancel** — ダウンロードスレッドの停止

### Syntax
```text
void JVCancel( );
【パラメータ】なし
```

### Return Value
なし

### Explanation
JVOpen により起動されたファイル準備処理(ダウンロード)を中止します。
JVOpen を呼び出すとローカルディスクに無いデータは JRA-VAN サーバーからダウンロードを開始します。ダウンロード中はその進捗状況を JVStatus で取得することが可能ですが、このときアプリケーション側からJVCancel を呼び出すことで、これらのファイル準備処理を中断することができます。
JVCancel によって中断した状態で JVRead/JVGets を呼び出すとエラーとなります。 JVCancel の代わりに JVClose を呼び出した場合もファイル準備処理を中断します。

## JVClose

- **JVClose** — JV-Data読み込み処理の終了

### Syntax
```text
Long JVClose( );
【パラメータ】なし
```

### Return Value
サービス終了処理が正しく終了した場合は、0 が返ります。

### Explanation
開いているファイルを全てクローズし、実行中のダウンロードスレッドがあれば中止します。保存パスが示すフォルダから不要なファイルを削除します。

## JVFiledelete

- **JVFiledelete** — ダウンロードしたファイルの削除

### Syntax
```text
Long JVFiledelete( String 型 filename);
```

### Parameters
filename
削除対象のファイル名を指定します。

### Return Value
処理が正しく終了した場合は0、エラーが発生した場合は-1 が返ります。

### Explanation
保存パスが示すフォルダから指定されたファイルを削除します。
保存パスに保存されているファイルの問題により JVRead/JVGets 中にエラーが発生した場合、 JVFiledelete メソッドでファイルを削除してください。削除が成功した後、直前に行なった JVOpen からの処理をやり直してください。

## JVFukuFile

- **JVFukuFile** — 勝負服画像情報要求

### Syntax
```text
Long JVFukuFile(String 型 pattern, String 型 filepath);
```

### Parameters
pattern
勝負服の色・模様を示す服色標示を最大全角30  文字で指定します。服色標示の文字列は、レーシングプログラムに記載されているものとなります。
(パラメータ例) 「水色,赤山形一本輪,水色袖」
注意事項:
・勝負服画像が作成できない場合は、「No Image」画像が出力されます。
filepath
勝負服ファイルの出力ファイル名をフルパスで指定します。実際に存在するフォルダを指定する必要があります。

### Return Value
処理が正しく終了した場合はコード 0 を返します。
エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)

### Explanation
服色標示パラメータより、勝負服画像のファイルを作成します。
作成される画像データは、サイズ50pix×50pix のビットマップ形式(24 ビット)となります。形式・サイズについては、必要に応じて競馬ソフト側で変換してご使用ください。

## JVFuku

- **JVFuku** — 勝負服画像情報要求(バイナリ)

### Syntax
```text
Long JVFuku ( String 型 pattern, Byte Array 型 buff );
```

### Parameters
pattern
勝負服の色・模様を示す服色標示を最大全角 30 文字で指定します。服色標示の文字列は、レーシングプログラムに記載されているものとなります。
(パラメータ例) 「水色,赤山形一本輪,水色袖」
注意事項:
・勝負服画像が作成できない場合は、「No Image」画像が出力されます。
buff
画像データが格納されたバイト配列がセットされるポインタを設定します。

### Return Value
処理が正しく終了した場合はコード 0 を返します。
エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。
(「3.コード表」参照)

### Explanation
服色標示パラメータより、勝負服画像の画像データを返します。
作成される画像データは、サイズ50pix×50pix のビットマップ形式(24 ビット)となります。形式・サイズについては、必要に応じて競馬ソフト側で変換してご使用ください。

## JVMVOpen

- **JVMVOpen** — 動画リストの取得要求

### Syntax
```text
Long JVMVOpen(String 型 movietype, String 型 searchkey);
```

### Parameters
movietype
取得する動画リストの種類を指定します。 種類によって、searchkey の内容が異なります。
searchkey
該当データを取得するための要求キーを指定します。
種類 | movietype | 指定するキー(searchkey) | 説明
調教映像指定週全馬 | “11” | “YYYYMMDD” | YYYY:開催年MM  :開催月DD  :開催日指定開催年月日の週に行われた調教の映像が対象になります。指定の開催年月日に出走する競走馬の調教映像ではなく、すべての競走馬が対象になります。 データの順序は保障されません。
調教映像指定週指定馬 | “12” | “YYYYMMDDNNNNNNNNNN” | YYYY:開催年MM  :開催月DD  :開催日 NNNNNNNNNN:血統登録番号 血統登録番号で指定された競走馬の、 指定開催年月日の週の調教映像が対 象になります。
調教映像指定馬全調教 | “13” | “NNNNNNNNNN” | NNNNNNNNNN:血統登録番号血統登録番号で指定された競走馬の、公開中の調教映像が対象になります。データの順序は調教日の降順です。
種類
movietype
指定するキー(searchkey)
説明
調教映像
指定週全馬
“11”
“YYYYMMDD”
YYYY:開催年
MM  :開催月
DD  :開催日
指定開催年月日の週に行われた調教の映像が対象になります。指定の開催年月日に出走する競走馬の調教映像ではなく、すべての競走馬が対象になります。 データの順序は保障されません。
調教映像
指定週指定馬
“12”
“YYYYMMDDNNNNNNNNNN”
YYYY:開催年
MM  :開催月
DD  :開催日 NNNNNNNNN
N:血統登録番号 血統登録番号で指定された競走馬の、 指定開催年月日の週の調教映像が対 象になります。
調教映像
指定馬全調教
“13”
“NNNNNNNNNN”
NNNNNNNNNN:血統登録番号
血統登録番号で指定された競走馬の、公開中の調教映像が対象になります。データの順序は調教日の降順です。

### Return Value
処理が正しく終了した場合はコード 0 を返します。
エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。
(「3.コード表」参照)

### Explanation
データ識別文字列で指定したデータを読み込むための準備をします。具体的には以下の処理を行います。
・movietype、searchkey の検査を行います。
・movietype および searchkeyに対応するデータをサーバにリクエストします。
・データの受信を完了した時点で処理をアプリケーションに返します。

### Notes
当メソッドを利用するためには、JRA レーシングビュアー連携機能利用申請が必要になります。 (未申請の場合、戻り値に-304 が返されます。)
詳細については JRA-VANホームページのソフト作者サポートページを参照ください。
※当メソッドを使用した開発を行う際には、JVInit メソッドにてソフトウェア ID に "SA000000/SD000004"をセットしていただくことで当メソッドを利用可能となります。

## JVMVRead

- **JVMVRead** — 動画リストの読み込み

### Syntax
```text
Long JVMVRead( String 型 buff , Long 型 size);
```

### Parameters
buff
呼出側で用意したデータ格納バッファを指定します。1行単位で読み出しますので改行コード
(0x0d,0x0a)を含めたレコードデータが収容可能なサイズ+1(ストリング終端文字  NULL)を用意します。
size
呼出側で用意したデータ格納バッファにコピーするデータの長さを指定します。この値がレコード長よりも小さい場合には残りのデータは切り捨てられ、データ格納バッファの最後の1バイトがストリング終端文字 NULL となります。
読み出されるレコードデータは以下のようになります。
種類 | レコードデータ内容 | 最大長 | 説明
調教映像 | “YYYYMMDDNNNNNNNNNN<改行>” | 21 | YYYY:調教実施年MM :調教実施月 DD  :調教実施日NNNNNNNNNN:血統登録番号
種類
レコードデータ内容
最大長
説明
調教映像
“YYYYMMDDNNNNNNNNNN<改行>”
21
YYYY:調教実施年
MM :調教実施月 DD  :調教実施日
NNNNNNNNNN:血統登録番号
※改行コードが不要な場合、size に最大長よりも2 少ない値を設定してください。

### Return Value
正常にレコードを読み込んだ場合はバッファーにセットされたデータのバイト数が返ります。全てのデータを読み終わると0が返ります。エラーが発生した場合にはエラーの理由コードとして負の数が返されます。(「3.コード表」参照)
JVRead と異なり、データの読み終わりの際に-1 を返さないことに注意してください。

### Explanation
JVMVOpen で準備した動画リストをカレント行から1行分読み出します。 JVMVOpen を行なわずに JVMVReadメソッドを呼び出すとエラーが返ります。 JVOpen / JVRTOpen 中は使用できません。(エラーが返ります。)

### Notes
JVMVRead では、渡されたデータ格納バッファは JV-Link 内にて解放し、新たに確保したバッファを buff に割り当てられます。
処理後は、JVClose を呼ぶ必要があります。
当メソッドを利用するためには、JRA レーシングビュアー連携機能利用申請が必要になります。 詳細については JRA-VANホームページのソフト作者サポートページを参照ください。
※当メソッドを使用した開発を行う際には、JVInit メソッドにてソフトウェア ID に "SA000000/SD000004"をセットしていただくことで当メソッドを利用可能となります。

## JVCourseFile

- **JVCourseFile** — コース図要求

### Syntax
```text
Long JVCourseFile(String 型 key, String 型 filepath, String 型 explanation);
```

### Parameters
key
コース図を取得するための要求キーを指定します。
指定するキー(key) | 説明
“YYYYMMDDJJKKKKTT” | YYYY:開催年MM  :開催月DD  :開催日JJ :場コード KKKK:距離TT  :トラックコード
指定するキー(key)
説明
“YYYYMMDDJJKKKKTT”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ :場コード KKKK:距離
TT  :トラックコード
※最新のコース図を取得したい場合は、開催年月日に「99999999」を指定してください。 東京芝 2400m の最新のコース図を取得したい場合に指定する要求キーは、
「9999999905240011」となります。
filepath
画像のファイルパス(ドライブ名を含むフルパス)が返されます。 対象コースがない場合は「No Image」画像のファイルパスが返されます。
explanation
コースの説明が返されます。コースの説明は最大で 6800バイトとなります。 対象コースがない場合は空文字列が返されます。

### Return Value
処理が正しく終了した場合はコード 0 を返します。
エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)

### Explanation
key で指定した該当レースのコース図が取得できます。 コンテンツサーバから取得したコース図はm_savepath 以下のpictures フォルダに一時的に保存 れます。
取得したコース図は、サイズ 256pix × 200pix の GIF 形式となります。形式・サイズについては、必要に応じて競馬ソフト側で変換してご使用ください。

## JVCourseFile2

- **JVCourseFile2** — コース図要求

### Syntax
```text
Long JVCourseFile2(String 型 key, String 型 filepath);
```

### Parameters
key
コース図を取得するための要求キーを指定します。
指定するキー(key) | 説明
“YYYYMMDDJJKKKKTT” | YYYY:開催年MM  :開催月DD  :開催日JJ :場コード KKKK:距離TT  :トラックコード
指定するキー(key)
説明
“YYYYMMDDJJKKKKTT”
YYYY:開催年
MM  :開催月
DD  :開催日
JJ :場コード KKKK:距離
TT  :トラックコード
※最新のコース図を取得したい場合は、開催年月日に「99999999」を指定してください。 東京芝 2400m の最新のコース図を取得したい場合に指定する要求キーは、
「9999999905240011」となります。 注意事項:該当するコース図が存在ない場合は、
「No Image」画像が出力されます。
filepath
コース図ファイルの出力ファイル名をフルパスで指定します。実際に存在するフォルダを指定す る必要があります。

### Return Value
処理が正しく終了した場合はコード 0 を返します。 エラーが発生した場合にはエラーの理由コードとしての負の数が返されます。(「3.コード表」参照)

### Explanation
key で指定した該当レースのコース図を取得します。取得したファイルは filepath で指定したパスに 保存されます。
取得したコース図は、サイズ 256pix × 200pix の GIF 形式となります。形式・サイズについては、 必要に応じて競馬ソフト側で変換してご使用ください。 蓄積系データとして取得したコース情報からコース図を取得する際は、コース改修年月日を開催年、 開催月、開催日として要求キーに指定してください。データ区分「2:更新」のデータは、コース解説やコース図画像ファイルが変更された際に提供するデータです。従いまして、データ区分が「1:新規登録」、または「2:更新」のレコードが存在する場合は、このメソッドを使用してコース図を取得してください。
データ仕様の詳細につきましては、JV-Data 仕様書をご参照ください。

# JV-Link SDK 5.0 API・データ投射監査

調査日: 2026-09-12。対象: `JRA-VAN Data Lab. SDK Ver5.0.0_64bit` と調査当時の Xanthos 作業ツリー。

> **履歴資料（2026-09-14追記）:** 以下は修正前の監査結果であり、現在の実装状態を示すものではない。ここで挙げた投射の不一致は0.3.0の実装で対応した。現在のAPI対応表と検証範囲は [API coverage](api-coverage.md)、移行方法は [record migration](../../docs/record-migration.md) を参照。実サービスの全通知種別の観測や実データによる旧形式の検証まで完了したことを意味しない。

## 調査時点の結論

**Xanthos は SDK 5.0 の完全な投射にはなっていない。** 公開メソッド名の欠落は見つからなかったが、64bit 起動、データ構造、イベント、引数の表現に確定した不一致がある。

| 観点 | 確認結果 |
|---|---|
| COM メソッド | 26/26 の呼び出し入口が存在。契約全体の適合を意味しない |
| COM プロパティ | 9/9 に対応する入口が存在。読み書きの制約・変換あり |
| COM イベント | 7/7 の受信メソッドが存在。ただしイベント種類とキーの処理が不適合 |
| JV-Data レコード | 仕様38種すべてに分岐あり。ただし24種で共通ヘッダーの位置が不一致。残る14種も全フィールド適合は未認定 |
| 64bit 実行 | `ComClientFactory.tryCreate` が COM 検出前に拒否。サンプルの Windows ターゲットも x86 固定 |

既存の [api-coverage.md](api-coverage.md) の「100%」は、API名の存在を示すものとして扱うべきであり、実データへの適合や5.0の64bit動作の保証には使えない。

## 原典と版番号

5.0 SDK に同梱された仕様書は4.9.0.1表記のまま。これは取り違えではなく、[公式SDK更新告知](https://developer.jra-van.jp/t/topic/959)でも仕様書に変更がないと説明されている。主な変更は64bit版の追加、Agent・設定画面周辺、開発環境の更新である。[公式リリース告知](https://jra-van.jp/news/20260804a.html)も参照。

以下の4ファイルは、手元の4.9.0.2 SDK内の同名ファイルとSHA-256が完全一致した。

| ID | 5.0 SDKの `ドキュメント/` 内の原典 | SHA-256 |
|---|---|---|
| S1 | `JV-Linkインターフェース仕様書_4.9.0.1(Win).pdf`（65ページ） | `dfd1c425a62304bb464f15c25106e030ffccbf99c7c777972d6bb6b6d27ef1d7` |
| S2 | `JV-Data仕様書_4.9.0.1.xlsx`（7シート） | `23bafd375f704acbdd696b5032ac1619f17d47e882587d6e7954b610527a8234` |
| S3 | `JV-Data仕様書_4.9.0.1.pdf`（56ページ） | `b6c21aae4ccbba6a71c5e8065609c4fbb1ccee826c16e7d99ca6ecf7a4101522` |
| S4 | `蓄積系提供データ一覧.xls` | `6658f662f6eefe51c3eb7b755ca92a0405f222ac01c4cfe45e1969ca0a51299d` |

加えて開発ガイド4.2.3、イベントのPython/VB/C++ガイド、同梱サンプルを確認した。開発ガイドは64bitプラットフォーム設定を説明し、Pythonイベント例は `JVRTOpen("0B12", bstr)` とデータ種別・キーを別々に渡す。

## 確定した不一致

### P0: 64bit版を標準の生成経路から利用できない

[ComClientFactory.fs](../../src/Xanthos/Interop/ComClientFactory.fs) は `Environment.Is64BitProcess` が真なら、登録済みCOMの有無によらず `ActivationFailure` を返す。x64のF# Interactiveでこの拒否を再現した。[CLIのプロジェクト](../../samples/Xanthos.Cli/Xanthos.Cli.fsproj)も `net10.0-windows` で `PlatformTarget=x86` を指定している。

5.0 SDKの64bit版を使うには生成処理・CLI・検証スクリプト・説明の更新が必要。`new ComJvLinkClient()` を直接使う経路まで起動不能と断定するものではなく、その64bit実動作は未検証。`ParentHWnd` の `IntPtr`→`int` 変換も、S1のLong宣言と実際の64bitタイプライブラリを照合すべき項目であり、単純に64bit整数へ変更してよいとは断定しない。

### P0: 主要レコード24種でヘッダーとフィールド位置が不一致

S2「フォーマット」では先頭にレコードID（位置1・2バイト）、データ区分（位置3・1バイト）、作成年月日（位置4・8バイト）がある。24種の実装は位置3からレースキー・馬ID等を読み出しており、必須ヘッダーをデータ本体として解釈する。

例: [RA.fs](../../src/Xanthos/Core/Records/RA.fs) とS2の76〜139行を比較すると、次のようにずれる。仕様は1始まり、実装は0始まりとして換算した。

| 項目 | 仕様の位置・長さ | 現実装の位置・長さ |
|---|---|---|
| レース識別部分 | 12から16バイト | 3から16バイト |
| 競走名本題 | 33から60バイト | 19から50バイト |
| 距離 | 698から4バイト | 73から4バイト |
| 発走時刻 | 874から4バイト（hhmm） | 80から12バイト（yyyyMMddHHmm） |

仕様位置に値を入れた1272バイトの合成RAを公開パーサーへ渡すと、期待したキー `2026091205040101` は `1202609122026091` に化け、名称には別のヘッダー値が混入し、距離2400は `None` になった。これは単なるフィールド省略ではなく、誤読である。

### P0: レコードIDとデータの意味自体が異なる

| ID | S2の意味 | 現実装の意味 |
|---|---|---|
| WF | 重勝式（WIN5） | 馬体重。`WFRecord` は馬番・体重等のみ |
| H1 / H6 | 票数1 / 票数6（三連単） | 払戻データ |
| O2 / O3 / O4 / O5 / O6 | 馬連 / ワイド / 馬単 / 三連複 / 三連単 | 複勝 / 枠連 / 馬連 / ワイド / 馬単 |
| BR / BN | 生産者マスタ / 馬主マスタ | 繁殖牝馬 / 繁殖馬 |
| AV / TC | 出走取消・競走除外 / 発走時刻変更 | 馬場状態変更 / 調教タイム |

馬体重には仕様どおりの別ID `WH` がある。また `H5` は現実装に存在するが、同梱仕様の38レコードに存在しない。O1やHR等も繰り返し配列を含む原典に対して単一エントリーのモデルであり、ヘッダーだけの修正では完全投射にならない。

### P1: イベント種別を失い、正式な変更通知キーを拒否する

S1 p.50では払戻・馬体重の引数は `YYYYMMDDJJRR`、変更系5種は `TTYYYYMMDDJJRR` に14桁の送信日時を付ける形式。`0B12` 等のdataspecはキーに含まれない。

[ComEvents.fs](../../src/Xanthos/Interop/ComEvents.fs) は7種類すべてを同じ `string -> unit` に送り、発火したイベント種別を捨てる。[Serialization.fs](../../src/Xanthos/Core/Serialization.fs) は存在しない `0B` 接頭辞を前提に分類するため、正式キーから `UnknownEvent` が生成される。払戻と馬体重は同じ形のキーなので、文字列だけから種別を復元することもできない。

合成した正式キーで次を再現した。

- `202609120511` → `UnknownEvent`、`WatchEvent.toRealtimeRequest` → `None`。
- `JC20260912051120260912120000` → `UnknownEvent`。さらに [Validation.fs](../../src/Xanthos/Runtime/Validation.fs) が非数字文字として拒否し、高水準リアルタイム取得へ渡せない。

低水準の `IJvLinkClient.OpenRealtime(spec, key)` 自体は文字列を受け取れるため、呼び出し経路を区別する必要がある。

### P1: JVOpenの終了時刻を表現できない

S1 pp.17–18は `fromtime` に開始時刻だけでなく `開始-終了` を許容する（一部dataspecは対象外）。[JvOpenRequest](../../src/Xanthos/Interop/IJvLinkClient.fs) は `FromTime: DateTime` のみで、[ComJvLinkClient.fs](../../src/Xanthos/Interop/ComJvLinkClient.fs) は常に単一の14桁へ変換する。CLI用の `parseFromTime` も範囲文字列を拒否する。既存APIでは終了時刻指定を投射できない。

### P1: コード表の型にも不一致がある

S2「コード表」144〜156行のグレードコードは `A/B/C/D/E/F/G/H/L` 等の文字コードだが、[CodeTables.fs](../../src/Xanthos/Core/Records/CodeTables.fs) は整数enumと `Int32.TryParse` を使用する。公式G1コード `A` を `parseCode<GradeCode>` へ渡すと `None` になることを再現した。フィールド位置とコード変換を一緒に見直す必要がある。

## 全メソッドの対応表

「入口あり」は、`IJvLinkClient` とCOM呼び出しをソースで確認したという意味。S1 p.9の26メソッドを母集団とし、実機適合を認定する表ではない。

| JV-Link | IJvLinkClient側 | Runtime側・補足 |
|---|---|---|
| JVInit | Init | サービスの初期化処理。入口あり |
| JVSetUIProperties | SetUiProperties | ShowConfigurationDialog。入口あり |
| JVSetServiceKey | SetServiceKeyDirect | SetServiceKey。入口あり |
| JVSetSaveFlag | SetSaveFlag | SetSaveDownloadsEnabled。入口あり |
| JVSetSavePath | SetSavePathDirect | SetSavePath。入口あり |
| JVOpen | Open | FetchPayloads等。終了時刻の指定欠落 |
| JVRTOpen | OpenRealtime | StreamRealtimePayloads/Async。変更イベントキーの検証に不一致 |
| JVStatus | Status | GetStatus。入口あり |
| JVRead | Read | JvReadOutcomeへ抽象化。ファイル名・読取バイト数を直接返す形ではない |
| JVGets | Gets / Read内部 | Getsの公開bufferはstring。Read経路ではbyte[]ペイロードを取得 |
| JVSkip | Skip | SkipCurrentFile。元APIはvoid、例外をResultへ変換 |
| JVCancel | Cancel | CancelDownload。元APIはvoid、例外をResultへ変換 |
| JVClose | Close | サービスの終了処理。戻り値・例外を握りつぶし、呼出側へ返さない |
| JVFiledelete | DeleteFile | DeleteFile。入口あり |
| JVFukuFile | SilksFile | GenerateSilksFile。No Imageの-1をNoneに変換 |
| JVFuku | SilksBinary | GetSilksBinary。No Imageの-1では返却画像を公開しない |
| JVMVCheck | MovieCheck | CheckMovieAvailability。0/1/-1を識別 |
| JVMVCheckWithType | MovieCheckWithType | CheckMovieAvailabilityのオーバーロード |
| JVMVPlay | MoviePlay | PlayMovie。入口あり |
| JVMVPlayWithType | MoviePlayWithType | PlayMovieのオーバーロード |
| JVMVOpen | MovieOpen | FetchWorkoutVideos。11/12/13の入口あり |
| JVMVRead | MovieRead | FetchWorkoutVideos。呼出側バッファの扱いは下記の要実機確認事項 |
| JVCourseFile | CourseFile | GetCourseDiagram。文字化けと判定した説明文をNoneにする |
| JVCourseFile2 | CourseFile2 | GetCourseDiagramBasic。入口あり |
| JVWatchEvent | WatchEvent | StartWatchEvents。受信後の種類・キー処理に不一致 |
| JVWatchEventClose | WatchEventClose | StopWatchEvents。入口あり |

追加の確認事項: S1 p.43の `JVMVRead` は呼出側がバッファを用意する契約だが、現実装は空文字列とsize=4096を渡している。5.0実機での結果は未検証のため、確定バグとは別扱いとする。JVReadのファイル名、JVCloseのエラー、No Image画像等を抽象化で捨てる設計も、「損失のない基底API」を目標とするなら見直す必要がある。

## プロパティ・イベントの網羅性

| S1 pp.7–8のプロパティ | 対応するRuntime API | 制約 |
|---|---|---|
| m_saveflag | Get/SetSaveDownloadsEnabled | bool変換 |
| m_savepath | Get/SetSavePath | 設定はJVSetSavePath経由 |
| m_servicekey | Get/SetServiceKey | 設定はJVSetServiceKey経由 |
| m_JVLinkVersion | GetJVLinkVersion | 読取 |
| m_TotalReadFilesize | GetTotalReadFileSize / Bytes | 原典のKB単位とByte換算を区別 |
| m_CurrentReadFilesize | GetCurrentReadFileSize | 読取 |
| m_CurrentFileTimestamp | GetCurrentFileTimestamp | DateTime optionへ変換 |
| ParentHWnd | Set/GetParentWindowHandle | COM読取を未対応扱い。64bit時の型を要確認 |
| m_payflag | Get/SetPayoffDialogSuppressed | COM設定を拒否し、設定画面へ誘導 |

`m_payflag` の書込み可否はS1 p.8のフラグ説明だけからは確定できない。既存実装の「実機ではread-only」という前提は5.0でも再確認が必要であり、ここでは未対応メソッドとして数えていない。

イベントは `JVEvtPay`、`JVEvtJockeyChange`、`JVEvtWeather`、`JVEvtCourseChange`、`JVEvtAvoid`、`JVEvtTimeChange`、`JVEvtWeight` の全7種類に受信関数・DISPID登録がある。ただし上記の情報喪失があるため、7/7の正しい型付きイベント投射とは評価できない。イベントIID・DISPIDの64bitタイプライブラリとの一致は未検証。

## データレコード全38種の一次照合

原典はS2「フォーマット」。行番号はレコードIDが記載されたExcelの行。全行について `PayloadParser` の分岐と各 `.fs` の存在を確認した。「不一致」は共通ヘッダー位置だけでも反証できるもの。「要精査」は共通ヘッダー位置が一致するだけで、全フィールド・繰返し・旧形式・空値・更新削除区分まで適合したという意味ではない。

| ID | 原典の名称 | 長さ（byte） | Excel行 | ヘッダー照合 |
|---|---|---:|---:|---|
| TK | １．特別登録馬 | 21657 | 18 | 不一致 |
| RA | ２．レース詳細 | 1272 | 76 | 不一致 |
| SE | ３．馬毎レース情報 | 555 | 143 | 不一致 |
| HR | ４．払戻 | 719 | 218 | 不一致 |
| H1 | ５．票数１ | 28955 | 300 | 不一致 |
| H6 | ６．票数6（3連単） | 102890 | 369 | 不一致 |
| O1 | ７．オッズ1（単複枠） | 962 | 393 | 不一致 |
| O2 | ８．オッズ2（馬連） | 2042 | 430 | 不一致 |
| O3 | ９．オッズ3（ワイド） | 2654 | 453 | 不一致 |
| O4 | １０．オッズ4（馬単） | 4031 | 477 | 不一致 |
| O5 | １１．オッズ5（3連複） | 12293 | 500 | 不一致 |
| O6 | １２．オッズ6（3連単） | 83285 | 523 | 不一致 |
| UM | １３．競走馬マスタ | 1609 | 546 | 不一致 |
| KS | １４．騎手マスタ | 4173 | 620 | 不一致 |
| CH | １５．調教師マスタ | 3862 | 710 | 不一致 |
| BR | １６．生産者マスタ | 545 | 781 | 不一致 |
| BN | １７．馬主マスタ | 477 | 802 | 不一致 |
| HN | １８．繁殖馬マスタ | 251 | 823 | 要精査 |
| SK | １９．産駒マスタ | 208 | 847 | 要精査 |
| CK | ２０．出走別着度数 | 6870 | 865 | 要精査 |
| RC | ２１．レコードマスタ | 501 | 1106 | 不一致 |
| HC | ２２．坂路調教 | 60 | 1144 | 要精査 |
| HS | ２３．競走馬市場取引価格 | 200 | 1172 | 要精査 |
| HY | ２４．馬名の意味由来 | 123 | 1191 | 要精査 |
| YS | ２５．開催スケジュール | 382 | 1202 | 要精査 |
| BT | ２６．系統情報 | 6889 | 1231 | 要精査 |
| CS | ２７．コース情報 | 6829 | 1243 | 要精査 |
| DM | ２８．タイム型データマイニング予想 | 303 | 1256 | 要精査 |
| TM | ２９．対戦型データマイニング予想 | 141 | 1278 | 要精査 |
| WF | ３０．重勝式(WIN5) | 7215 | 1298 | 不一致 |
| JG | ３１．競走馬除外情報 | 80 | 1340 | 要精査 |
| WC | ３２．ウッドチップ調教 | 105 | 1359 | 要精査 |
| WH | １０１．馬体重 | 847 | 1415 | 要精査 |
| WE | １０２．天候馬場状態 | 42 | 1437 | 不一致 |
| AV | １０３．出走取消・競走除外 | 78 | 1463 | 不一致 |
| JC | １０４．騎手変更 | 161 | 1481 | 不一致 |
| TC | １０５．発走時刻変更 | 45 | 1511 | 不一致 |
| CC | １０６．コース変更 | 50 | 1533 | 不一致 |

S2「データ種別一覧」の蓄積系・速報系dataspecとS4も参照した。dataspecは文字列を受け取るため、新しいIDの列挙漏れによる遮断は見つからなかった。ただし `0B51` 等で返るWFを正しく解釈できないため、要求を発行できることと返却データを利用できることは別である。S2は不明なレコードを読み飛ばす設計も求めており、現実装の `UnknownRecord` はこの拡張余地を持つ。

## 検証範囲と次の作業

原典の抽出・ハッシュ比較、26メソッド/9プロパティ/7イベントの入口照合、38レコードの分岐・ヘッダー照合、上記のF#再現を実施した。SDKのインストーラー実行、利用キー登録、実サービスへの接続は行っていない。14レコードの全項目・全コード表・全戻り値についての完全適合認定は今回の結果に含めない。

前回のスタブ中心の1,009件のテスト成功は、公式レイアウトへの適合を保証しない。実フィクスチャ未配置でスキップされたテストもあり、独自の合成レイアウトで通るテストと仕様に基づくテストを区別する必要がある。

推奨する修正順序:

1. 基底APIで公式引数・戻り値・生byte[]・ファイル名・イベント種類を保持する契約を定義する。
2. 64bit起動経路を整備し、32bit/64bitそれぞれでCOM契約を確認する。
3. RA/SE/HR/オッズ/票数/WFを優先してモデルとパーサーを原典から再構成する。グレード等のコード表も修正する。
4. イベント種類と生キーを一緒に渡し、正式キーをリアルタイム取得へ接続する。JVOpenの時刻範囲も追加する。
5. 全38種の公式位置から作った合成データ、繰返し境界、更新・削除区分、ローカル実フィクスチャで回帰試験する。

抽出テキスト・Excel JSON・入力ハッシュはローカルの `.artifacts/sdk-audit/` に保存した。再現は同ディレクトリの `reproduce.fsx` と `reproduction.txt` を参照。原典PDF/Excelや抽出全文は追跡対象に追加していない。本調査ではライブラリの実装を変更していない。

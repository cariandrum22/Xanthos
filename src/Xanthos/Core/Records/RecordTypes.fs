namespace Xanthos.Core.Records

/// JV-Data record type identifiers
module RecordTypes =

    /// All supported JV-Data record types
    type RecordType =
        // Race data (通常データ)
        | TK // 特別登録馬 (Special Registration)
        | RA // レース詳細 (Race Details)
        | SE // 馬毎レース情報 (Runner Race Info)
        | HR // 払戻 (Payoff)

        // Odds data (オッズデータ)
        | O1 // オッズ1(単複枠) - Win/Place/Bracket Quinella Odds
        | O2 // オッズ2(馬連) - Quinella Odds
        | O3 // オッズ3(ワイド) - Wide Odds
        | O4 // オッズ4(馬単) - Exacta Odds
        | O5 // オッズ5(3連複) - Trio Odds
        | O6 // オッズ6(3連単) - Trifecta Odds

        // Vote count data (票数データ)
        | H1 // 票数1(単複枠) - Win/Place/Bracket Vote Count
        | H5 // 票数5 (if exists)
        | H6 // 票数6(3連単) - Trifecta Vote Count

        // Master data (マスタデータ)
        | UM // 競走馬マスタ (Horse Master)
        | KS // 騎手マスタ (Jockey Master)
        | CH // 調教師マスタ (Trainer Master)
        | BR // 生産者マスタ (Breeder Master)
        | BN // 馬主マスタ (Owner Master)
        | HN // 繁殖馬マスタ (Breeding Horse Master)
        | SK // 産駒マスタ (Offspring Master)
        | RC // レコードマスタ (Record Master)

        // Analysis data (分析データ)
        | CK // 出走別着度数 (Race Frequency by Runner)
        | HC // 坂路調教 (Hill Training)
        | HS // 競走馬市場取引価格 (Horse Market Price)
        | HY // 馬名の意味由来 (Horse Name Origin)
        | YS // 開催スケジュール (Race Schedule)
        | BT // 系統情報 (Lineage Info)
        | CS // コース情報 (Course Info)
        | DM // タイム型データマイニング予想 (Time-based Mining Prediction)
        | TM // 対戦型データマイニング予想 (Match-based Mining Prediction)
        | WF // 重勝式WIN5 (WIN5 Multi-race Bet)
        | WC // ウッドチップ調教 (Woodchip Training)

        // Real-time data (リアルタイムデータ)
        | WH // 馬体重 (Horse Weight)
        | WE // 天候馬場状態 (Weather/Track Condition)
        | AV // 出走取消・競走除外 (Scratch/Exclusion)
        | JC // 騎手変更 (Jockey Change)
        | TC // 発走時刻変更 (Post Time Change)
        | CC // コース変更 (Course Change)
        | JG // 競走馬除外情報 (Horse Exclusion Info)

        // Unknown
        | Unknown of string

    /// Parse 2-character record type identifier
    let parse (typeId: string) : RecordType =
        match typeId.ToUpperInvariant().Trim() with
        // Race data
        | "TK" -> TK
        | "RA" -> RA
        | "SE" -> SE
        | "HR" -> HR

        // Odds data
        | "O1" -> O1
        | "O2" -> O2
        | "O3" -> O3
        | "O4" -> O4
        | "O5" -> O5
        | "O6" -> O6

        // Vote count data
        | "H1" -> H1
        | "H5" -> H5
        | "H6" -> H6

        // Master data
        | "UM" -> UM
        | "KS" -> KS
        | "CH" -> CH
        | "BR" -> BR
        | "BN" -> BN
        | "HN" -> HN
        | "SK" -> SK
        | "RC" -> RC

        // Analysis data
        | "CK" -> CK
        | "HC" -> HC
        | "HS" -> HS
        | "HY" -> HY
        | "YS" -> YS
        | "BT" -> BT
        | "CS" -> CS
        | "DM" -> DM
        | "TM" -> TM
        | "WF" -> WF
        | "WC" -> WC

        // Real-time data
        | "WH" -> WH
        | "WE" -> WE
        | "AV" -> AV
        | "JC" -> JC
        | "TC" -> TC
        | "CC" -> CC
        | "JG" -> JG

        | other -> Unknown other

    /// Convert record type to string identifier
    let toString (recordType: RecordType) : string =
        match recordType with
        // Race data
        | TK -> "TK"
        | RA -> "RA"
        | SE -> "SE"
        | HR -> "HR"
        // Odds data
        | O1 -> "O1"
        | O2 -> "O2"
        | O3 -> "O3"
        | O4 -> "O4"
        | O5 -> "O5"
        | O6 -> "O6"
        // Vote count data
        | H1 -> "H1"
        | H5 -> "H5"
        | H6 -> "H6"
        // Master data
        | UM -> "UM"
        | KS -> "KS"
        | CH -> "CH"
        | BR -> "BR"
        | BN -> "BN"
        | HN -> "HN"
        | SK -> "SK"
        | RC -> "RC"
        // Analysis data
        | CK -> "CK"
        | HC -> "HC"
        | HS -> "HS"
        | HY -> "HY"
        | YS -> "YS"
        | BT -> "BT"
        | CS -> "CS"
        | DM -> "DM"
        | TM -> "TM"
        | WF -> "WF"
        | WC -> "WC"
        // Real-time data
        | WH -> "WH"
        | WE -> "WE"
        | AV -> "AV"
        | JC -> "JC"
        | TC -> "TC"
        | CC -> "CC"
        | JG -> "JG"
        // Unknown
        | Unknown s -> s

    /// Check if record type is recognized (known in JV-Data specification)
    let isRecognized (recordType: RecordType) : bool =
        match recordType with
        | Unknown _ -> false
        | _ -> true

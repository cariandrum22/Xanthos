namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// KS Record: 騎手マスタ (Jockey Master)
module KS =

    /// KS Record data
    type KSRecord =
        { JockeyCode: string // 騎手コード
          JockeyName: string // 騎手名
          JockeyNameKana: string option // 騎手名カナ
          BelongsTo: string option // 所属
          InitialYear: int option // 初免許年
          BirthDate: DateTime option } // 生年月日

    /// KS record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "JockeyCode" 2 5 // 騎手コード
          text "JockeyName" 7 34 // 騎手名
          text "JockeyNameKana" 41 30 // 騎手名カナ
          text "BelongsTo" 71 10 // 所属
          int "InitialYear" 81 4 // 初免許年
          date "BirthDate" 85 8 "yyyyMMdd" ] // 生年月日

    /// Parse KS record from raw bytes
    let parse (data: byte[]) : Result<KSRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! jockeyCode = requireText fields "JockeyCode"
            let! jockeyName = requireText fields "JockeyName"

            return
                { JockeyCode = jockeyCode
                  JockeyName = jockeyName
                  JockeyNameKana = getText fields "JockeyNameKana"
                  BelongsTo = getText fields "BelongsTo"
                  InitialYear = getInt fields "InitialYear"
                  BirthDate = getDate fields "BirthDate" }
        }

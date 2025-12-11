namespace Xanthos.Core.Records

open System
open Xanthos.Core
open Xanthos.Core.Records.FieldDefinitions
open Xanthos.Core.Records.RecordParser

/// CH Record: 調教師マスタ (Trainer Master)
module CH =

    /// CH Record data
    type CHRecord =
        { TrainerCode: string // 調教師コード
          TrainerName: string // 調教師名
          TrainerNameKana: string option // 調教師名カナ
          BelongsTo: string option // 所属
          InitialYear: int option // 初免許年
          BirthDate: DateTime option } // 生年月日

    /// CH record field specifications
    let private fieldSpecs =
        [ textRaw "RecordType" 0 2 // レコード種別
          text "TrainerCode" 2 5 // 調教師コード
          text "TrainerName" 7 34 // 調教師名
          text "TrainerNameKana" 41 30 // 調教師名カナ
          text "BelongsTo" 71 10 // 所属
          int "InitialYear" 81 4 // 初免許年
          date "BirthDate" 85 8 "yyyyMMdd" ] // 生年月日

    /// Parse CH record from raw bytes
    let parse (data: byte[]) : Result<CHRecord, XanthosError> =
        parseRecord {
            let! fields = parseFieldsXanthos data fieldSpecs
            let! trainerCode = requireText fields "TrainerCode"
            let! trainerName = requireText fields "TrainerName"

            return
                { TrainerCode = trainerCode
                  TrainerName = trainerName
                  TrainerNameKana = getText fields "TrainerNameKana"
                  BelongsTo = getText fields "BelongsTo"
                  InitialYear = getInt fields "InitialYear"
                  BirthDate = getDate fields "BirthDate" }
        }

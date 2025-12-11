namespace Xanthos.Core.Records

open System

/// Code table enumerations for JV-Data records
module CodeTables =

    /// 性別コード (Sex Code)
    type SexCode =
        | Male = 1 // 牡
        | Female = 2 // 牝
        | Gelding = 3 // セン

    /// 毛色コード (Hair Color Code)
    type HairColorCode =
        | Chestnut = 1 // 栗毛
        | Liver = 2 // 栃栗毛
        | Bay = 3 // 鹿毛
        | DarkBay = 4 // 黒鹿毛
        | Brown = 5 // 青鹿毛
        | Black = 6 // 青毛
        | Gray = 7 // 芦毛
        | Roan = 8 // 栗粕毛
        | Palomino = 9 // 白毛

    /// 馬場状態コード (Track Condition Code)
    type TrackConditionCode =
        | Good = 1 // 良
        | Yielding = 2 // 稍重
        | Soft = 3 // 重
        | Heavy = 4 // 不良

    /// トラック種別コード (Track Surface Code)
    type TrackSurfaceCode =
        | Turf = 1 // 芝
        | Dirt = 2 // ダート
        | Obstacle = 3 // 障害

    /// 競馬場コード (Racecourse Code)
    type RacecourseCode =
        | Sapporo = 1
        | Hakodate = 2
        | Fukushima = 3
        | Niigata = 4
        | Tokyo = 5
        | Nakayama = 6
        | Chukyo = 7
        | Kyoto = 8
        | Hanshin = 9
        | Kokura = 10

    /// グレードコード (Grade Code)
    type GradeCode =
        | None = 0 // なし
        | G1 = 1 // G1
        | G2 = 2 // G2
        | G3 = 3 // G3
        | Listed = 4 // リステッド
        | OpenClass = 5 // オープン

    /// レース条件コード (Race Condition Code)
    type RaceConditionCode =
        | TwoYearOld = 1 // 2歳
        | ThreeYearOld = 2 // 3歳
        | ThreeYearOldAndUp = 3 // 3歳以上
        | FourYearOldAndUp = 4 // 4歳以上

    /// 脚質コード (Running Style Code)
    type RunningStyleCode =
        | Escape = 1 // 逃げ
        | Leading = 2 // 先行
        | Tracking = 3 // 差し
        | Pursuing = 4 // 追込

    /// Parse string code to enum with validation
    let parseCode<'T when 'T: enum<int>> (value: string) : 'T option =
        match Int32.TryParse value with
        | true, code ->
            if Enum.IsDefined(typeof<'T>, code) then
                Some(enum<'T> code)
            else
                None
        | false, _ -> None

    /// Parse code with fallback to default value
    let parseCodeOrDefault<'T when 'T: enum<int>> (value: string) (defaultValue: 'T) : 'T =
        parseCode<'T> value |> Option.defaultValue defaultValue

    /// Get all defined values for an enum type
    let getAllCodes<'T when 'T: enum<int>> () : 'T list =
        Enum.GetValues(typeof<'T>) |> Seq.cast<'T> |> Seq.toList

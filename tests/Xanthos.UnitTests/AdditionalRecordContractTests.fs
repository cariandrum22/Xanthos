namespace Xanthos.UnitTests

open System
open Xunit
open Xanthos

module AdditionalRecordContractTests =
    let internal value =
        function
        | Ok v -> v
        | Error e -> failwithf "%A" e

    let internal error =
        function
        | Error e -> e
        | Ok _ -> failwith "Expected failure"

    let internal mapping id =
        match id with
        | "HN" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "BreedingId"
              "6", "PedigreeId"
              "8", "Name"
              "9", "KanaName"
              "10", "EuropeanName"
              "11", "BirthYear"
              "12", "Sex"
              "13", "Breed"
              "14", "Coat"
              "15", "ImportKind"
              "16", "ImportYear"
              "17", "Birthplace"
              "18", "FatherBreedingId"
              "19", "MotherBreedingId" ]
        | "SK" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "PedigreeId"
              "5", "BirthDate"
              "6", "Sex"
              "7", "Breed"
              "8", "Coat"
              "9", "ImportKind"
              "10", "ImportYear"
              "11", "BreederId"
              "12", "Birthplace"
              "13", "AncestorBreedingIds[{i}]" ]
        | "HS" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "PedigreeId"
              "5", "FatherBreedingId"
              "6", "MotherBreedingId"
              "7", "BirthYear"
              "8", "MarketId"
              "9", "Organizer"
              "10", "MarketName"
              "11", "StartDate"
              "12", "EndDate"
              "13", "Age"
              "14", "PriceYen" ]
        | "HY" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "PedigreeId"
              "5", "Name"
              "6", "Origin" ]
        | "BT" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "BreedingId"
              "5", "LineageId"
              "6", "Name"
              "7", "Description" ]
        | "CS" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Racecourse"
              "5", "DistanceMetres"
              "6", "Track"
              "7", "RenovationDate"
              "8", "Description" ]
        | "JG" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Identity.RaceNumber"
              "10", "PedigreeId"
              "11", "Name"
              "12", "EntrySequence"
              "13", "EntryStatus"
              "14", "ExclusionStatus" ]
        | "YS" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Weekday"
              "10.a", "GradedRaces[{i}].SpecialRaceNumber"
              "10.b", "GradedRaces[{i}].Title"
              "10.c", "GradedRaces[{i}].Abbreviation10"
              "10.d", "GradedRaces[{i}].Abbreviation6"
              "10.e", "GradedRaces[{i}].Abbreviation3"
              "10.f", "GradedRaces[{i}].Edition"
              "10.g", "GradedRaces[{i}].Grade"
              "10.h", "GradedRaces[{i}].RaceKind"
              "10.i", "GradedRaces[{i}].RaceSymbol"
              "10.j", "GradedRaces[{i}].WeightRule"
              "10.k", "GradedRaces[{i}].DistanceMetres"
              "10.l", "GradedRaces[{i}].Track" ]
        | "HC" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Centre"
              "5", "Date"
              "6", "Time"
              "7", "PedigreeId"
              "8", "Totals[0]"
              "9", "Laps[0]"
              "10", "Totals[1]"
              "11", "Laps[1]"
              "12", "Totals[2]"
              "13", "Laps[2]"
              "14", "Laps[3]" ]
        | "WC" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Centre"
              "5", "Date"
              "6", "Time"
              "7", "PedigreeId"
              "8", "Course"
              "9", "Direction"
              "11", "Totals[0]"
              "12", "Laps[0]"
              "13", "Totals[1]"
              "14", "Laps[1]"
              "15", "Totals[2]"
              "16", "Laps[2]"
              "17", "Totals[3]"
              "18", "Laps[3]"
              "19", "Totals[4]"
              "20", "Laps[4]"
              "21", "Totals[5]"
              "22", "Laps[5]"
              "23", "Totals[6]"
              "24", "Laps[6]"
              "25", "Totals[7]"
              "26", "Laps[7]"
              "27", "Totals[8]"
              "28", "Laps[8]"
              "29", "Laps[9]" ]
        | "WH" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Identity.RaceNumber"
              "10", "Announcement"
              "11.p1", "Horses[{i}].HorseNumber"
              "11.p3", "Horses[{i}].Name"
              "11.p39", "Horses[{i}].Weight"
              "11.p42", "Horses[{i}].ChangeSign"
              "11.p43", "Horses[{i}].Change" ]
        | "DM" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Identity.RaceNumber"
              "10", "CreatedTime"
              "11.a", "Horses[{i}].HorseNumber"
              "11.b", "Horses[{i}].PredictedSeconds"
              "11.c", "Horses[{i}].FasterErrorSeconds"
              "11.d", "Horses[{i}].SlowerErrorSeconds" ]
        | "TM" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Identity.RaceNumber"
              "10", "CreatedTime"
              "11.a", "Horses[{i}].HorseNumber"
              "11.b", "Horses[{i}].Score" ]
        | "CK" ->
            [ "1", "Header.RecordId"
              "2", "Header.DataCategory"
              "3", "Header.CreatedDateRaw"
              "4", "Identity.Year"
              "5", "Identity.MonthDay"
              "6", "Identity.Racecourse"
              "7", "Identity.Meeting"
              "8", "Identity.Day"
              "9", "Identity.RaceNumber"
              "10", "PedigreeId"
              "11", "Name"
              "12", "FlatBasePrizeHundredYen"
              "13", "JumpBasePrizeHundredYen"
              "14", "FlatAddedPrizeHundredYen"
              "15", "JumpAddedPrizeHundredYen"
              "16", "FlatEarningsHundredYen"
              "17", "JumpEarningsHundredYen"
              "18", "Overall[{i}]"
              "19", "Central[{i}]"
              "20", "TurfStraight[{i}]"
              "21", "TurfRight[{i}]"
              "22", "TurfLeft[{i}]"
              "23", "DirtStraight[{i}]"
              "24", "DirtRight[{i}]"
              "25", "DirtLeft[{i}]"
              "26", "Jump[{i}]"
              "27", "TurfFirm[{i}]"
              "28", "TurfGood[{i}]"
              "29", "TurfSoft[{i}]"
              "30", "TurfHeavy[{i}]"
              "31", "DirtFirm[{i}]"
              "32", "DirtGood[{i}]"
              "33", "DirtSoft[{i}]"
              "34", "DirtHeavy[{i}]"
              "35", "JumpFirm[{i}]"
              "36", "JumpGood[{i}]"
              "37", "JumpSoft[{i}]"
              "38", "JumpHeavy[{i}]"
              "39", "TurfUpTo1200[{i}]"
              "40", "Turf1201To1400[{i}]"
              "41", "Turf1401To1600[{i}]"
              "42", "Turf1601To1800[{i}]"
              "43", "Turf1801To2000[{i}]"
              "44", "Turf2001To2200[{i}]"
              "45", "Turf2201To2400[{i}]"
              "46", "Turf2401To2800[{i}]"
              "47", "TurfOver2800[{i}]"
              "48", "DirtUpTo1200[{i}]"
              "49", "Dirt1201To1400[{i}]"
              "50", "Dirt1401To1600[{i}]"
              "51", "Dirt1601To1800[{i}]"
              "52", "Dirt1801To2000[{i}]"
              "53", "Dirt2001To2200[{i}]"
              "54", "Dirt2201To2400[{i}]"
              "55", "Dirt2401To2800[{i}]"
              "56", "DirtOver2800[{i}]"
              "57", "SapporoTurf[{i}]"
              "58", "HakodateTurf[{i}]"
              "59", "FukushimaTurf[{i}]"
              "60", "NiigataTurf[{i}]"
              "61", "TokyoTurf[{i}]"
              "62", "NakayamaTurf[{i}]"
              "63", "ChukyoTurf[{i}]"
              "64", "KyotoTurf[{i}]"
              "65", "HanshinTurf[{i}]"
              "66", "KokuraTurf[{i}]"
              "67", "SapporoDirt[{i}]"
              "68", "HakodateDirt[{i}]"
              "69", "FukushimaDirt[{i}]"
              "70", "NiigataDirt[{i}]"
              "71", "TokyoDirt[{i}]"
              "72", "NakayamaDirt[{i}]"
              "73", "ChukyoDirt[{i}]"
              "74", "KyotoDirt[{i}]"
              "75", "HanshinDirt[{i}]"
              "76", "KokuraDirt[{i}]"
              "77", "SapporoJump[{i}]"
              "78", "HakodateJump[{i}]"
              "79", "FukushimaJump[{i}]"
              "80", "NiigataJump[{i}]"
              "81", "TokyoJump[{i}]"
              "82", "NakayamaJump[{i}]"
              "83", "ChukyoJump[{i}]"
              "84", "KyotoJump[{i}]"
              "85", "HanshinJump[{i}]"
              "86", "KokuraJump[{i}]"
              "87", "RunningStyle[{i}]"
              "88", "RecordedRaceCount"
              "89", "JockeyId"
              "90", "JockeyName"
              "91.a", "JockeyPerformances[{i}].Year"
              "91.b", "JockeyPerformances[{i}].FlatBasePrizeHundredYen"
              "91.c", "JockeyPerformances[{i}].JumpBasePrizeHundredYen"
              "91.d", "JockeyPerformances[{i}].FlatAddedPrizeHundredYen"
              "91.e", "JockeyPerformances[{i}].JumpAddedPrizeHundredYen"
              "91.f", "JockeyPerformances[{i}].Turf[{j}]"
              "91.g", "JockeyPerformances[{i}].Dirt[{j}]"
              "91.h", "JockeyPerformances[{i}].Jump[{j}]"
              "91.i", "JockeyPerformances[{i}].TurfUpTo1200[{j}]"
              "91.j", "JockeyPerformances[{i}].Turf1201To1400[{j}]"
              "91.k", "JockeyPerformances[{i}].Turf1401To1600[{j}]"
              "91.l", "JockeyPerformances[{i}].Turf1601To1800[{j}]"
              "91.m", "JockeyPerformances[{i}].Turf1801To2000[{j}]"
              "91.n", "JockeyPerformances[{i}].Turf2001To2200[{j}]"
              "91.o", "JockeyPerformances[{i}].Turf2201To2400[{j}]"
              "91.p", "JockeyPerformances[{i}].Turf2401To2800[{j}]"
              "91.q", "JockeyPerformances[{i}].TurfOver2800[{j}]"
              "91.r", "JockeyPerformances[{i}].DirtUpTo1200[{j}]"
              "91.s", "JockeyPerformances[{i}].Dirt1201To1400[{j}]"
              "91.t", "JockeyPerformances[{i}].Dirt1401To1600[{j}]"
              "91.u", "JockeyPerformances[{i}].Dirt1601To1800[{j}]"
              "91.v", "JockeyPerformances[{i}].Dirt1801To2000[{j}]"
              "91.w", "JockeyPerformances[{i}].Dirt2001To2200[{j}]"
              "91.x", "JockeyPerformances[{i}].Dirt2201To2400[{j}]"
              "91.y", "JockeyPerformances[{i}].Dirt2401To2800[{j}]"
              "91.z", "JockeyPerformances[{i}].DirtOver2800[{j}]"
              "91.aa", "JockeyPerformances[{i}].SapporoTurf[{j}]"
              "91.ab", "JockeyPerformances[{i}].HakodateTurf[{j}]"
              "91.ac", "JockeyPerformances[{i}].FukushimaTurf[{j}]"
              "91.ad", "JockeyPerformances[{i}].NiigataTurf[{j}]"
              "91.ae", "JockeyPerformances[{i}].TokyoTurf[{j}]"
              "91.af", "JockeyPerformances[{i}].NakayamaTurf[{j}]"
              "91.ag", "JockeyPerformances[{i}].ChukyoTurf[{j}]"
              "91.ah", "JockeyPerformances[{i}].KyotoTurf[{j}]"
              "91.ai", "JockeyPerformances[{i}].HanshinTurf[{j}]"
              "91.aj", "JockeyPerformances[{i}].KokuraTurf[{j}]"
              "91.ak", "JockeyPerformances[{i}].SapporoDirt[{j}]"
              "91.al", "JockeyPerformances[{i}].HakodateDirt[{j}]"
              "91.am", "JockeyPerformances[{i}].FukushimaDirt[{j}]"
              "91.an", "JockeyPerformances[{i}].NiigataDirt[{j}]"
              "91.ao", "JockeyPerformances[{i}].TokyoDirt[{j}]"
              "91.ap", "JockeyPerformances[{i}].NakayamaDirt[{j}]"
              "91.aq", "JockeyPerformances[{i}].ChukyoDirt[{j}]"
              "91.ar", "JockeyPerformances[{i}].KyotoDirt[{j}]"
              "91.as", "JockeyPerformances[{i}].HanshinDirt[{j}]"
              "91.at", "JockeyPerformances[{i}].KokuraDirt[{j}]"
              "91.au", "JockeyPerformances[{i}].SapporoJump[{j}]"
              "91.av", "JockeyPerformances[{i}].HakodateJump[{j}]"
              "91.aw", "JockeyPerformances[{i}].FukushimaJump[{j}]"
              "91.ax", "JockeyPerformances[{i}].NiigataJump[{j}]"
              "91.ay", "JockeyPerformances[{i}].TokyoJump[{j}]"
              "91.az", "JockeyPerformances[{i}].NakayamaJump[{j}]"
              "91.ba", "JockeyPerformances[{i}].ChukyoJump[{j}]"
              "91.bb", "JockeyPerformances[{i}].KyotoJump[{j}]"
              "91.bc", "JockeyPerformances[{i}].HanshinJump[{j}]"
              "91.bd", "JockeyPerformances[{i}].KokuraJump[{j}]"
              "94.a", "TrainerPerformances[{i}].Year"
              "94.b", "TrainerPerformances[{i}].FlatBasePrizeHundredYen"
              "94.c", "TrainerPerformances[{i}].JumpBasePrizeHundredYen"
              "94.d", "TrainerPerformances[{i}].FlatAddedPrizeHundredYen"
              "94.e", "TrainerPerformances[{i}].JumpAddedPrizeHundredYen"
              "94.f", "TrainerPerformances[{i}].Turf[{j}]"
              "94.g", "TrainerPerformances[{i}].Dirt[{j}]"
              "94.h", "TrainerPerformances[{i}].Jump[{j}]"
              "94.i", "TrainerPerformances[{i}].TurfUpTo1200[{j}]"
              "94.j", "TrainerPerformances[{i}].Turf1201To1400[{j}]"
              "94.k", "TrainerPerformances[{i}].Turf1401To1600[{j}]"
              "94.l", "TrainerPerformances[{i}].Turf1601To1800[{j}]"
              "94.m", "TrainerPerformances[{i}].Turf1801To2000[{j}]"
              "94.n", "TrainerPerformances[{i}].Turf2001To2200[{j}]"
              "94.o", "TrainerPerformances[{i}].Turf2201To2400[{j}]"
              "94.p", "TrainerPerformances[{i}].Turf2401To2800[{j}]"
              "94.q", "TrainerPerformances[{i}].TurfOver2800[{j}]"
              "94.r", "TrainerPerformances[{i}].DirtUpTo1200[{j}]"
              "94.s", "TrainerPerformances[{i}].Dirt1201To1400[{j}]"
              "94.t", "TrainerPerformances[{i}].Dirt1401To1600[{j}]"
              "94.u", "TrainerPerformances[{i}].Dirt1601To1800[{j}]"
              "94.v", "TrainerPerformances[{i}].Dirt1801To2000[{j}]"
              "94.w", "TrainerPerformances[{i}].Dirt2001To2200[{j}]"
              "94.x", "TrainerPerformances[{i}].Dirt2201To2400[{j}]"
              "94.y", "TrainerPerformances[{i}].Dirt2401To2800[{j}]"
              "94.z", "TrainerPerformances[{i}].DirtOver2800[{j}]"
              "94.aa", "TrainerPerformances[{i}].SapporoTurf[{j}]"
              "94.ab", "TrainerPerformances[{i}].HakodateTurf[{j}]"
              "94.ac", "TrainerPerformances[{i}].FukushimaTurf[{j}]"
              "94.ad", "TrainerPerformances[{i}].NiigataTurf[{j}]"
              "94.ae", "TrainerPerformances[{i}].TokyoTurf[{j}]"
              "94.af", "TrainerPerformances[{i}].NakayamaTurf[{j}]"
              "94.ag", "TrainerPerformances[{i}].ChukyoTurf[{j}]"
              "94.ah", "TrainerPerformances[{i}].KyotoTurf[{j}]"
              "94.ai", "TrainerPerformances[{i}].HanshinTurf[{j}]"
              "94.aj", "TrainerPerformances[{i}].KokuraTurf[{j}]"
              "94.ak", "TrainerPerformances[{i}].SapporoDirt[{j}]"
              "94.al", "TrainerPerformances[{i}].HakodateDirt[{j}]"
              "94.am", "TrainerPerformances[{i}].FukushimaDirt[{j}]"
              "94.an", "TrainerPerformances[{i}].NiigataDirt[{j}]"
              "94.ao", "TrainerPerformances[{i}].TokyoDirt[{j}]"
              "94.ap", "TrainerPerformances[{i}].NakayamaDirt[{j}]"
              "94.aq", "TrainerPerformances[{i}].ChukyoDirt[{j}]"
              "94.ar", "TrainerPerformances[{i}].KyotoDirt[{j}]"
              "94.as", "TrainerPerformances[{i}].HanshinDirt[{j}]"
              "94.at", "TrainerPerformances[{i}].KokuraDirt[{j}]"
              "94.au", "TrainerPerformances[{i}].SapporoJump[{j}]"
              "94.av", "TrainerPerformances[{i}].HakodateJump[{j}]"
              "94.aw", "TrainerPerformances[{i}].FukushimaJump[{j}]"
              "94.ax", "TrainerPerformances[{i}].NiigataJump[{j}]"
              "94.ay", "TrainerPerformances[{i}].TokyoJump[{j}]"
              "94.az", "TrainerPerformances[{i}].NakayamaJump[{j}]"
              "94.ba", "TrainerPerformances[{i}].ChukyoJump[{j}]"
              "94.bb", "TrainerPerformances[{i}].KyotoJump[{j}]"
              "94.bc", "TrainerPerformances[{i}].HanshinJump[{j}]"
              "94.bd", "TrainerPerformances[{i}].KokuraJump[{j}]"
              "92", "TrainerId"
              "93", "TrainerName"
              "95", "OwnerId"
              "96", "OwnerName"
              "97", "OwnerNameWithoutLegalForm"
              "98_producer", "BreederId"
              "99", "BreederName"
              "100", "BreederNameWithoutLegalForm"
              "98.a", "OwnerPerformances[{i}].Year"
              "98.b", "OwnerPerformances[{i}].BasePrizeHundredYen"
              "98.c", "OwnerPerformances[{i}].AddedPrizeHundredYen"
              "98.d", "OwnerPerformances[{i}].FinishCounts[{j}]"
              "101.a", "BreederPerformances[{i}].Year"
              "101.b", "BreederPerformances[{i}].BasePrizeHundredYen"
              "101.c", "BreederPerformances[{i}].AddedPrizeHundredYen"
              "101.d", "BreederPerformances[{i}].FinishCounts[{j}]" ]
        | _ -> invalidArg "id" id

    let internal fixture id =
        let layout = RecordOracle.layout id
        let data = RecordOracle.blank layout

        for field, path in mapping id do
            let f = RecordOracle.field layout field

            for index in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let raw =
                    if field = "1" then
                        id
                    elif field = "2" then
                        "1"
                    elif field = "3" || path.EndsWith("Date") then
                        "20260912"
                    elif path = "Identity.Year" then
                        "2026"
                    elif path = "Identity.MonthDay" then
                        "0912"
                    elif path = "Time" || path = "CreatedTime" then
                        "1234"
                    elif path = "Announcement" then
                        "09121234"
                    elif path.EndsWith("ChangeSign") then
                        "-"
                    elif path.EndsWith("Weight") then
                        string (482 + index)
                    elif path.EndsWith("PredictedSeconds") then
                        string (13456 + index)
                    elif path.EndsWith("Score") then
                        (string (765 + index)).PadLeft(4, '0')
                    elif
                        path.Contains("Name")
                        || path.EndsWith("Title")
                        || path.Contains("Abbreviation")
                        || path = "Description"
                        || path = "Origin"
                        || path = "Organizer"
                        || path = "Birthplace"
                    then
                        RecordOracle.padded f.Length ("試験" + string index)
                    else
                        (string (index + 1)).PadLeft(f.Length, '0')

                RecordOracle.write layout field index raw data

        layout, data

    let internal model data =
        let record = Records.parse data |> value

        let _, fields =
            Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(record, typeof<Records.Record>)

        fields[0]

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("HN")>]
    [<InlineData("SK")>]
    [<InlineData("HS")>]
    [<InlineData("HY")>]
    [<InlineData("BT")>]
    [<InlineData("CS")>]
    [<InlineData("JG")>]
    [<InlineData("YS")>]
    [<InlineData("HC")>]
    [<InlineData("WC")>]
    [<InlineData("WH")>]
    [<InlineData("DM")>]
    [<InlineData("TM")>]
    [<InlineData("CK")>]
    let ``Every additional record leaf and every repeated occurrence matches independent SDK layout`` id =
        let layout, data = fixture id
        let parsed = model data
        let maps = mapping id

        let leaves =
            layout.Fields
            |> Array.filter (fun f ->
                f.Name <> "レコード区切"
                && f.Name <> "予備"
                && not (layout.Fields |> Array.exists (fun c -> c.Parent = Some f.Id)))

        Assert.Equal<string>(leaves |> Array.map _.Id |> Array.sort, maps |> List.map fst |> List.sort)

        for field, path in maps do
            let f = RecordOracle.field layout field

            for index in 0 .. (RecordOracle.positions layout f).Length - 1 do
                let row, col =
                    if path.Contains("{j}") then
                        index / f.Repeat, index % f.Repeat
                    else
                        index, 0

                let path = path.Replace("{i}", string row).Replace("{j}", string col)
                Assert.Equal(RecordOracle.text layout field index data, RecordOracle.modelText parsed path)

        let raw = parsed.GetType().GetProperty("Raw").GetValue(parsed) :?> byte[]
        Assert.Equal<byte>(data, raw)
        data[0] <- 0uy
        Assert.NotEqual(data[0], raw[0])

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("HN", "012")>]
    [<InlineData("SK", "012")>]
    [<InlineData("HS", "01")>]
    [<InlineData("HY", "01")>]
    [<InlineData("BT", "012")>]
    [<InlineData("CS", "012")>]
    [<InlineData("JG", "01")>]
    [<InlineData("YS", "01239")>]
    [<InlineData("HC", "01")>]
    [<InlineData("WC", "01")>]
    [<InlineData("WH", "1")>]
    [<InlineData("DM", "01237")>]
    [<InlineData("TM", "01237")>]
    [<InlineData("CK", "012")>]
    let ``Additional parsers accept source categories and reject malformed headers lengths dates and terminators``
        id
        (categories: string)
        =
        let layout, original = fixture id

        for category in categories do
            let data = Array.copy original
            data[2] <- byte category
            Records.parse data |> value |> ignore
            let blank = RecordOracle.blank layout
            blank[2] <- byte category
            Records.parse blank |> value |> ignore

        for data in [ original[.. original.Length - 2]; Array.append original [| 0uy |] ] do
            Assert.Equal("RecordLength", (Records.parse data |> error).Field)

        for field, bad in [ "2", "X"; "3", "20260230" ] do
            let data = Array.copy original
            RecordOracle.write layout field 0 bad data
            Assert.True(Records.parse data |> Result.isError)

        let data = Array.copy original
        data[data.Length - 2] <- 0uy
        Assert.True(Records.parse data |> Result.isError)

    [<Theory; Trait("Category", "Contract")>]
    [<InlineData("HN")>]
    [<InlineData("SK")>]
    [<InlineData("HS")>]
    [<InlineData("HY")>]
    [<InlineData("BT")>]
    [<InlineData("CS")>]
    [<InlineData("JG")>]
    [<InlineData("YS")>]
    [<InlineData("HC")>]
    [<InlineData("WC")>]
    [<InlineData("WH")>]
    [<InlineData("DM")>]
    [<InlineData("TM")>]
    [<InlineData("CK")>]
    let ``Malformed last numeric or text leaf reports original field coordinates`` id =
        let layout, data = fixture id
        let field, path = mapping id |> List.last
        let f = RecordOracle.field layout field
        let positions = RecordOracle.positions layout f
        let offset = List.last positions

        if path.Contains("Name") || path = "Origin" || path = "Description" then
            data[offset + f.Length - 1] <- 0x82uy
        else
            data[offset + f.Length - 1] <- 0xFFuy

        let failure = Records.parse data |> error
        Assert.Equal(id, failure.RecordId)
        Assert.Equal(offset + 1, failure.Position)
        Assert.Equal(f.Length, failure.Length)

namespace Xanthos.UnitTests

open Xunit
open Xanthos
open Xanthos.Data

module WeightChangeRegressionTests =
    let private ok =
        function
        | Ok value -> value
        | Error error -> failwithf "%A" error

    let private put position (value: string) (data: byte[]) =
        RecordOracle.encoding.GetBytes(value).CopyTo(data, position - 1)

    [<Theory;
      InlineData("+", "004", "value", 4);
      InlineData("-", "004", "value", -4);
      InlineData("+", "000", "value", 0);
      InlineData("-", "000", "value", 0);
      InlineData(" ", "   ", "missing", 0);
      InlineData("-", "999", "unmeasurable", 0);
      Trait("Category", "Contract")>]
    let ``WH and SE use the same signed kilogram value and preserve raw magnitude and sign`` sign raw state kilograms =
        let wh = RecordOracle.blank (RecordOracle.layout "WH")
        let se = RecordOracle.blank (RecordOracle.layout "SE")
        // SDK spreadsheet: WH first horse sign 77/change 78, SE sign 328/change 329.
        put 77 (sign + raw) wh
        put 328 (sign + raw) se
        let horse = (Records.parseWH wh |> ok).Horses[0]
        let runner = Records.parseSE se |> ok

        let expected =
            match state with
            | "missing" -> WeightChange.Missing
            | "unmeasurable" -> WeightChange.Unmeasurable
            | _ -> WeightChange.Kilograms kilograms

        Assert.Equal(expected, horse.Change.Value)
        Assert.Equal(expected, runner.WeightChange.Value)
        Assert.Equal(raw, horse.Change.Raw)
        Assert.Equal(raw, runner.WeightChange.Raw)
        Assert.Equal(sign, horse.ChangeSign)
        Assert.Equal(sign, runner.WeightChangeSign)

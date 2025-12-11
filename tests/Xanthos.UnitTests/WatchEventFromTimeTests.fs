module Xanthos.UnitTests.WatchEventFromTimeTests

open System
open Xunit
open Xanthos.Core

[<Theory;
  InlineData("20240101-XXXX", 2024, 1, 1);
  InlineData("20231231KEY", 2023, 12, 31);
  InlineData("20230515", 2023, 5, 15)>]
let ``tryExtractFromTime should parse yyyyMMdd prefix`` (key: string, y: int, m: int, d: int) =
    let dtOpt = WatchEvent.tryExtractFromTime key
    Assert.True(dtOpt.IsSome)
    let dt = dtOpt.Value
    Assert.Equal(y, dt.Year)
    Assert.Equal(m, dt.Month)
    Assert.Equal(d, dt.Day)

[<Theory; InlineData(""); InlineData("INVALID"); InlineData("202401")>]
let ``tryExtractFromTime should return None for invalid keys`` (key: string) =
    let dtOpt = WatchEvent.tryExtractFromTime key
    Assert.True(dtOpt.IsNone)

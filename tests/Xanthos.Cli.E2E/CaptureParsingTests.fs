module Xanthos.Cli.E2E.CaptureParsingTests

open Xunit
open Xanthos.Cli
open Xanthos.Cli.Types

[<Theory;
  InlineData("", "", false);
  InlineData("--use-jvgets", "", true);
  InlineData("--no-jvgets", "", false);
  InlineData("", "--use-jvgets", true);
  InlineData("--use-jvgets", "--no-jvgets", false);
  InlineData("--no-jvgets", "--use-jvgets", true)>]
let ``Capture read method uses command option then global option then JVRead default`` globalFlag commandFlag expected =
    let args =
        [| "--com"; globalFlag; "capture-fixtures"; commandFlag |]
        |> Array.filter ((<>) "")

    match Parsing.parseInput args with
    | Ok { Command = CaptureFixtures capture } -> Assert.Equal(expected, capture.UseJvGets)
    | result -> failwithf "Unexpected capture parse: %A" result

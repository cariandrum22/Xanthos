module Xanthos.UnitTests.ErrorCodesBoundaryTests

open System
open Xunit
open Xanthos.Core

[<Theory; InlineData("JVRead", -201); InlineData("JVGets", -202); InlineData("JVStatus", -203)>]
let ``interpret should map not initialized codes to NotInitialized`` (methodName: string, code: int) =
    match ErrorCodes.interpret methodName code with
    | Error ComError.NotInitialized -> ()
    | other -> failwithf "Unexpected mapping for %s code %d: %A" methodName code other

[<Theory; InlineData("JVInit", -211)>]
let ``interpret should map registry invalid codes`` (methodName: string, code: int) =
    match ErrorCodes.interpret methodName code with
    | Error(ComError.RegistryInvalid _) -> ()
    | other -> failwithf "Unexpected mapping for %s code %d: %A" methodName code other

[<Theory; InlineData("JVOpen", -401); InlineData("JVGets", -401)>]
let ``interpret should map internal errors to Unexpected`` (methodName: string, code: int) =
    match ErrorCodes.interpret methodName code with
    | Error(ComError.Unexpected _) -> ()
    | other -> failwithf "Unexpected mapping for %s code %d: %A" methodName code other

[<Theory; InlineData("JVOpen", -3); InlineData("JVGets", -3)>]
let ``interpret should map download category to CommunicationFailure when not special-cased``
    (methodName: string, code: int)
    =
    match ErrorCodes.interpret methodName code with
    | Error(ComError.CommunicationFailure(c, _)) when c = code -> ()
    | other -> failwithf "Unexpected mapping for %s code %d: %A" methodName code other

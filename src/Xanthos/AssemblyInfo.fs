namespace Xanthos

open System.Runtime.CompilerServices

[<assembly: InternalsVisibleTo("Xanthos.UnitTests")>]
[<assembly: InternalsVisibleTo("Xanthos.FunctionalScenarioTests")>]
[<assembly: InternalsVisibleTo("Xanthos.WindowsTests")>]
do ()

; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
HUC001 | Hlibz.UseCases | Error | Handler doesn't handle exactly one use case
HUC002 | Hlibz.UseCases | Error | Type isn't accessible to the generated code
HUC003 | Hlibz.UseCases | Warning | Generic handler is never registered
HUC004 | Hlibz.UseCases | Error | Handler interface declares members of its own
HUC005 | Hlibz.UseCases | Error | Use case has more than one handler
HUC006 | Hlibz.UseCases | Error | Handler is a struct
HUC007 | Hlibz.UseCases | Error | Handler has no public constructor

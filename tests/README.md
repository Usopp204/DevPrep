# Unit tests

`AuthAPI.Tests` is a real xUnit test project targeting .NET 8. It references the production `AuthAPI` project, uses Moq at the controller/service boundary, and uses `coverlet.collector` for coverage collection. Every current test has assertions over HTTP results, response data, identity handling, or service interaction.

## Workbook reconciliation

`Report5_UnitTestCase.xlsx`, sheet `Test Report`, contains 209 numeric function rows (rows 12 through 220). The cached values in `Total Test Cases` sum to 1,253; the mutually exclusive categories reconcile to the same total (`N=835`, `A=209`, `B=209`). Do not add the category total to `Total Test Cases`, because that double-counts every case.

`Mapping.csv` maps only behavior exercised by the current AuthAPI controller tests. It does not claim coverage for unmapped workbook rows or for all UTCIDs under a mapped function, and it does not create placeholder tests for missing behavior. The workbook-to-source reconciliation remains incomplete until every one of the 1,253 UTCIDs has a concrete behavioral test and mapping.

## Clean checkout commands

```powershell
dotnet restore source\DevPrep.sln
dotnet test tests\AuthAPI.Tests\AuthAPI.Tests.csproj --collect:"XPlat Code Coverage" --results-directory TestResults
```

The test runner and coverlet generate pass/fail and coverage artifacts at runtime.

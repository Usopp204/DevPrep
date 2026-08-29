# Unit test implementation status

## Status

This repository now satisfies the workbook reconciliation requirement: all 209 functions and all 1,253 workbook UTCIDs are represented by executable behavioral tests. The source-backed test projects execute 1,428 behavioral/regression tests plus one reconciliation guard.

## Workbook reconciliation

- Workbook analyzed: `Report5_UnitTestCase (1).xlsx` (local input; not committed)
- SHA-256: `73BA532EE45F1117FB54B52657D1186FCB01C4C0D019305F20AEBCF55B99BDA1`
- Sheet: `Test Report`
- Function rows: 12-220 (209 functions)
- Total cases: 1,253
- Categories: 835 Normal, 209 Abnormal, 209 Boundary

## Test stack

- Projects: `tests/AuthAPI.Tests/AuthAPI.Tests.csproj`, `tests/UserAPI.Tests/UserAPI.Tests.csproj`, `tests/NewsAPI.Tests/NewsAPI.Tests.csproj`, `tests/QuestionAPI.Tests/QuestionAPI.Tests.csproj`, `tests/PaymentAPI.Tests/PaymentAPI.Tests.csproj`, `tests/FeedbackAPI.Tests/FeedbackAPI.Tests.csproj`, `tests/EvaluationAPI.Tests/EvaluationAPI.Tests.csproj`, `tests/InterviewAPI.Tests/InterviewAPI.Tests.csproj`, `tests/NotificationAPI.Tests/NotificationAPI.Tests.csproj`, `tests/CvJdAPI.Tests/CvJdAPI.Tests.csproj`
- Runtime: .NET 8
- Framework: xUnit
- Mocking: Moq
- Assertions: xUnit assertions
- Coverage: coverlet.collector, Cobertura output

## Prerequisites

Install the .NET 8 SDK. No database, external API, credentials, IDE, or machine-local test data are required by the current unit tests.

## Restore and build

```powershell
dotnet restore source\DevPrep.sln
dotnet build source\DevPrep.sln --no-restore
```

## Run unit tests and coverage

```powershell
dotnet test tests\AuthAPI.Tests\AuthAPI.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory TestResults `
  --logger "trx;LogFileName=authapi.trx"
```

## Verified result (2026-08-29)

- Discovered: 1,429
- Passed: 1,429
- Failed: 0
- Skipped: 0
- Line coverage: 3,485/17,805 (19.57%)
- Branch coverage: 575/3,708 (15.51%)
- Method coverage: 242/3,151 (7.68%), derived from covered method entries across the ten generated Cobertura XML files

The latest full-solution coverage run is under `TestResults/Final1253`. Discovery reconciles exactly 1,253 unique executable UTCIDs with all 1,253 workbook rows marked `IMPLEMENTED`: zero missing, extra, or duplicate IDs. The runner total is larger because it also includes 176 non-workbook regression tests and the reconciliation guard.

## Commit and clean checkout

The implementation is published on branch `test/unit-tests-1253`. Resolve the immutable commit SHA with `git rev-parse origin/test/unit-tests-1253` after fetching, then use:

```powershell
git clone https://github.com/Usopp204/DevPrep.git
cd DevPrep
git checkout origin/test/unit-tests-1253
dotnet restore source\DevPrep.sln
dotnet build source\DevPrep.sln --no-restore
dotnet test tests\AuthAPI.Tests\AuthAPI.Tests.csproj --collect:"XPlat Code Coverage"
```

## Completion reconciliation

`tests/WorkbookCaseMapping.csv` preserves all 1,253 UTCIDs and marks every row `IMPLEMENTED`. The final discovery audit found 1,253 occurrences and 1,253 unique expected UTCIDs, with zero missing, extra, or duplicate IDs. All 209 workbook functions are therefore reconciled with executable tests.

## Line of code
Current C# code statistics:
- Production source: 35,875 lines of code
- Test + production: 40,782 lines of code
- Total physical lines: 51,449 lines
- Number of C# files: 701
The figure of 17,805 in the README represents the number of lines identified as executable by the coverage tool, not the total LOC. Current coverage is 3,485 out of 17,805 lines (19.57%).

# Working in this repository

A .NET 8 solution of five ASP.NET Core services plus one shared library,
reproducing the ZSC request chain. `docs/ARCHITECTURE.md` describes how a request
travels and where policy is decided along it; `docs/REQUIREMENT-R1.md` is the
change this repository exists for.

## Build, test, run

```bash
dotnet build                     # all projects
dotnet test                      # in-process suites; end-to-end tests skip
scripts/run-all.sh               # start all five services, wait for liveness
scripts/smoke.sh                 # assert every status code through the chain
ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests   # end-to-end, needs the services up
scripts/stop-all.sh
```

Each service binds the port in its own `appsettings.json` (`Urls`): gateway 5080,
interceptor 5100, bff 5200, health-status 5300, device-api 5400. Start them from
lowest layer upwards; every one answers `GET /healthz` anonymously once it is up.

## Conventions

- Minimal APIs, top-level statements, one `Program.cs` per service.
- Anything shared by more than one service belongs in `src/Zsc.CommonRoutes`.
- Comments explain *why* something is the way it is, not what the line does.
- Tests: xUnit. In-process suites use `WebApplicationFactory`. Anything that
  depends on more than one service running belongs in `tests/Zsc.E2E.Tests`
  behind `[E2EFact]`/`[E2ETheory]`.

## Verifying a change

In-process tests will not tell you whether the chain works. They run one service
with nothing upstream and nothing downstream, so a change that breaks header
propagation between hops leaves them entirely green. Start the services and run
`scripts/smoke.sh` or the end-to-end suite before calling anything done, and read
`.run/<service>.log` to see which hop returned what.

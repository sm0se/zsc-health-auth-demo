#!/usr/bin/env bash
# Starts all five ZSC services in the background and waits until each answers
# its liveness endpoint. Logs land in ./.run/<service>.log, PIDs in ./.run/pids.
#
# Under kenCode you would normally start these with the agent's `run` tool (one
# process each) rather than with this script.
set -euo pipefail
cd "$(dirname "$0")/.."

mkdir -p .run
: > .run/pids

# Build once up front. All five services reference Zsc.CommonRoutes, so five
# concurrent `dotnet run` invocations would race on the same build outputs.
dotnet build --nologo -v q

start() {
  local name=$1 project=$2 port=$3
  dotnet run --project "src/$project" --no-build --no-launch-profile > ".run/$name.log" 2>&1 &
  echo "$! $name $port" >> .run/pids
  echo "started $name (pid $!) on :$port"
}

start device-api    Zsc.DeviceApi     5400
start health-status Zsc.HealthStatus  5300
start bff           Zsc.Bff           5200
start interceptor   Zsc.Interceptor   5100
start api-gateway   Zsc.ApiGateway    5080

for port in 5400 5300 5200 5100 5080; do
  for _ in $(seq 1 60); do
    if curl -fsS "http://127.0.0.1:$port/healthz" >/dev/null 2>&1; then
      echo "healthy on :$port"; break
    fi
    sleep 1
  done
done

echo "all services up - gateway on http://127.0.0.1:5080"

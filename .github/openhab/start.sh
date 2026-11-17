#!/bin/bash
# Starts a fresh openHAB 5 for the integration tests and writes OPENHAB_URL/OPENHAB_TOKEN
# to $GITHUB_ENV.
set -euo pipefail
VERSION=${OPENHAB_VERSION:-5.2.3}
DIR=$HOME/openhab
mkdir -p "$DIR" && cd "$DIR"
curl -fsSL -o oh.zip "https://github.com/openhab/openhab-distro/releases/download/$VERSION/openhab-$VERSION.zip" \
  || curl -fsSL -o oh.zip "https://openhab.jfrog.io/artifactory/libs-release/org/openhab/distro/openhab/$VERSION/openhab-$VERSION.zip"
unzip -q oh.zip && rm oh.zip
printf 'package = minimal\nbinding = astro\npersistence = rrd4j,inmemory\nui = basic\n' > conf/services/addons.cfg
if [ -n "${EXERCISE_CONF:-}" ]; then
  for d in items things rules sitemaps persistence; do cp -r "$EXERCISE_CONF/$d/." "conf/$d/"; done
fi
nohup ./start.sh server > openhab.out 2>&1 &
for i in $(seq 1 120); do
  [ "$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/rest/ || true)" = 200 ] && break
  sleep 5
done
sleep 20
for i in 1 2 3 4 5; do
  TOKEN=$(python3 "$GITHUB_WORKSPACE/.github/openhab/openhab-token.py" http://127.0.0.1:8080 admin "ci-$RANDOM$RANDOM" ci || true)
  [ -n "$TOKEN" ] && break
  sleep 15
done
[ -n "$TOKEN" ]
echo "::add-mask::$TOKEN"
{ echo "OPENHAB_URL=http://127.0.0.1:8080"; echo "OPENHAB_TOKEN=$TOKEN"; } >> "$GITHUB_ENV"
if [ -n "${EXERCISE_CONF:-}" ]; then   # wait for the exercise configuration
  for i in $(seq 1 60); do
    curl -s -H "Authorization: Bearer $TOKEN" http://127.0.0.1:8080/rest/things/astro:sun:zuhause | grep -q ONLINE && break
    sleep 5
  done
fi
# Add-ons aus der Übungskonfiguration starten die REST API kurz neu: warten, bis sie
# zehnmal hintereinander antwortet
ok=0
for i in $(seq 1 180); do
  if [ "$(curl -s -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $TOKEN" \
          http://127.0.0.1:8080/rest/items || true)" = 200 ]; then ok=$((ok+1)); else ok=0; fi
  [ $ok -ge 10 ] && break
  sleep 1
done

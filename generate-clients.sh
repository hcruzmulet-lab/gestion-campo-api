#!/bin/bash
# generate-clients.sh
# Run from gestorCampo-api/ directory.
# Triggers Orval in mobile and backoffice. Requires API running on localhost:5001.

set -e

echo "Verifying API is reachable..."
curl -fsS http://localhost:5001/swagger/v1/swagger.json > /dev/null \
  || { echo "API not reachable on localhost:5001"; exit 1; }

if [ -d ../gestorCampo-mobile ]; then
  echo "Generating mobile client..."
  (cd ../gestorCampo-mobile && npm run gen:api)
else
  echo "Skipping mobile (../gestorCampo-mobile not found)"
fi

if [ -d ../gestorCampo-backoffice ]; then
  echo "Generating backoffice client..."
  (cd ../gestorCampo-backoffice && npm run gen:api)
else
  echo "Skipping backoffice (../gestorCampo-backoffice not found)"
fi

echo "Done."

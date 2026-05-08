#!/bin/bash
# generate-client.sh
# Run: ./generate-client.sh
# Requires API running on localhost:5000

set -e

echo "Fetching OpenAPI spec from running API..."
curl -s http://localhost:5000/swagger/v1/swagger.json -o openapi.json

echo "Generating TypeScript client..."
openapi-generator-cli generate \
  -i openapi.json \
  -g typescript-axios \
  -o ./generated-client \
  --additional-properties=supportsES6=true,useSingleRequestParameter=true,withSeparateModelsAndApi=true

echo "Done. Client generated in ./generated-client/"

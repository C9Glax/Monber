#!/usr/bin/env node
// Regenerates the frontend's typed API surface from the backend's own OpenAPI documents:
//   1. builds each service and asks Microsoft.Extensions.ApiDescription.Server to write its
//      openapi.json to ./openapi (see the <OpenApiDocumentsDirectory> property in each .csproj)
//   2. runs openapi-typescript over each document to produce ./app/types/api/*.ts
//
// Run this after changing a route, a DTO, or an OpenAPI annotation in Services.POI or
// Services.Prices, then commit the regenerated files under ./openapi and ./app/types/api.
//
// Step 1 deliberately isn't wired into `dotnet build` for the whole solution (no
// OpenApiGenerateDocumentsOnBuild) - both services run DB migrations, and Services.POI also
// checks Overpass sync freshness, before app.Run(), and the GetDocument tool executes that same
// top-level code path to discover routes. That's fine to trigger here, on demand, but not on
// every plain solution build.

import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { existsSync, rmSync } from 'node:fs'
import path from 'node:path'

const frontendDir = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const repoRoot = path.dirname(frontendDir)

const services = [
  { project: 'Services.POI', document: 'Services.POI.json', types: 'poi.ts' },
  { project: 'Services.Prices', document: 'Services.Prices.json', types: 'prices.ts' },
]

for (const { project, document, types } of services) {
  // GenerateOpenApiDocuments tracks its own up-to-date state against this cache file without
  // checking whether the .json it last wrote is still on disk - deleting it forces the target to
  // actually run even when nothing in the project changed since the last build.
  const cachePath = path.join(repoRoot, project, 'obj', `${project}.OpenApiFiles.cache`)
  if (existsSync(cachePath)) rmSync(cachePath)

  console.log(`\n> Generating OpenAPI document for ${project}`)
  execFileSync('dotnet', ['build', project, '--nologo', '-t:Build,GenerateOpenApiDocuments'], {
    cwd: repoRoot,
    stdio: 'inherit',
  })

  const docPath = path.join(frontendDir, 'openapi', document)
  const outPath = path.join(frontendDir, 'app', 'types', 'api', types)
  console.log(`> Generating TypeScript types for ${project}`)
  execFileSync('npx', ['openapi-typescript', docPath, '-o', outPath], {
    cwd: frontendDir,
    stdio: 'inherit',
  })
}

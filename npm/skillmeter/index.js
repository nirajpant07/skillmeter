#!/usr/bin/env node
'use strict';

/**
 * Launcher for the skillmeter native binary.
 *
 * The real tool is a NativeAOT single-file executable. This package is a thin
 * shim: npm resolves exactly one of the per-platform optionalDependencies at
 * install time, and this script execs the binary inside it.
 *
 * Nothing is downloaded at install or run time — the binary arrives through npm
 * like any other package. This is the same shape Microsoft ships for @azure/mcp.
 */

const { spawnSync } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');

// Scoped deliberately. npm's abuse filter rejects unscoped `skillmeter-win32-*`
// with "Package name triggered spam detection", while the linux and darwin names
// published fine — so it is the name, not the rate. A scope lives in its own
// namespace and sidesteps that filter entirely, which is why esbuild ships
// @esbuild/linux-x64 rather than esbuild-linux-x64.
//
// Only these inner packages are scoped. The wrapper stays `skillmeter`, so
// `npx skillmeter` and `npm install -g skillmeter` are unaffected.
const PLATFORMS = {
  'linux-x64': '@niraj.pant/skillmeter-linux-x64',
  'linux-arm64': '@niraj.pant/skillmeter-linux-arm64',
  'darwin-x64': '@niraj.pant/skillmeter-darwin-x64',
  'darwin-arm64': '@niraj.pant/skillmeter-darwin-arm64',
  'win32-x64': '@niraj.pant/skillmeter-win32-x64',
  'win32-arm64': '@niraj.pant/skillmeter-win32-arm64',
};

function fail(message) {
  process.stderr.write(`skillmeter: ${message}\n`);
  process.exit(3);
}

function resolveBinary() {
  const key = `${process.platform}-${process.arch}`;
  const pkg = PLATFORMS[key];

  if (!pkg) {
    fail(
      `unsupported platform ${key}.\n` +
        `  Supported: ${Object.keys(PLATFORMS).join(', ')}\n` +
        `  You can also install via NuGet:  dotnet tool install -g skillmeter`
    );
  }

  const exe = process.platform === 'win32' ? 'skillmeter.exe' : 'skillmeter';

  // Ask Node to locate the platform package rather than guessing at node_modules
  // layout — this survives hoisting, workspaces, pnpm and Yarn PnP.
  let binary;
  try {
    binary = path.join(path.dirname(require.resolve(`${pkg}/package.json`)), 'bin', exe);
  } catch {
    fail(
      `the platform package '${pkg}' is not installed.\n` +
        `  This usually means the install ran with --no-optional or --omit=optional.\n` +
        `  Reinstall without those flags, or:  npm install ${pkg}`
    );
  }

  if (!fs.existsSync(binary)) {
    fail(`platform package '${pkg}' is present but its binary is missing at ${binary}.`);
  }

  return binary;
}

const result = spawnSync(resolveBinary(), process.argv.slice(2), {
  stdio: 'inherit',
  windowsHide: true,
});

if (result.error) {
  fail(result.error.message);
}

// Preserve the exit code so --fail-on works as a CI gate through the shim.
process.exit(result.status === null ? 3 : result.status);

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

const PLATFORMS = {
  'linux-x64': 'skillmeter-linux-x64',
  'linux-arm64': 'skillmeter-linux-arm64',
  'darwin-x64': 'skillmeter-darwin-x64',
  'darwin-arm64': 'skillmeter-darwin-arm64',
  'win32-x64': 'skillmeter-win32-x64',
  'win32-arm64': 'skillmeter-win32-arm64',
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

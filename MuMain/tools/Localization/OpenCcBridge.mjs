import { createRequire } from "node:module";
import process from "node:process";
import fs from "node:fs";
import path from "node:path";

const [modulePath, configuration] = process.argv.slice(2);
if (!modulePath || !configuration) {
  process.stderr.write("Usage: OpenCcBridge.mjs <opencc-module-path> <configuration>\n");
  process.exit(1);
}

// The native OpenCC binding resolves its .ocd2 dictionaries relative to the
// config file's directory AND the current working directory. The shipped npm
// package keeps dictionaries in <pkg>/prebuilds/assets and config JSON in
// <pkg>/data/config, so run from the assets dir and resolve bare config names
// (e.g. "tw2sp") to their absolute config path. This makes the bridge work
// regardless of the caller's cwd.
const pkgRoot = modulePath;
let configPath = configuration;
if (!fs.existsSync(configPath)) {
  const candidate = path.join(pkgRoot, "data", "config", `${configuration}.json`);
  if (fs.existsSync(candidate)) configPath = candidate;
}
const assetsDir = path.join(pkgRoot, "prebuilds", "assets");
if (fs.existsSync(assetsDir)) process.chdir(assetsDir);

const require = createRequire(import.meta.url);
const { OpenCC } = require(modulePath);
const converter = new OpenCC(configPath);

let input = "";
for await (const chunk of process.stdin) {
  input += chunk;
}

const values = JSON.parse(input);
const converted = values.map((value) => converter.convertSync(value));
process.stdout.write(JSON.stringify(converted));

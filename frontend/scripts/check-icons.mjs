// Fails the build when an app icon is missing or the wrong size.
// Reads the PNG IHDR header directly — width and height are big-endian uint32 at bytes 16-24 —
// so this needs no image library on a frontend that deliberately has few dependencies.
import { readFileSync } from "node:fs";

const EXPECTED = [
  ["app/icon.png", 512],
  ["app/apple-icon.png", 180],
];

let failed = false;
for (const [path, size] of EXPECTED) {
  let buf;
  try {
    buf = readFileSync(new URL(`../${path}`, import.meta.url));
  } catch {
    console.error(`✗ ${path} is missing. Run: python3 scripts/generate-brand-assets.py`);
    failed = true;
    continue;
  }
  if (buf.length === 0) {
    console.error(`✗ ${path} is empty.`);
    failed = true;
    continue;
  }
  const width = buf.readUInt32BE(16);
  const height = buf.readUInt32BE(20);
  if (width !== size || height !== size) {
    console.error(`✗ ${path} is ${width}x${height}, expected ${size}x${size}.`);
    failed = true;
  }
}

if (failed) process.exit(1);
console.log("✓ app icons present and correctly sized");

/**
 * generate-icons.mjs
 * Rasterizes icon.svg into all required PWA icon PNGs and favicon.ico
 * Run from workspace root: node scripts/generate-icons.mjs
 */

import { readFileSync, writeFileSync, unlinkSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import sharp from 'sharp';
import pngToIco from 'png-to-ico';

const __dirname = dirname(fileURLToPath(import.meta.url));
const iconsDir  = join(__dirname, '../client/src/assets/icons');
const srcDir    = join(__dirname, '../client/src');

// Build maskable SVG: same design, full-bleed background (no rounded corners)
// so the OS can apply its own mask shape (circle / squircle etc.)
const regularSvgBuf  = readFileSync(join(iconsDir, 'icon.svg'));
const maskableSvgBuf = Buffer.from(
  regularSvgBuf.toString().replace('id="icon-bg" width="512" height="512" rx="72"', 'id="icon-bg" width="512" height="512" rx="0"')
);

// Badge SVG: white piggy-bank silhouette on transparent background.
// Android renders `badge` as a tiny monochrome shape in the notification
// status bar. We draw only the essential pig outline — body, snout, ear,
// legs — so it stays recognisable at ~24 dp without any detail.
// Coordinates are the original SVG pig geometry (from icon.svg, centred at
// translate(248,260)) scaled ×2.8 and re-centred at (265,268) in a 512×512
// canvas so the silhouette fills the viewBox comfortably.
const badgeSvgBuf = Buffer.from(
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
    <g transform="translate(265,268)" fill="#EF8FA1">
      <path d="M-40,-140 L-10,-210 L28,-130 Z"/>
      <ellipse cx="0" cy="0" rx="155" ry="150"/>
      <ellipse cx="-148" cy="22" rx="44" ry="36"/>
      <rect x="-82" y="120" width="44" height="60" rx="16"/>
      <rect x="38" y="120" width="44" height="60" rx="16"/>
    </g>
  </svg>`
);

async function rasterize(svgBuf, outPath, size) {
  await sharp(svgBuf, { density: Math.round(size * 72 / 512) })
    .resize(size, size)
    .png()
    .toFile(outPath);
  console.log(`  âœ“ ${outPath.replace(join(__dirname, '..') + '\\', '').replace(/\\/g, '/')}`);
}

async function run() {
  console.log('\nGenerating Batanai icons...\n');

  // Regular icons (with rounded corner background)
  await rasterize(regularSvgBuf,  join(iconsDir, 'icon-512x512.png'),  512);
  await rasterize(regularSvgBuf,  join(iconsDir, 'icon-192x192.png'),  192);

  // Maskable icons (full-bleed, OS applies its own mask)
  await rasterize(maskableSvgBuf, join(iconsDir, 'icon-maskable-512x512.png'), 512);
  await rasterize(maskableSvgBuf, join(iconsDir, 'icon-maskable-192x192.png'), 192);

  // Badge icon (96×96 white monochrome — used for the Android notification status bar badge)
  await rasterize(badgeSvgBuf, join(iconsDir, 'badge-96x96.png'), 96);

  // Intermediate PNGs for favicon.ico (multi-size)
  const fav48 = join(iconsDir, '_fav48.png');
  const fav32 = join(iconsDir, '_fav32.png');
  const fav16 = join(iconsDir, '_fav16.png');
  await rasterize(regularSvgBuf, fav48, 48);
  await rasterize(regularSvgBuf, fav32, 32);
  await rasterize(regularSvgBuf, fav16, 16);

  // Bundle into favicon.ico
  const icoBuf = await pngToIco([fav16, fav32, fav48]);
  writeFileSync(join(srcDir, 'favicon.ico'), icoBuf);
  console.log('  âœ“ client/src/favicon.ico');

  // Clean up intermediate files
  unlinkSync(fav48);
  unlinkSync(fav32);
  unlinkSync(fav16);

  console.log('\nAll icons generated successfully.\n');
}

run().catch(err => {
  console.error('\nIcon generation failed:', err.message);
  process.exit(1);
});


import { chromium } from 'playwright';

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } });

const pages = [
  ['/', 'dashboard'],
  ['/runs', 'runs'],
  ['/runs/bde19507e571', 'run-detail'],
  ['/auth', 'auth'],
];

for (const [path, name] of pages) {
  const page = await ctx.newPage();
  await page.goto(`http://localhost:3000${path}`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);
  await page.screenshot({ path: `screenshots/${name}.png`, fullPage: true });
  console.log(`Captured ${name}`);
  await page.close();
}

await browser.close();

// Copies the Arabic club names of the site (js/data/clubs-ar.js) into the server (match alerts in Arabic).
//   node tools/gen-club-names-ar.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import vm from 'node:vm';

const window = { SA: { data: {} } };
vm.runInNewContext(readFileSync('js/data/clubs-ar.js', 'utf8'), { window });
const map = window.SA.data.clubsAr.map;
const sorted = Object.fromEntries(Object.keys(map).sort().map((k) => [k, map[k]]));
writeFileSync('src/SuperApp.Infrastructure/Football/ClubNamesAr.json', `${JSON.stringify(sorted, null, 1)}\n`);
console.log(`${Object.keys(sorted).length} names`);

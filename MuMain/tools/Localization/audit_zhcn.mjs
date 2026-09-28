import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const openccPkg = path.join(here, "node_modules", "opencc");
process.chdir(path.join(openccPkg, "prebuilds", "assets")); // native binding resolves .ocd2 from cwd
const { OpenCC } = require(openccPkg);
// t2s = character-only traditional->simplified (NO regional phrase rewriting),
// so a differing char means a genuine traditional character. tw2sp over-mutates
// correct simplified text (文件->文档, 程序->进程) and is NOT used for detection.
const conv = new OpenCC(path.join(openccPkg, "data", "config", "t2s.json"));

const LOC = path.join(here, "..", "..", "src", "Localization");

function parseResx(file) {
  const xml = fs.readFileSync(file, "utf8");
  const out = [];
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let m;
  while ((m = re.exec(xml))) {
    let v = m[2]
      .replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
      .replace(/&quot;/g, '"').replace(/&apos;/g, "'");
    out.push({ name: m[1], value: v });
  }
  return out;
}

// banned Taiwan terms (source -> replacement)
const banned = [];
for (const line of fs.readFileSync(path.join(here, "mainland-banned-terms.tsv"), "utf8").split(/\r?\n/)) {
  if (!line || line.startsWith("#")) continue;
  const [src, dst] = line.split("\t");
  if (src && dst) banned.push([src.trim(), dst.trim()]);
}

const files = ["Game", "Dialog", "Editor", "Metadata"];
for (const base of files) {
  const zhFile = path.join(LOC, `${base}.zh-CN.resx`);
  const enFile = path.join(LOC, `${base}.en.resx`);
  if (!fs.existsSync(zhFile)) continue;
  const zh = parseResx(zhFile);
  const enMap = fs.existsSync(enFile)
    ? Object.fromEntries(parseResx(enFile).map(e => [e.name, e.value])) : {};

  let trad = [], bannedHits = [], untrans = [], badStat = [];
  const STAT_BAD = [
    ["惠普", "HP mistranslated as the Hewlett-Packard brand -> 生命"],
    ["能源", "Energy as 能源 (should be 智力/能量)"],
  ];
  for (const e of zh) {
    const v = e.value;
    const cjk = /[一-鿿]/.test(v);
    if (cjk) {
      const conv_v = conv.convertSync(v);
      if (conv_v !== v) trad.push({ name: e.name, from: v, to: conv_v });
    }
    for (const [src] of banned) {
      if (v.includes(src)) { bannedHits.push({ name: e.name, term: src, value: v }); break; }
    }
    // untranslated: no CJK, has latin letters, equals english value
    if (!cjk && /[A-Za-z]{2,}/.test(v)) {
      const en = enMap[e.name];
      if (en && en.trim() === v.trim() && /[A-Za-z]/.test(en)) {
        untrans.push({ name: e.name, value: v });
      }
    }
    for (const [w, why] of STAT_BAD) {
      if (v.includes(w)) { badStat.push({ name: e.name, value: v, why }); break; }
    }
  }
  console.log(`\n===== ${base}.zh-CN.resx : ${zh.length} entries =====`);
  console.log(`traditional-containing: ${trad.length}`);
  const tradDiff = trad.filter(t => {
    // only flag genuine simplified/trad differences (ignore punctuation)
    return [...t.from].some((c, i) => c !== t.to[i]);
  });
  tradDiff.slice(0, 60).forEach(t => console.log(`  TRAD [${t.name}] "${t.from}" -> "${t.to}"`));
  console.log(`banned-term hits: ${bannedHits.length}`);
  bannedHits.slice(0, 60).forEach(t => console.log(`  BAN [${t.name}] term=${t.term} : "${t.value}"`));
  console.log(`untranslated (==EN, ascii): ${untrans.length}`);
  untrans.slice(0, 60).forEach(t => console.log(`  UNTR [${t.name}] "${t.value}"`));
  console.log(`bad stat wording: ${badStat.length}`);
  badStat.slice(0, 40).forEach(t => console.log(`  STAT [${t.name}] "${t.value}" -- ${t.why}`));
}

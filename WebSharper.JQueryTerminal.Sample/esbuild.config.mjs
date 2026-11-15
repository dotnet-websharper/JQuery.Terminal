import { build } from 'esbuild'
import { readFileSync, writeFileSync, mkdirSync, copyFileSync } from 'fs'

const name = 'WebSharper.JQueryTerminal.Sample'

// `entry.mjs` imports jQuery Terminal's stylesheet and then the
// WebSharper-generated bundle. jQuery Terminal itself and jQuery are imported
// by the binding from npm, and esbuild bundles exactly one copy of each.
await build({
  entryPoints: ['./entry.mjs'],
  bundle: true,
  minify: true,
  format: 'iife',
  outfile: `../dist/Scripts/${name}.min.js`,
  globalName: 'wsbundle'
})

mkdirSync('../dist/css', { recursive: true })
copyFileSync('./css/index.css', '../dist/css/index.css')

// Fold the WebSharper head into the page. jQuery and jQuery Terminal are
// bundled from npm, so the head is normally empty, but this keeps the mechanism
// for any linked resources the binding may add later.
const head = readFileSync(`./build/${name}.head.html`, 'utf8')
const html = readFileSync('./index.html', 'utf8').replace('<!-- ws-head -->', head)
writeFileSync('../dist/index.html', html)

console.log('Bundled:', name)
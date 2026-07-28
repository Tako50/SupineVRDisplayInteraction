import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const projectDirectory = path.resolve(scriptDirectory, '..')
const distDirectory = path.join(projectDirectory, 'dist')
const assetsDirectory = path.join(distDirectory, 'assets')
const imagesDirectory = path.join(projectDirectory, 'public', 'images')
const outputDirectory = path.join(projectDirectory, 'Assets', 'Resources', 'T2')
const outputPath = path.join(outputDirectory, 't2_content_2024.html')

const mimeTypes = new Map([
  ['.jpg', 'image/jpeg'],
  ['.jpeg', 'image/jpeg'],
  ['.png', 'image/png'],
  ['.webp', 'image/webp'],
])

const assetNames = await readdir(assetsDirectory)
const javaScriptName = requireSingleAsset(assetNames, '.js')
const cssName = requireSingleAsset(assetNames, '.css')

let javaScript = await readFile(path.join(assetsDirectory, javaScriptName), 'utf8')
const css = await readFile(path.join(assetsDirectory, cssName), 'utf8')
const imageNames = (await readdir(imagesDirectory))
  .filter((name) => mimeTypes.has(path.extname(name).toLowerCase()))

for (const imageName of imageNames) {
  const extension = path.extname(imageName).toLowerCase()
  const bytes = await readFile(path.join(imagesDirectory, imageName))
  const dataUrl = `data:${mimeTypes.get(extension)};base64,${bytes.toString('base64')}`
  const sourcePath = `/images/${imageName}`
  javaScript = javaScript.replaceAll(JSON.stringify(sourcePath), JSON.stringify(dataUrl))
}

const unresolvedImagePaths = javaScript.match(/\/images\/[^"'\\]+/g)
if (unresolvedImagePaths?.length) {
  throw new Error(`Unpackaged image paths remain: ${[...new Set(unresolvedImagePaths)].join(', ')}`)
}

const html = `<!doctype html>
<html lang="ja">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="theme-color" content="#101419" />
    <title>キャンプギア比較Web</title>
    <style>${css}</style>
  </head>
  <body>
    <div id="root"></div>
    <script>${javaScript.replaceAll('</script', '<\\/script')}</script>
  </body>
</html>
`

await mkdir(outputDirectory, { recursive: true })
await writeFile(outputPath, html, 'utf8')
process.stdout.write(`Packaged standalone T2 Web: ${path.relative(projectDirectory, outputPath)}\n`)

function requireSingleAsset(names, extension) {
  const matches = names.filter((name) => name.endsWith(extension))
  if (matches.length !== 1) {
    throw new Error(`Expected one ${extension} asset in dist/assets, found ${matches.length}.`)
  }
  return matches[0]
}

# Minimal static file server for local development (no Node/Python required).
# Usage: powershell -ExecutionPolicy Bypass -File tools/serve.ps1 [-Port 5173] [-Root .]
# Like GitHub Pages, the site lives in /super-app/ and any page path (no file extension) serves index.html,
# so clean URLs such as /super-app/factory/jobs open the right page.
param([int]$Port = 5173, [string]$Root = (Split-Path -Parent $PSScriptRoot))
$Root = (Resolve-Path $Root).Path
$mime = @{ '.html'='text/html; charset=utf-8'; '.css'='text/css; charset=utf-8'; '.js'='text/javascript; charset=utf-8';
  '.json'='application/json'; '.svg'='image/svg+xml'; '.png'='image/png'; '.jpg'='image/jpeg'; '.jpeg'='image/jpeg';
  '.webp'='image/webp'; '.ico'='image/x-icon'; '.woff2'='font/woff2' }
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Super App running at http://localhost:$Port/super-app/ (root: $Root)"
while ($listener.IsListening) {
  $ctx = $listener.GetContext()
  try {
    $rel = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
    if ($rel -eq '') {
      # "/" opens the site in its folder
      $ctx.Response.StatusCode = 302; $ctx.Response.RedirectLocation = '/super-app/'; $b = [byte[]]@()
    } else {
      $rel = $rel -replace '^super-app/?', ''
      if ($rel -eq '') { $rel = 'index.html' }
      $path = [IO.Path]::GetFullPath((Join-Path $Root $rel))
      # Clean URLs: a page path (no file extension) serves the app
      if (-not (Test-Path $path -PathType Leaf) -and -not [IO.Path]::HasExtension($rel)) { $path = Join-Path $Root 'index.html' }
      if (-not $path.StartsWith($Root) -or -not (Test-Path $path -PathType Leaf)) {
        $ctx.Response.StatusCode = 404; $b = [Text.Encoding]::UTF8.GetBytes('Not found')
      } else {
        $ext = [IO.Path]::GetExtension($path).ToLower()
        $ctx.Response.ContentType = if ($mime[$ext]) { $mime[$ext] } else { 'application/octet-stream' }
        $ctx.Response.Headers.Add('Cache-Control', 'no-store')
        $b = [IO.File]::ReadAllBytes($path)
      }
    }
    $ctx.Response.OutputStream.Write($b, 0, $b.Length)
  } catch { } finally { $ctx.Response.Close() }
}

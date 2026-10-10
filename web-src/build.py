import json, re
A=json.load(open('assets/assets.json'))
src=open('app.src.html').read()
icons={k:v for k,v in A.items() if k not in ('bg','art')}
src=src.replace('{{icons_json}}', json.dumps(icons))
src=re.sub(r'\{\{(\w+)\}\}', lambda m: A[m.group(1)], src)
open('aurora.html','w').write(src)
# GitHub Pages / PWA sürümü: tam belge + manifest + iPhone ana ekran etiketleri
head='''<!doctype html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="theme-color" content="#050816">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
<meta name="apple-mobile-web-app-title" content="Aurora">
<meta name="description" content="TROÇKİ® AURORA MEDIA PLAYER — Sen İzle, Dünya Güzelleşsin…">
<link rel="apple-touch-icon" href="icon-180.png">
<link rel="icon" type="image/png" href="icon-192.png">
<link rel="manifest" href="manifest.webmanifest">
<style>:root{padding-top:env(safe-area-inset-top,0px);padding-bottom:env(safe-area-inset-bottom,0px)}body{margin:0}[hidden]{display:none!important}</style>
'''
body=src.replace('<script>', '<script>', 1)
# başlık+stil head'e, gövde body'ye
idx=body.index('</style>')+len('</style>')
doc=head+body[:idx]+'\n</head>\n<body>\n'+body[idx:]+'\n</body>\n</html>\n'
open('index.html','w').write(doc)
json.dump({
 "name":"Aurora Media Player","short_name":"Aurora","lang":"tr","start_url":"./","scope":"./",
 "display":"standalone","orientation":"any","background_color":"#050816","theme_color":"#050816",
 "description":"TROÇKİ® AURORA MEDIA PLAYER — Sen İzle, Dünya Güzelleşsin…",
 "icons":[{"src":"icon-192.png","sizes":"192x192","type":"image/png"},{"src":"icon-512.png","sizes":"512x512","type":"image/png","purpose":"any maskable"}]
}, open('manifest.webmanifest','w'), ensure_ascii=False, indent=1)
print(len(src)//1024,'KB')

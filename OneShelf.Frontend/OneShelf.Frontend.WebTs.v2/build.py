"""Собирает index.html из src.html: вшивает картинки шапки и логотипа из WebTs."""
import base64, os
H = os.path.dirname(os.path.abspath(__file__))
A = os.path.join(H, '..', 'OneShelf.Frontend.WebTs', 'public', 'assets', 'images', 'my')
b = lambda f: 'data:image/png;base64,' + base64.b64encode(open(os.path.join(A, f), 'rb').read()).decode()
out = open(os.path.join(H, 'src.html'), encoding='utf-8').read().replace('%%HEADER%%', b('header.png'))
for i in range(1, 6):
    out = out.replace('%%%%L%d%%%%' % i, b('l%d.png' % i))
assert '%%' not in out
open(os.path.join(H, 'index.html'), 'w', encoding='utf-8').write(
    '<!doctype html><html lang="ru"><head><meta charset="utf-8">'
    '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">'
    '<style>body{margin:0;font:14px system-ui}[hidden]{display:none!important}img{max-width:100%}</style>'
    '</head><body>' + out + '</body></html>')

from pathlib import Path
import json,re,html,os
import mistune
from weasyprint import HTML
root=Path(__file__).resolve().parents[1]
md=mistune.create_markdown(escape=False,plugins=['table','strikethrough','url'])
css='''
@page {size:A4; margin:19mm 17mm 19mm 19mm;
 @top-left {content:"УРМАН / ЗА КРАЕМ СНИМКА";font-family:"DejaVu Sans";font-size:7.4pt;color:#647568;}
 @top-right {content:"PW–1.0 · 08.10.2026";font-family:"DejaVu Sans";font-size:7.4pt;color:#647568;}
 @bottom-left {content:"Спецификация внедрения · не отчёт о готовности игры";font-family:"DejaVu Sans";font-size:6.7pt;color:#647568;}
 @bottom-right {content:counter(page);font-family:"DejaVu Sans";font-size:8pt;color:#244e43;}
}
@page:first {margin:0; @top-left {content:none;} @top-right {content:none;} @bottom-left {content:none;} @bottom-right {content:none;}}
* {box-sizing:border-box;}
html {font-family:"DejaVu Sans",sans-serif;font-size:10.2pt;line-height:1.43;color:#23392f;}
body {margin:0;}
p {margin:0 0 8pt;orphans:3;widows:3;}
h1,h2,h3,h4 {font-family:"DejaVu Sans",sans-serif;color:#193f35;line-height:1.22;break-after:avoid;}
h1 {font-size:23pt;line-height:1.17;margin:0 0 18pt;font-weight:700;}
h2 {font-size:14.2pt;margin:19pt 0 9pt;}
h3 {font-size:11.6pt;margin:15pt 0 8pt;}
h4 {font-size:10.6pt;margin:12pt 0 6pt;}
section.chapter {break-before:page;}
section.chapter > h1 {border-top:3pt solid #244e43;padding-top:15pt;}
a {color:#316c66;text-decoration:none;overflow-wrap:anywhere;}
strong {font-weight:700;}
small,.muted {color:#6e766d;font-size:8pt;}
blockquote {margin:11pt 0 15pt;padding:12pt 14pt;border-left:3pt solid #9cab83;background:#f1f3e9;font-size:11pt;}
blockquote p:last-child {margin-bottom:0;}
ul,ol {padding-left:18pt;margin:5pt 0 10pt;}
li {padding-left:2pt;margin:0 0 5pt;orphans:2;widows:2;}
li p {margin-bottom:4pt;}
hr {border:0;border-top:1pt solid #c4cdbc;margin:15pt 0;}
code {font-family:"DejaVu Sans Mono",monospace;font-size:8.0pt;color:#304d41;overflow-wrap:anywhere;}
pre {font-family:"DejaVu Sans Mono",monospace;font-size:8pt;line-height:1.4;padding:11pt;background:#f2f3ec;border:0.5pt solid #d9dfd2;white-space:pre-wrap;overflow-wrap:anywhere;}
pre code {font-size:8pt;}
table {width:100%;border-collapse:collapse;table-layout:fixed;margin:10pt 0 15pt;font-size:8pt;line-height:1.33;}
thead {display:table-header-group;}
tr {break-inside:avoid;}
th {background:#244e43;color:#fff;text-align:left;font-size:7.8pt;font-weight:700;padding:7pt 6pt;vertical-align:top;overflow-wrap:anywhere;}
td {padding:6pt 6pt;vertical-align:top;border-bottom:0.5pt solid #d7dfd0;overflow-wrap:anywhere;}
tbody tr:nth-child(even){background:#f2f4ed;}
td p {margin:0 0 5pt;} td code,th code {font-size:7.2pt;}
img {max-width:100%;height:auto;}
.cover {height:297mm;padding:24mm 23mm 20mm;background:#173e35;color:#f6f1df;position:relative;break-after:page;}
.cover .eyebrow {font-size:11pt;letter-spacing:3pt;color:#c6cdb4;margin-bottom:24mm;}
.cover h1 {font-size:44pt;letter-spacing:-1pt;line-height:1.06;color:#fffdf0;border:none;padding:0;margin:0 0 14mm;}
.cover .subtitle {font-size:19pt;line-height:1.28;color:#e2e7cc;margin-bottom:14mm;max-width:147mm;}
.cover .rule {width:38mm;height:2mm;background:#d4ba89;margin:0 0 14mm;}
.cover .idea {font-size:12.8pt;line-height:1.6;max-width:150mm;color:#e5ead9;}
.cover .numbers {margin-top:18mm;font-size:11pt;line-height:1.9;color:#d4ba89;}
.cover .footer {position:absolute;left:23mm;bottom:19mm;right:20mm;border-top:0.5pt solid #607669;padding-top:8mm;font-size:8pt;line-height:1.6;color:#d1dbc8;}
.toc {break-before:page;}
.toc a {display:block;padding:4pt 0;border-bottom:0.5pt solid #d7dfd0;color:#244e43;font-size:9.4pt;}
.toc a::after {content:leader('.') target-counter(attr(href),page);font-size:9pt;}
.card {break-inside:avoid;}
.card h3 {margin-top:14pt;}
.task-section > table th:first-child,.task-section > table td:first-child {width:13%;}
.asset-section .card {padding-top:3pt;}
.figpage {break-before:page;}
.figpage .imagewrap {height:166mm;display:flex;align-items:center;justify-content:center;background:#f7f6ef;padding:6mm;margin:8mm 0 5mm;}
.figpage img {max-height:154mm;max-width:153mm;object-fit:contain;}
.figpage h1 {font-size:23pt;}
.figpage h2 {font-size:13pt;}
figcaption {font-size:9pt;line-height:1.5;color:#4f6258;}
.graph-figure {margin:12pt 0 16pt;padding:5pt;background:#faf8f2;break-inside:avoid;}
.graph-figure p {font-size:8pt;margin:5pt 3pt;}
.sourcepath {font-size:7.7pt;color:#768071;margin:-9pt 0 15pt;overflow-wrap:anywhere;}
@media screen {body{max-width:940px;margin:auto;padding:25px;background:white;}html{background:#e7eadf;}.cover{height:1070px;}section{padding-top:35px;}.toc a::after{content:none;}}
'''
order=[root/'00_START_HERE_RU.md',root/'AUTHOR_REQUIREMENTS_COVERAGE_RU.md']
order += sorted((root/'docs').glob('0[1-9]_*.md'))
order += sorted((root/'levels').glob('*.md'))
order += sorted((root/'docs').glob('1[0-6]_*.md'))
order += [root/'VALIDATION_REPORT_RU.md']
sections=[];toc=[];markdown=[]
for i,p in enumerate(order):
    text=p.read_text();title=text.splitlines()[0].lstrip('# ')
    aid='chapter-'+str(i)
    toc.append((aid,title))
    markdown.append(text)
    body=md(text)
    body=body.replace('</h1>','</h1><div class="sourcepath">'+html.escape(str(p.relative_to(root)))+'</div>',1)
    if p.parent.name=='levels':
        wid=p.name[:3]
        figure=f'<div class="graph-figure"><img src="diagrams/{wid}_graph.svg"/><p>Схема проектных связей. Все расстояния и позиции уточняются по паспорту; это не кадр существующей игры.</p></div>'
        # Figure directly after introductory paragraph, before first level subheading.
        body=body.replace('<h2>',figure+'<h2>',1)
    classes='chapter'
    if p.name.startswith('11_'):
        classes+=' task-section'
        # Contiguous detail cards can flow two per page when there is room.
        chunks=re.split(r'(?=<h3>PW-\d{3}\.)',body)
        body=chunks[0]+''.join('<div class="card">'+x+'</div>' for x in chunks[1:])
    if p.name.startswith('10_'):
        classes+=' asset-section'
        chunks=re.split(r'(?=<h3>A-)',body)
        body=chunks[0]+''.join('<div class="card">'+x+'</div>' for x in chunks[1:])
    sections.append(f'<section class="{classes}" id="{aid}">{body}</section>')
refs=[
('ART_Aminov_SuAnasy_1978_reference.jpeg','Файзрахман Аминов','Су анасы · 1978','Главный ориентир: цветовые массы, ритм природы и связь сказочного пространства с материальной жизнью. Не переносить чужую композицию в игру целиком.','Рис. 9 в статье А. А. Миннебаевой «Иконография Су анасы в искусстве Татарстана XX–XXI вв.»; ART02.'),
('ART_Karamyshev_SuAnasy_1985_reference.jpeg','Владимир Карамышев','Су анасы · 1985','Дополнительный ориентир: текучая линия и связь силуэта с водной средой. Это источник исследования, а не утверждённый дизайн нашего персонажа.','Рис. 12 в той же статье; ART02.'),
('ART_Fatkhutdinov_SuAnasy_1995_1997_reference.jpeg','Ахсан Фатхутдинов','Су анасы · 1995–1997','Дополнительный ориентир: обрамление участвует в изображении. Авторскую трактовку духов не выдавать за единственно верный фольклорный канон.','Рис. 19 в той же статье; ART02 / ART03.'),
('P1_user_selected_painterly.png','Ранее выбранное направление','Наружная деревня · P1','Сохранённый пользовательский выбор из прежнего visual-reset. Относится к основному миру, не является новым эталонным кадром фотомира.','Локальный источник: прежний пакет visual-reset; REF-P1. Происхождение и права наследуются с историей источника.')]
for j,(fn,artist,title,use,source) in enumerate(refs,1):
    aid='art-reference-'+str(j);toc.append((aid,'Референс '+str(j)+'. '+artist))
    sections.append(f'''<section class="figpage" id="{aid}"><h1>Художественный источник {j:02}</h1><h2>{html.escape(artist)}<br>{html.escape(title)}</h2><div class="imagewrap"><img src="references/{fn}"/></div><figcaption><p>{html.escape(use)}</p><p>{html.escape(source)}</p><p><strong>Репродукция для изучения. Не игровой ассет и не доказательство готового рендера.</strong></p></figcaption></section>''')
cover='''<section class="cover"><div class="eyebrow">УРМАН / ПРОИЗВОДСТВЕННАЯ БИБЛИЯ</div><h1>За краем<br>снимка</h1><div class="subtitle">Книга Тукая и живописные миры<br>внутри фотографий</div><div class="rule"></div><div class="idea">Одна деревня. Два способа её увидеть.<br>Прекрасные и странные места,<br>из которых не хочется уходить.</div><div class="numbers">5 фотомиров · 13 снимков · 48 семейств ассетов<br>100 задач реализации · миграция 118 прежних задач</div><div class="footer">Полный мандат на внедрение и фундаментальную переработку ранней альфы.<br>PW–1.0 / 8 октября 2026 / Для Codex и других исполнителей.<br>Это спецификация: новые игровые сцены и ассеты ещё не произведены.</div></section>'''
tocbody='<section class="toc"><h1>Навигация по пакету</h1><p>Главы сохраняют номера исходных файлов. Паспорта пяти миров расположены рядом с инженерной частью; реестры производства — после них.</p>'+''.join(f'<a href="#{a}">{html.escape(t)}</a>' for a,t in toc)+'</section>'
full='<!DOCTYPE html><html lang="ru"><head><meta charset="UTF-8"/><title>УРМАН — За краем снимка — Полное ТЗ</title><style>'+css+'</style></head><body>'+cover+tocbody+''.join(sections)+'</body></html>'
htmlpath=root/'URMAN_PHOTOWORLDS_FULL_SPEC_RU.html';htmlpath.write_text(full)
(root/'URMAN_PHOTOWORLDS_FULL_SPEC_RU.md').write_text('\n\n---\n\n'.join(markdown))
print('HTML and Markdown ready',len(full),flush=True)
HTML(filename=str(htmlpath),base_url=str(root)).write_pdf(str(root/'URMAN_PHOTOWORLDS_FULL_SPEC_RU.pdf'))
print('PDF READY',flush=True)

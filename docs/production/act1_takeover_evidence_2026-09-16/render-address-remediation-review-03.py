"""Render the single review proposal from immutable native evidence; no game writes."""
import hashlib,json,math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent
GEOMETRY=ROOT/'images-address-world-08/address-remediation-geometry.json'
REGISTRY=ROOT/'images-address-world-08/address-world-receipt.json'
SEARCH=ROOT/'images-address-remediation-search-01/address-remediation-geometry.json'
g=json.loads(GEOMETRY.read_text());r=json.loads(REGISTRY.read_text())['registry'];s=json.loads(SEARCH.read_text())
ids=['H006','H007','H010','H011','H012','H013','H014','H015','H019','H021','H024','H032','H041','H044','H045','H046']
compact={'H012','H014','H019','H021','H024','H044','H045','H046'}
selected=[]
for short in ids:
    aid='ADR-'+short
    old=next(x for x in g['proposals'] if x['addressId']==aid and x['candidate']=='current-observed')
    if short in {'H014','H021'}:
        choice=next(x for x in s['boundedCompactSearch'] if x['addressId']==aid)['alternatives'][0]
        receipt=SEARCH.name;scope='B32 / remediation-search-01';name='bounded alternative 0'
    else:
        name='compact-preserve-well-and-drain-proposal' if short=='H012' else 'compact-shared-model-preferred-proposal' if short in compact else 'coordinated-full-scale-proposal'
        choice=next(x for x in g['proposals'] if x['addressId']==aid and x['candidate']==name)
        receipt=GEOMETRY.name;scope='B31 / Address08'
    origin=choice['origin'];basis=choice['basis'];yaw=math.degrees(math.atan2(basis['z']['x'],basis['z']['z']))
    floors=choice['floors'];record=next(x for x in r['addresses'] if x['AddressId']==aid)
    selected.append(dict(addressId=aid,buildingId=old['BuildingId'],parcelId=old['ParcelId'],number=record['HouseNumber'],street=record['StreetId'],
        model='compact-normal-height' if short in compact else 'existing-normal-scale',currentOrigin=old['origin'],origin=origin,yawDegrees=yaw,
        shiftXZ=math.hypot(origin['x']-old['origin']['x'],origin['z']-old['origin']['z']),currentFootprint=old['footprint'],footprint=choice['footprint'],
        compactWorldCorners=choice.get('compactWorldCorners'),evidence=scope,candidate=name,contacts=choice['contacts'],
        terrainDeltaRange=[min(f['deltaFromCandidateBase'] for f in floors),max(f['deltaFromCandidateBase'] for f in floors)],
        floorOwners=sorted({f['shapeOwner'] for f in floors if f['shapeOwner']}),geometryChanged=False,foundationVerified=False,entranceVerified=False,accepted=False))

def box(p):return p['min']['x'],p['max']['x'],p['min']['z'],p['max']['z']
def separation(a,b):
    ax,bx,az,bz=box(a);cx,dx,cz,dz=box(b)
    return math.hypot(max(0,ax-dx,cx-bx),max(0,az-dz,cz-bz))
pairs=[dict(first=a['addressId'],second=b['addressId'],metres=separation(a['footprint'],b['footprint'])) for i,a in enumerate(selected) for b in selected[i+1:]]
payload=dict(schemaVersion=1,purpose='Author review view of the existing proposal, not runtime placement data',
    sources={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [GEOMETRY,REGISTRY,SEARCH]},
    selected=selected,nearestSelectedPairs=sorted(pairs,key=lambda p:p['metres'])[:12],
    roads=r['graph']['roads'],geometryChanged=False,authorApproval=False,placementAcceptance=False)
target=ROOT/'address-miniature-remediation-review-04.json'
if target.exists():raise RuntimeError('Refusing to overwrite '+str(target))
target.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')

image=Image.new('RGB',(1800,2040),'#faf8f2');draw=ImageDraw.Draw(image)
def font(size,bold=False):return ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial'+(' Bold' if bold else '')+'.ttf',size)
F=font(22);S=font(18);B=font(24,True);TITLE=font(38,True)
draw.text((45,30),'16 жилых домов — единое предложение после B32',font=TITLE,fill='#20323c')
draw.text((45,85),'8 существующих моделей нормального масштаба + 8 компактных домов нормальной высоты',font=F,fill='#334b56')
draw.text((45,121),'IDs, номера и стороны улиц сохраняются. Это схема согласования; геометрия игры ещё не изменена.',font=F,fill='#334b56')
roads=r['graph']['roads'];moving={x['buildingId'] for x in selected}
def panel(rect,extent,title,labels=True):
    saved=image.copy()
    x,y,w,h=rect;x0,x1,z0,z1=extent;scale=min(w/(x1-x0),h/(z1-z0));ox=x+(w-scale*(x1-x0))/2;oy=y+(h-scale*(z1-z0))/2
    def xy(a,b):return ox+(a-x0)*scale,oy+(b-z0)*scale
    def within(points):return any(x0<=a<=x1 and z0<=b<=z1 for a,b in points)
    draw.rounded_rectangle((x,y,x+w,y+h),radius=10,fill='#f1f0e8',outline='#bbc6c4',width=2)
    for road in roads:
        if road['Id'].startswith('access/'):continue
        points=[(p['X'],p['Z']) for p in road['Points']]
        if not within(points):continue
        points=[xy(a,b) for a,b in points]
        color='#c0c8c0' if not road['Id'].startswith('route/') else '#79a398'
        draw.line(points,fill=color,width=max(2,round(road['Width']*scale)),joint='curve')
    for building in r['buildings']:
        if building['BuildingId'] in moving:continue
        p=[(q['X'],q['Z']) for q in building['Footprint']]
        if len(p)>2 and within(p):draw.polygon([xy(a,b) for a,b in p],fill='#dbded7',outline='#9ba49c',width=1)
    for row in selected:
        a,b,c,d=box(row['footprint'])
        if not within([(a,c),(b,d)]):continue
        color='#335e94' if row['model']=='existing-normal-scale' else '#725393'
        p=row.get('compactWorldCorners')
        if p:
            centre=(sum(q['x'] for q in p)/len(p),sum(q['z'] for q in p)/len(p))
            poly=sorted({(q['x'],q['z']) for q in p},key=lambda q:math.atan2(q[1]-centre[1],q[0]-centre[0]))
        else:poly=[(a,c),(b,c),(b,d),(a,d)]
        draw.polygon([xy(*q) for q in poly],fill='#ddd5eb' if 'compact' in row['model'] else '#cfdeee',outline=color,width=3)
        aa,bb,cc,dd=box(row['currentFootprint']);corners=[xy(aa,cc),xy(bb,cc),xy(bb,dd),xy(aa,dd),xy(aa,cc)]
        for p1,p2 in zip(corners,corners[1:]):
            length=math.dist(p1,p2);steps=max(1,int(length/5))
            for i in range(0,steps,2):draw.line([(p1[0]+(p2[0]-p1[0])*i/steps,p1[1]+(p2[1]-p1[1])*i/steps),(p1[0]+(p2[0]-p1[0])*min(i+1,steps)/steps,p1[1]+(p2[1]-p1[1])*min(i+1,steps)/steps)],fill='#ad684e',width=2)
        old=row['currentOrigin'];new=row['origin'];start=xy(old['x'],old['z']);end=xy(new['x'],new['z'])
        if row['shiftXZ']>.1:
            draw.line([start,end],fill=color,width=2);draw.ellipse((end[0]-3,end[1]-3,end[0]+3,end[1]+3),fill=color)
        if labels:
            label=row['addressId'][4:];p=xy((a+b)/2,(c+d)/2);bbx=draw.textbbox((0,0),label,font=S);tw=bbx[2]
            draw.rectangle((p[0]-tw/2-3,p[1]-12,p[0]+tw/2+3,p[1]+12),fill='#faf8f2');draw.text((p[0]-tw/2,p[1]-11),label,font=S,fill=color)
    draw.rectangle((x+8,y+8,x+w-8,y+42),fill='#f1f0e8');draw.text((x+16,y+12),title,font=B,fill='#20323c')
    clipped=image.crop((x,y,x+w,y+h));image.paste(saved);image.paste(clipped,(x,y))
    return xy

panel((45,190,1040,1255),(-49,44,-63,49),'План: X / Z, метры')
for y,color,text in [(1475,'#ad684e','Пунктир: нынешний объём'),(1510,'#335e94','Синий: существующая модель, нормальный масштаб'),(1545,'#725393','Фиолетовый: компактный дом 4.8 × 5.6 м'),(1580,'#9ba49c','Серый: сохранённые соседние объекты')]:
    draw.line((55,y+10,95,y+10),fill=color,width=4);draw.text((110,y),text,font=F,fill='#334b56')
draw.text((1120,196),'Окончательные предлагаемые anchors',font=B,fill='#20323c')
draw.text((1120,232),'Дом / адрес         X; Z             сдвиг',font=F,fill='#334b56')
for i,row in enumerate(selected):
    y=275+i*65;color='#725393' if row['model']=='compact-normal-height' else '#335e94';short=row['addressId'][4:]
    draw.rounded_rectangle((1110,y-7,1760,y+50),radius=5,fill='#eee9f4' if row['model']=='compact-normal-height' else '#e8edf4')
    street='Тукай' if row['street']=='tukay' else 'Урман'
    draw.text((1122,y),f'{short} / {street} {row["number"]}',font=F,fill=color)
    draw.text((1400,y),f'{row["origin"]["x"]:g}; {row["origin"]["z"]:g}',font=F,fill='#20323c')
    draw.text((1634,y),f'{row["shiftXZ"]:.2f} м',font=F,fill='#20323c')
draw.text((1120,1342),'H014: только 0.5 м, вместо прежних 15–20 м.',font=F,fill='#20323c')
draw.text((1120,1382),'H021: сарай, скамья и EX-проход сохранены.',font=F,fill='#20323c')
draw.text((1120,1432),'Опоры, двери и общий проход — после реализации.',font=S,fill='#9a5138')
draw.text((1120,1467),'Ограды/сараи H010/H011/H013/H041 требуют',font=S,fill='#9a5138')
draw.text((1120,1495),'локального ремонта у точно установленных владельцев.',font=S,fill='#9a5138')
draw.text((1120,1540),'Подпись «свободный габарит» не означает готовый дом.',font=S,fill='#9a5138')
panel((45,1640,820,335),(-27,-3,3,24),'H012 / H014: колодец и канава остаются')
panel((905,1640,850,335),(-27,-5,-40,-23),'H021: перемещается только жилой объём')
draw.text((45,2000),'Источники: B31 Address08 + B32 remediation-search-01. Нативные запросы; без изменения мира или принятия размещений.',font=S,fill='#53626a')
png=ROOT/'address-miniature-remediation-review-04.png'
if png.exists():raise RuntimeError('Refusing to overwrite '+str(png))
image.save(png)
print(json.dumps({'png':str(png),'data':str(target),'selected':len(selected),'nearestPairs':payload['nearestSelectedPairs'][:3]},ensure_ascii=False))

'use strict';
(function(){
 const NS='http://www.w3.org/2000/svg', INK='http://www.inkscape.org/namespaces/inkscape';
 function importMapSVG(text){
  if(typeof text!=='string'||text.length>20000000)throw Error('SVG files must be 20 MB or smaller.');
  if(/^\s*%PDF-/i.test(text))throw Error('This is a PDF document, not SVG. Export the floorplan as SVG or use Image Tracing.');
  const cleaned=text.replace(/<!DOCTYPE[\s\S]*?>/gi,'').replace(/<!ENTITY[\s\S]*?>/gi,'').replace(/&[A-Za-z][A-Za-z0-9._-]*;/g,' ');
  const doc=new DOMParser().parseFromString(cleaned,'image/svg+xml');
  if(doc.querySelector('parsererror')||!doc.documentElement||doc.documentElement.localName.toLowerCase()!=='svg')throw Error('The file is not valid SVG geometry.');
  if(doc.querySelector('script,foreignObject,iframe,object,embed'))throw Error('Active SVG content was removed. Export plain geometry and try again.');
  const T=Toolkit, root=doc.documentElement, warnings=[], elements=[...doc.querySelectorAll('*')];
  const textOf=n=>String(n?.getAttribute?.('data-name')||n?.getAttribute?.('inkscape:label')||n?.getAttribute?.('label')||n?.getAttributeNS?.(INK,'label')||n?.id||n?.textContent||'').trim();
  const label=n=>textOf(n).replace(/[_-]\d+[_-]?$/,'');
  const rootLength=name=>{const v=Number(String(root.getAttribute(name)||'').replace(/[a-z%]+$/i,''));return Number.isFinite(v)&&v>0?v:0;};
  const vb0=(root.getAttribute('viewBox')||'').trim().split(/[ ,]+/).map(Number);
  const vb=vb0.length===4&&vb0.every(Number.isFinite)&&vb0[2]>0&&vb0[3]>0?vb0:[0,0,rootLength('width')||1200,rootLength('height')||800];
  const number=(n,a,d=0)=>{const v=Number(n.getAttribute(a));return Number.isFinite(v)?v:d;};
  const color=n=>{const raw=n.getAttribute('fill')||n.style?.fill||n.parentElement?.getAttribute?.('fill')||'#CCCCCC';return /^#[0-9a-f]{6}$/i.test(raw)?raw:'#CCCCCC';};
  const matrix=n=>{
   let chain=[],e=n;while(e&&e.nodeType===1){chain.unshift(e);e=e.parentNode;}
   let m=new DOMMatrix().translate(-vb[0],-vb[1]);
   for(const item of chain){const s=item.getAttribute('transform');if(!s)continue;const holder=document.createElementNS(NS,'g');holder.setAttribute('transform',s);const c=holder.transform.baseVal.consolidate();if(c)m=m.multiply(c.matrix);}
   return m;
  };
  const rawPath=n=>{
   if(n.localName==='rect'){const x=number(n,'x'),y=number(n,'y'),w=number(n,'width'),h=number(n,'height');if(w<=0||h<=0)return null;const r=Math.max(0,Math.min(number(n,'rx'),w/2,h/2));return r?'M'+(x+r)+' '+y+'H'+(x+w-r)+'Q'+(x+w)+' '+y+' '+(x+w)+' '+(y+r)+'V'+(y+h-r)+'Q'+(x+w)+' '+(y+h)+' '+(x+w-r)+' '+(y+h)+'H'+(x+r)+'Q'+x+' '+(y+h)+' '+x+' '+(y+h-r)+'V'+(y+r)+'Q'+x+' '+y+' '+(x+r)+' '+y+'Z':T.path([[x,y],[x+w,y],[x+w,y+h],[x,y+h]]);}
   const d=n.getAttribute('d')||'';return /^[MmLlHhVvCcSsQqTtAaZz0-9eE+.,\s-]+$/.test(d)&&/[Zz]\s*$/.test(d)?d:null;
  };
  const geometry=n=>{
   const d=rawPath(n);if(!d)return null;const m=matrix(n),host=document.createElementNS(NS,'svg'),p=document.createElementNS(NS,'path');p.setAttribute('d',d);host.style.cssText='position:absolute;left:-100000px;top:-100000px;width:1px;height:1px';host.append(p);document.body.append(host);let b;try{b=p.getBBox();}catch(_){host.remove();return null;}host.remove();const corners=[[b.x,b.y],[b.x+b.width,b.y],[b.x+b.width,b.y+b.height],[b.x,b.y+b.height]].map(q=>new DOMPoint(q[0],q[1]).matrixTransform(m));return {d,matrix:[m.a,m.b,m.c,m.d,m.e,m.f],bounds:[Math.min(...corners.map(q=>q.x)),Math.min(...corners.map(q=>q.y)),Math.max(...corners.map(q=>q.x)),Math.max(...corners.map(q=>q.y))],source:n};
  };
  const groups=elements.filter(n=>n.localName==='g'), zoneGroups=groups.filter(n=>/(ZONE|SECTION|AREA|BLOCK|SEATING)/i.test(label(n))), stageGroup=groups.find(n=>/(STAGE|PLATFORM|PERFORMANCE)/i.test(label(n)));
  const allGeo=elements.map(geometry).filter(Boolean), isDesc=(n,parent)=>parent&&parent!==n&&parent.contains(n);
  const candidates=allGeo.filter(g=>{const [x,y,r,b]=g.bounds,w=r-x,h=b-y;return w>.25&&h>.25&&w<=Math.max(80,vb[2]*.08)&&h<=Math.max(80,vb[3]*.08)&&w*h<=Math.max(6400,vb[2]*vb[3]*.01);});
  const inside=(g,n)=>n&&n.contains(g.source);
  const seatGroups=zoneGroups.length?zoneGroups: [root];
  const used=new Set(), seatsByZone=new Map();
  const makeSeat=(g,i)=>{const [x,y,r,b]=g.bounds,w=r-x,h=b-y;return {x,y,width:w,height:h,rx:Math.min(number(g.source,'rx'),w/2,h/2)||1.5,uid:T.uid(),number:i+1,row:0,col:i,fill:color(g.source),templateId:'standard'};};
  function nearText(seat){const sx=seat.x+seat.width/2,sy=seat.y+seat.height/2;let best=null,dist=Infinity;for(const n of elements.filter(e=>e.localName==='text')){const v=(n.textContent||'').trim(),num=Number(v);if(!Number.isInteger(num)||num<1)continue;const x=number(n,'x',sx),y=number(n,'y',sy),d=Math.hypot(x-sx,y-sy);if(d<Math.max(seat.width,seat.height)*3&&d<dist){best=num;dist=d;}}return best;}
  for(const container of seatGroups){let list=candidates.filter(g=>!used.has(g.source)&&inside(g,container));if(!list.length&&container===root)list=candidates.filter(g=>!used.has(g.source));if(!list.length)continue;const bounds=list.reduce((a,g)=>[Math.min(a[0],g.bounds[0]),Math.min(a[1],g.bounds[1]),Math.max(a[2],g.bounds[2]),Math.max(a[3],g.bounds[3])],[Infinity,Infinity,-Infinity,-Infinity]);const title=container===root?'IMPORTED':label(container).replace(/^(ZONE|SECTION|AREA|BLOCK|SEATING)[_ -]*/i,'').trim()||'IMPORTED';const z={uid:T.uid(),name:title.toUpperCase().replace(/[^A-Z0-9_]/g,'_').slice(0,40)||'IMPORTED',color:'#7C9FC1',seatTemplateId:'standard',section:T.shape([[Math.max(0,bounds[0]-24),Math.max(0,bounds[1]-24)],[bounds[2]+24,Math.max(0,bounds[1]-24)],[bounds[2]+24,bounds[3]+24],[Math.max(0,bounds[0]-24),bounds[3]+24]]),label:{x:(bounds[0]+bounds[2])/2,y:Math.max(16,bounds[1]-34),size:16,text:title.toUpperCase()},seats:[]};list.sort((a,b)=>a.bounds[1]-b.bounds[1]||a.bounds[0]-b.bounds[0]).forEach((g,i)=>{used.add(g.source);const s=makeSeat(g,i),read=nearText(s);if(read)s.number=read;z.seats.push(s);});const rows=[];z.seats.sort((a,b)=>a.y-b.y||a.x-b.x).forEach(s=>{let row=rows.findIndex(v=>Math.abs(v-s.y)<Math.max(3,s.height*.6));if(row<0){row=rows.length;rows.push(s.y);}s.row=row;});seatsByZone.set(z.uid,z); }
  const remaining=allGeo.filter(g=>!used.has(g.source));
  let stageGeo=stageGroup&&allGeo.find(g=>inside(g,stageGroup));if(!stageGeo)stageGeo=remaining.filter(g=>{const [x,y,r,b]=g.bounds;return r-x>vb[2]*.12&&b-y<vb[3]*.18;}).sort((a,b)=>(b.bounds[2]-b.bounds[0])-(a.bounds[2]-a.bounds[0]))[0];
  const stage=stageGeo?{...stageGeo,label:{x:(stageGeo.bounds[0]+stageGeo.bounds[2])/2,y:(stageGeo.bounds[1]+stageGeo.bounds[3])/2,text:'STAGE'},direction:'up'}:{...T.shape([[vb[2]*.4,vb[3]*.05],[vb[2]*.6,vb[3]*.05],[vb[2]*.6,vb[3]*.1],[vb[2]*.4,vb[3]*.1]]),label:{x:vb[2]/2,y:vb[3]*.075,text:'STAGE'},direction:'up'};
  if(!stageGeo)warnings.push('No stage-like shape was detected; a default stage was created.');
  const artworks=remaining.filter(g=>g!==stageGeo).map(g=>({...g,fill:color(g.source)})).filter(g=>g.bounds[2]-g.bounds[0]>1&&g.bounds[3]-g.bounds[1]>1).map(g=>({d:g.d,fill:g.fill,matrix:g.matrix,bounds:g.bounds}));
  const zones=[...seatsByZone.values()];if(!zones.length)warnings.push('No seat-sized geometry was detected. The SVG will be imported as artwork and can be traced or edited manually.');
  if(zones.length&&zones.length===1&&zones[0].name==='IMPORTED')warnings.push('Seats were inferred from normal SVG geometry; review the generated IMPORTED zone and numbering.');
  if(zoneGroups.length)warnings.push('Named seating groups were detected and converted into editable zones.');
  const template=T.defaultTemplate?T.defaultTemplate():{id:'standard',shape:'rounded-rect',width:7,height:7,cornerRadius:1.5,fill:'#CCCCCC'};
  const p={format:'venue-toolkit',version:1,name:'Imported venue',width:vb[2],height:vb[3],stage,zones,artwork:artworks,seatTemplates:[template]};
  if(p.zones.reduce((n,z)=>n+z.seats.length,0)>20000)throw Error('This SVG contains more than 20,000 detected seats. Reduce the source geometry or import it in zones.');
  const fixed=T.autoFix(p),audit=T.audit(p),seatCount=p.zones.reduce((n,z)=>n+z.seats.length,0);
  warnings.push(fixed?fixed+' geometry or numbering repairs were applied.':'Geometry and numbering were checked automatically.');
  return {project:T.checkProject(p),warnings:[...new Set(warnings)],summary:{zones:p.zones.length,seats:seatCount,stage:!!stageGeo,artwork:p.artwork.length,audit:audit.filter(x=>x.status==='fail').length}};
 }
 window.importMapSVG=importMapSVG;
})();

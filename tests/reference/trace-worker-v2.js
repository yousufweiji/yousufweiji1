'use strict';
function simplifyClosed(points,tolerance){
 if(!tolerance||points.length<5)return points;
 const distance=(p,a,b)=>{const dx=b[0]-a[0],dy=b[1]-a[1],l=dx*dx+dy*dy,t=l?Math.max(0,Math.min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/l)):0;return Math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy);};
 function rdp(p){const keep=new Uint8Array(p.length);keep[0]=keep[p.length-1]=1;const todo=[[0,p.length-1]];while(todo.length){const [a,b]=todo.pop();let max=tolerance,k=-1;for(let i=a+1;i<b;i++){let d=distance(p[i],p[a],p[b]);if(d>max){max=d;k=i;}}if(k>=0){keep[k]=1;todo.push([a,k],[k,b]);}}return p.filter((_,i)=>keep[i]);}
 let far=1,best=0;points.forEach((p,i)=>{const d=Math.hypot(p[0]-points[0][0],p[1]-points[0][1]);if(d>best){best=d;far=i;}});
 const a=rdp(points.slice(0,far+1)),b=rdp(points.slice(far).concat([points[0]]));const out=a.slice(0,-1).concat(b.slice(0,-1));return out.length>=3?out:points;
}
function contourPath(p,curves){const f=n=>Math.round(n*1000)/1000;let d='M'+p[0].map(f).join(' ');if(!curves)return d+p.slice(1).map(q=>'L'+q.map(f).join(' ')).join('')+'Z';
 const tangent=p.map((q,i)=>{const a=p[(i+p.length-1)%p.length],b=p[(i+1)%p.length],u=[q[0]-a[0],q[1]-a[1]],v=[b[0]-q[0],b[1]-q[1]],l=Math.hypot(...u),r=Math.hypot(...v);if(!l||!r||(u[0]*v[0]+u[1]*v[1])/(l*r)<.7)return [0,0];const t=[b[0]-a[0],b[1]-a[1]],len=Math.hypot(...t),scale=Math.min(l,r)*.22/len;return t.map(n=>n*scale);});
 for(let i=0;i<p.length;i++){const j=(i+1)%p.length,a=p[i],b=p[j],u=tangent[i],v=tangent[j];if(u[0]===0&&u[1]===0&&v[0]===0&&v[1]===0)d+='L'+b.map(f).join(' ');else d+='C'+[a[0]+u[0],a[1]+u[1],b[0]-v[0],b[1]-v[1],...b].map(f).join(' ');}return d+'Z';
}

// Deterministic colour quantization and pixel-boundary tracing. No remote services.
function trace({pixels,width,height,colors=4,threshold=160,mode='color',removeWhite=true,minArea=6,tolerance=0,curves=false}){
 const px=new Uint8ClampedArray(pixels),n=width*height;
 if(width<1||height<1||width>2048||height>2048||px.length!==n*4)throw Error('Tracing image dimensions are invalid.');
 const active=i=>px[i*4+3]>=128&&(!removeWhite||Math.min(px[i*4],px[i*4+1],px[i*4+2])<245);
 let palette=[];if(mode==='mono')palette=[[35,43,55]];else{
  const samples=[];for(let i=0;i<n;i+=Math.max(1,Math.floor(n/12000)))if(active(i))samples.push([px[i*4],px[i*4+1],px[i*4+2]]);
  if(!samples.length)throw Error('No visible shapes found. Try keeping the background.');
  palette=[samples[Math.floor(samples.length/2)]];
  const dist=(a,b)=>(a[0]-b[0])**2+(a[1]-b[1])**2+(a[2]-b[2])**2;
  for(let k=1;k<colors;k++){let far=null,best=0;for(const p of samples){const d=Math.min(...palette.map(c=>dist(p,c)));if(d>best){best=d;far=p;}}if(!far||best<80)break;palette.push(far);}
  for(let step=0;step<7;step++){const sums=palette.map(()=>[0,0,0,0]);for(const p of samples){let k=0,best=Infinity;palette.forEach((c,j)=>{let d=dist(p,c);if(d<best){best=d;k=j;}});for(let c=0;c<3;c++)sums[k][c]+=p[c];sums[k][3]++;}palette=palette.map((p,i)=>sums[i][3]?sums[i].slice(0,3).map(x=>Math.round(x/sums[i][3])):p);}
 }
 const labels=new Int16Array(n);labels.fill(-1);for(let i=0;i<n;i++){if(!active(i))continue;const r=px[i*4],g=px[i*4+1],b=px[i*4+2];if(mode==='mono'){if(.2126*r+.7152*g+.0722*b<threshold)labels[i]=0;}else{let best=Infinity;palette.forEach((p,j)=>{const d=(r-p[0])**2+(g-p[1])**2+(b-p[2])**2;if(d<best){best=d;labels[i]=j;}});}}
 const result=[];let total=0;
 for(let k=0;k<palette.length;k++){
  const edges=new Map();let count=0;const key=(x,y)=>y*(width+1)+x;
  const edge=(a,b,dir)=>{if(total+count>=350000)throw Error('Too much image detail. Reduce resolution or use silhouette mode.');if(!edges.has(a))edges.set(a,[]);edges.get(a).push({b,dir});count++;};
  for(let y=0;y<height;y++)for(let x=0;x<width;x++){let i=y*width+x;if(labels[i]!==k)continue;if(y===0||labels[i-width]!==k)edge(key(x,y),key(x+1,y),0);if(x===width-1||labels[i+1]!==k)edge(key(x+1,y),key(x+1,y+1),1);if(y===height-1||labels[i+width]!==k)edge(key(x+1,y+1),key(x,y+1),2);if(x===0||labels[i-1]!==k)edge(key(x,y+1),key(x,y),3);}
  total+=count;if(total>350000)throw Error('Too much image detail. Reduce resolution or use silhouette mode.');
  let parts=[],loops=0;while(edges.size){const start=edges.keys().next().value;let current=start,dir=-1,pts=[],guard=0;
   do{pts.push([current%(width+1),Math.floor(current/(width+1))]);const candidates=edges.get(current);if(!candidates||!candidates.length)throw Error('Could not close a traced contour.');let index=0;if(candidates.length>1&&dir!==-1){const priority=d=>[1,0,3,2].indexOf((d-dir+4)%4);candidates.forEach((v,j)=>{if(priority(v.dir)<priority(candidates[index].dir))index=j;});}let e=candidates.splice(index,1)[0];if(!candidates.length)edges.delete(current);current=e.b;dir=e.dir;if(++guard>count+1)throw Error('Contour limit exceeded.');}while(current!==start);
   let area=0;for(let i=0;i<pts.length;i++){const a=pts[i],b=pts[(i+1)%pts.length];area+=a[0]*b[1]-b[0]*a[1];}if(Math.abs(area)/2<minArea)continue;
   const clean=pts.filter((b,i)=>{const a=pts[(i+pts.length-1)%pts.length],c=pts[(i+1)%pts.length];return (b[0]-a[0])*(c[1]-b[1])!==(b[1]-a[1])*(c[0]-b[0]);});
   if(clean.length>2){const contour=simplifyClosed(clean,tolerance);parts.push(contourPath(contour,curves));loops++;}
  }
  if(parts.length)result.push({d:parts.join(''),fill:'#'+palette[k].map(v=>v.toString(16).padStart(2,'0')).join(''),contours:loops});
  if(typeof postMessage==='function')postMessage({progress:Math.round((k+1)/palette.length*100)});
 }
 if(!result.length)throw Error('No shapes found. Change the threshold or minimum shape area.');return {width,height,paths:result,edges:total};
}
if(typeof self!=='undefined')self.onmessage=e=>{try{self.postMessage({result:trace(e.data)});}catch(error){self.postMessage({error:error.message});}};
if(typeof module!=='undefined')module.exports=trace;

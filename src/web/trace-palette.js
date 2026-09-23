'use strict';
// CPU reference implementation. No network, randomness or third-party tracing code.
(function(root){
 const linear=Array.from({length:256},(_,v)=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;});
 function lab(rgb){const [r,g,b]=rgb.map(v=>linear[Math.max(0,Math.min(255,Math.round(v)))]),l=Math.cbrt(.4122214708*r+.5363325363*g+.0514459929*b),m=Math.cbrt(.2119034982*r+.6806995451*g+.1073969566*b),s=Math.cbrt(.0883024619*r+.2817188376*g+.6299787005*b);return [.2104542553*l+.793617785*m-.0040720468*s,1.9779984951*l-2.428592205*m+.4505937099*s,.0259040371*l+.7827717662*m-.808675766*s];}
 const distance=(a,b)=>(a[0]-b[0])**2+(a[1]-b[1])**2+(a[2]-b[2])**2;
 const bucket=(r,g,b)=>(r>>3)*1024+(g>>3)*32+(b>>3);
 function locks(text){if(Array.isArray(text))text=text.join(',');if(!String(text||'').trim())return [];const items=String(text).split(/[\s,;]+/).filter(Boolean);if(items.length>32)throw Error('Use at most 32 locked colours.');return [...new Set(items.map(s=>{if(!/^#[0-9a-f]{6}$/i.test(s))throw Error('Locked colours must use six-digit HEX, for example #0066CC.');return s.toLowerCase();}))].map(s=>[1,3,5].map(i=>parseInt(s.slice(i,i+2),16)));}
 function quantize(px,width,height,options){const {colors=4,removeWhite=true,lockedColors='',paletteOnly=false,auto=false,denoise=false}=options;
 if(!Number.isInteger(colors)||colors<1||colors>128)throw Error('Palette size must be 1–128.');const fixed=locks(lockedColors);if(paletteOnly&&!fixed.length)throw Error('Enter a locked palette first.');if(!paletteOnly&&fixed.length>colors)throw Error('Increase colour count to include all locked colours.');
 const counts=new Uint32Array(32768),sums=new Float64Array(32768*3),bins=new Int32Array(width*height);bins.fill(-1);
 for(let i=0;i<bins.length;i++){const j=i*4,r=px[j],g=px[j+1],b=px[j+2];if(px[j+3]<128||(removeWhite&&Math.min(r,g,b)>=245))continue;const k=bucket(r,g,b);bins[i]=k;counts[k]++;sums[k*3]+=r;sums[k*3+1]+=g;sums[k*3+2]+=b;}
 const samples=[];for(let k=0;k<32768;k++)if(counts[k]){const rgb=[0,1,2].map(c=>sums[k*3+c]/counts[k]);samples.push({key:k,rgb,lab:lab(rgb),weight:counts[k]});}if(!samples.length)throw Error('No visible pixels; try keeping the background.');
 const palette=fixed.map(p=>p.slice()),labs=palette.map(lab);if(!palette.length){const s=samples.reduce((a,b)=>b.weight>a.weight?b:a);palette.push(s.rgb);labs.push(s.lab);}
 const target=paletteOnly?palette.length:colors;
 while(palette.length<target){let pick=null,best=-1,maxDistance=0;for(const s of samples){const d=Math.min(...labs.map(c=>distance(s.lab,c)));maxDistance=Math.max(maxDistance,d);const score=d*Math.sqrt(s.weight);if(score>best){best=score;pick=s;}}if(maxDistance<1e-10||(auto&&maxDistance<.0016))break;palette.push(pick.rgb);labs.push(pick.lab);}
 const nearest=p=>{let best=Infinity,k=0;for(let j=0;j<labs.length;j++){const d=distance(p,labs[j]);if(d<best){best=d;k=j;}}return k;};
 if(!paletteOnly)for(let iteration=0;iteration<8;iteration++){const acc=palette.map(()=>[0,0,0,0]);let moved=0;for(const s of samples){const a=acc[nearest(s.lab)];for(let c=0;c<3;c++)a[c]+=s.rgb[c]*s.weight;a[3]+=s.weight;}for(let k=fixed.length;k<palette.length;k++)if(acc[k][3]){const rgb=acc[k].slice(0,3).map(v=>v/acc[k][3]),next=lab(rgb);moved+=distance(labs[k],next);palette[k]=rgb;labs[k]=next;}if(moved<1e-8)break;}
 const lut=new Int16Array(32768);for(const s of samples)lut[s.key]=nearest(s.lab);let labels=new Int16Array(bins.length);for(let i=0;i<bins.length;i++)labels[i]=bins[i]<0?-1:lut[bins[i]];
 if(denoise){const cleaned=labels.slice();for(let y=1;y<height-1;y++)for(let x=1;x<width-1;x++){const i=y*width+x,k=labels[i],a=labels[i-1];if(k<0||a<0||a===k)continue;if(labels[i+1]===a&&labels[i-width]===a&&labels[i+width]===a)cleaned[i]=a;}labels=cleaned;}
 return {palette:palette.map(p=>p.map(v=>Math.max(0,Math.min(255,Math.round(v))))),labels,histogramBins:samples.length};
 }
 root.TracePalette={lab,locks,quantize};if(typeof module!=='undefined')module.exports=root.TracePalette;
})(typeof self==='undefined'?globalThis:self);

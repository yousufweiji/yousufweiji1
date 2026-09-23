'use strict';
// The imported production engine stays isolated from the canvas editor's globals.
function scopeImportedCSS(css){
 try{const sheet=new CSSStyleSheet();sheet.replaceSync(css);return [...sheet.cssRules].filter(r=>r.type===CSSRule.STYLE_RULE).map(r=>r.selectorText.split(',').map(s=>s.trim().startsWith('svg[data-toolkit-svg=')?s.trim():'svg[data-toolkit-svg="true"] '+s.trim()).join(',')+'{'+r.style.cssText+'}').join('\n');}catch{return '';}
}
let desktopStore=Object.create(null),desktopStoreTimer,desktopWrites=Promise.resolve(),desktopRestoring=false;
const desktopHost=parent.DesktopProduction;
function persistDesktopStore(){clearTimeout(desktopStoreTimer);desktopStoreTimer=setTimeout(()=>{const content=JSON.stringify(desktopStore);desktopWrites=desktopWrites.catch(()=>{}).then(()=>desktopHost.storageWrite(content)).catch(e=>{toast('Storage failed: '+e.message,'error');throw e;});desktopWrites.catch(()=>{});},250);}
storageGet=(key,fallback=null)=>Object.hasOwn(desktopStore,key)?desktopStore[key]:fallback;
storageSet=(key,value)=>{if(typeof value!=='string')value=String(value);const old=desktopStore[key];desktopStore[key]=value;if(JSON.stringify(desktopStore).length>45000000){if(old===undefined)delete desktopStore[key];else desktopStore[key]=old;toast('Production storage is full. Export project/files before removing older versions.','error');return false;}persistDesktopStore();if(!desktopRestoring)desktopHost.changed();return true;};
storageRemove=key=>{delete desktopStore[key];persistDesktopStore();};
// Native storage survives random localhost ports and browser profile resets.
libOpenDB=async()=>null;
const originalFallbackSave=libFallbackSave;
libFallbackSave=items=>{if(!originalFallbackSave(items))throw Error('File library was not saved.');return true;};
download=(name,blob)=>desktopHost.saveBlob(name,blob).then(r=>{if(r){toast('Saved '+name,'success');setLastAction('Saved '+name);}else{toast('Save cancelled','info');setLastAction('Save cancelled · '+name);}}).catch(e=>toast('Save failed: '+e.message,'error'));
const desktopParse=parseSVG;
parseSVG=(text,isRestore=false)=>{desktopParse(text,isRestore);if(!isRestore)V6.baseline=null;if(!desktopRestoring)desktopHost.changed();};
const compareBeforeDesktop=compareSVGNode;
compareSVGNode=xml=>{const node=compareBeforeDesktop(normalizeSVGSource(String(xml)).text);node.setAttribute('data-toolkit-svg','true');node.querySelectorAll('script,foreignObject,iframe,object,embed,link,animate,set,animateMotion,animateTransform').forEach(n=>n.remove());for(const n of [node,...node.querySelectorAll('*')])for(const a of [...n.attributes])if(/^on/i.test(a.name)||/javascript:|data:text\/html/i.test(a.value)||(/href$/i.test(a.name)&&!a.value.startsWith('#')))n.removeAttribute(a.name);node.querySelectorAll('style').forEach(s=>s.textContent=scopeImportedCSS(s.textContent));return node;};
const desktopPayload=projectPayload;
projectPayload=(includeAudit=true)=>({...desktopPayload(includeAudit),json:STATE.json,jsonOutput:STATE.jsonOutput,artwork:STATE.artwork,sourceLock:$('sourceLock')?.checked!==false});
const desktopOpen=openProject;
openProject=async file=>{const text=await file.text(),d=JSON.parse(text);if(!d||!['yousufweiji-hallmap-production','platinumlist-sop-production'].includes(d.format)||typeof d.svg!=='string'||d.svg.length>MAX_IMPORT_BYTES)throw Error('Invalid production project.');
 desktopRestoring=true;try{await desktopOpen({name:file.name,text:async()=>text});STATE.json=d.json||null;STATE.jsonOutput=d.jsonOutput||'';if(typeof d.artwork==='string'&&/^data:image\/(png|jpeg|webp|gif);base64,/i.test(d.artwork)){STATE.artwork=d.artwork;setArtworkSource(d.artwork,{name:'Saved artwork'});}else{STATE.artwork=null;STATE.artworkSource=null;}if($('sourceLock'))$('sourceLock').checked=d.sourceLock!==false;V6.baseline=null;if(STATE.svg&&$('sourceLock')?.checked)captureV6Baseline();}finally{desktopRestoring=false;}
};
// Shared app image decoder adds GIF/BMP/TIFF without an online converter.
loadArtwork=async file=>{if(!file)return;try{const blob=await desktopHost.artworkImage(file);const data=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(reader.result);reader.onerror=()=>reject(Error('Cannot read artwork.'));reader.readAsDataURL(blob);});STATE.artwork=data;setArtworkSource(data,file);const img=new Image();img.src=data;img.style.cssText='max-width:100%;max-height:360px';$('artPreview')?.replaceChildren(img);desktopHost.changed();scheduleAutosave();}catch(e){toast(e.message,'error');}};
function normalizeEditorLayers(xml){const d=new DOMParser().parseFromString(xml,'image/svg+xml');if(d.querySelector('parsererror'))throw Error('Invalid editor SVG.');for(const g of d.querySelectorAll('g[data-name]')){const name=g.getAttribute('data-name');if(SUBLAYERS.includes(name))g.id=name;}for(const n of d.querySelectorAll('[transform]'))if(/^matrix\(\s*1[ ,]+0[ ,]+0[ ,]+1[ ,]+0[ ,]+0\s*\)$/.test(n.getAttribute('transform')))n.removeAttribute('transform');return new XMLSerializer().serializeToString(d);}
const desktopSheet=sheetManifestLoad;
sheetManifestLoad=async file=>{try{if(file&&/\.xlsx$/i.test(file.name)){const r=await desktopHost.readSheet(file);await loadManifest({text:async()=>r.csv});$('sheetManifestOut').textContent='Loaded '+r.sheet+' · '+r.rows+' rows · offline XLSX';scheduleAutosave();}else await desktopSheet(file);}catch(e){$('sheetManifestOut').textContent='Manifest import failed: '+e.message;}};
window.ProductionBridge={
 loadEditor(xml,name){STATE.sourceName=name;parseSVG(normalizeEditorLayers(xml));switchTab('builder');},
 xml(){return STATE.svg?serialize():null;},
 payload(){return STATE.svg?projectPayload():null;},
 async restore(payload){if(payload)await openProject({name:'Desktop project',text:async()=>JSON.stringify(payload)});},
 reset(){desktopRestoring=true;try{STATE.svg=null;STATE.originalSVG='';STATE.undo=[];STATE.redo=[];STATE.versions=[];STATE.json=null;STATE.manifest=null;STATE.artwork=null;STATE.artworkSource=null;STATE.audit=[];STATE.enhancedQA=[];renderSVG();updateBadges();refreshInventory();}finally{desktopRestoring=false;}},
 state(){return {seats:countSeats(),tabs:$$('[data-tab]').map(e=>e.dataset.tab),audit:STATE.audit,gate:STATE.gate,library:libFallbackLoad()};},
 async flush(){clearTimeout(desktopStoreTimer);await desktopWrites.catch(()=>{});await desktopHost.storageWrite(JSON.stringify(desktopStore));}
};
(async()=>{try{const s=await desktopHost.storageRead();if(s)desktopStore=Object.assign(Object.create(null),JSON.parse(s));}catch(e){console.error(e);}
 safeBoot();for(const input of $$('input[type=file]'))if((input.accept||'').includes('image/')||(input.accept||'').includes('.png'))input.accept+=',.gif,.bmp,.tif,.tiff';
 document.querySelector('.prod').textContent='PRODUCTION · APEX 2.2';
 const oldAutosaveInfo=updateAutosaveInfo;updateAutosaveInfo=()=>{oldAutosaveInfo();$('autosaveInfo').textContent=$('autosaveInfo').textContent.replace('local browser storage','local desktop storage');};updateAutosaveInfo();
 document.getElementById('sheetManifestOut').textContent='CSV, TSV, Excel 2003 XML and XLSX load offline.';
 // Keep native project saving available from the full-screen production workspace.
 const b=document.createElement('button');b.className='btn small';b.textContent='Save desktop project';b.onclick=()=>desktopHost.saveProject();document.querySelector('.hright').prepend(b);
 // Existing helper scripts can also be saved as JSX instead of only copied.
 document.querySelectorAll('[data-bridge-copy]').forEach(b=>{const save=b.cloneNode();save.textContent='Save JSX';save.removeAttribute('data-bridge-copy');save.onclick=()=>downloadText('Illustrator Helper '+(+b.dataset.bridgeCopy+1)+'.jsx',Object.values(BRIDGE_SCRIPTS)[+b.dataset.bridgeCopy]);b.after(save);});
 desktopHost.ready(window.ProductionBridge);
})().catch(e=>{console.error(e);toast('Production startup failed: '+e.message,'error');});

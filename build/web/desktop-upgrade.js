'use strict';
async function blobBase64(blob){if(blob.size>35000000)throw Error('File exceeds the 35 MB native transfer limit.');return new Promise((resolve,reject)=>{const r=new FileReader();r.onload=()=>resolve(String(r.result).split(',')[1]);r.onerror=()=>reject(Error('Cannot read file.'));r.readAsDataURL(blob);});}
function base64Blob(data,type){const text=atob(data),bytes=new Uint8Array(text.length);for(let i=0;i<text.length;i++)bytes[i]=text.charCodeAt(i);return new Blob([bytes],{type});}
window.ToolkitImages={
 async normalized(file){if(file.size>30000000)throw Error('Choose an image under 30 MB.');if(/\.(tiff?|gif|bmp)$/i.test(file.name||'')){if(!native){if(/\.tiff?$/i.test(file.name))throw Error('TIFF decoding requires the desktop app.');return file;}const r=await api('decode',{content:await blobBase64(file)});if(r.frames>1)toast('Using the first frame/page of '+r.frames+'.');return base64Blob(r.data,'image/png');}return file;},
 async bitmap(file){return createImageBitmap(await this.normalized(file));}
};
let productionPromise,productionResolve;
async function ensureProduction(){if(productionBridge)return productionBridge;if(!productionPromise){productionPromise=new Promise(resolve=>productionResolve=resolve);$('productionFrame').src='production.html';}return productionPromise;}
window.DesktopProduction={
 async storageRead(){if(native)return (await api('studio')).content;return localStorage.getItem('toolkit-production');},
 async storageWrite(content){if(native)return api('studio',{content});localStorage.setItem('toolkit-production',content);},
 changed(){setDirty();},
 async saveBlob(name,blob){const ext=name.split('.').pop().toLowerCase(),kind={txt:'report',zip:'archive',venue:'project'}[ext]||ext;const allowed=['report','archive','project','json','svg','csv','xml','tsv','jsx','png','jpg','jpeg','webp','gif','bmp','tif','tiff','pdf','eps','dxf'];if(!allowed.includes(kind))throw Error('Unsupported output extension: '+ext);if(native){const r=await api('save',{kind,name,content:await blobBase64(blob),encoding:'base64'});return !r.cancelled;}return save(kind,name,await blob.text());},
 saveProject:()=>$('saveProject').click(),
 artworkImage:file=>ToolkitImages.normalized(file),
 async readSheet(file){if(!native)throw Error('XLSX import requires the desktop app.');return api('xlsx',{content:await blobBase64(file)});},
 async ready(bridge){productionBridge=bridge;try{if(pendingProduction)await bridge.restore(pendingProduction);else bridge.loadEditor(T.svg(project),project.name+'.svg');productionResolve(bridge);}catch(e){toast('Production restore failed: '+e.message);productionResolve(bridge);}}
};
bind('productionOpen',async()=>{$('productionWorkspace').hidden=false;await ensureProduction();});
bind('productionBack',()=>{$('productionWorkspace').hidden=true;render();});
bind('productionSend',async()=>{if(preview)throw Error('Apply or discard the preview first.');const bridge=await ensureProduction();if(await ask('Replace the production working copy?','This sends the canvas map to Production tools. Save the desktop project first if you need to keep the current production copy.','Send map'))bridge.loadEditor(T.svg(project),project.name+'.svg');});
bind('productionReturn',async()=>{const bridge=await ensureProduction(),xml=bridge.xml();if(!xml)throw Error('Load a production SVG first.');let imported;try{imported=importMapSVG(xml);}catch(e){throw Error(e.message+' The full SVG remains available in Production tools.');}if(!(await ask('Import production map into the canvas?',imported.warnings.join('\n\n')+'\n\nThe full production working copy stays available; Undo restores the canvas.','Import map')))return;push();project=imported.project;preview=null;selected=null;setDirty();fit();render();$('productionWorkspace').hidden=true;});
for(const id of ['traceMode','traceColors','traceResolution','threshold','minArea','removeWhite','traceDetail','traceCurves','lockedColors','paletteOnly','traceDenoise'])$(id).addEventListener('change',()=>{if(traceResult)toast('Settings changed. Run Trace image to update the vector result.');});

$('traceFormat').addEventListener('change',()=>$('aiScript').hidden=$('traceFormat').value!=='ai');
bind('aiScript',()=>save('jsx','Convert SVG to Illustrator AI.jsx',window.IllustratorConversionScript));

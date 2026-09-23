// Shared by the CEP preview and Illustrator ES3 builders. No browser or host APIs.
var YWGeometry = (function () {
    var shapes = ['rect','rounded','ellipse','circle','semicircle','arch','trapezoid','thrust','tstage','ustage','runway','hexagon'];
    var layouts = ['normal','fan','amphitheatre','oval','arena','thrust','club','tiered'];
    function has(a,v){for(var i=0;i<a.length;i++)if(a[i]===v)return true;return false;}
    function number(v,def,min,max,label){var n=(v===undefined||v===null||v==='')?def:Number(v);if(!isFinite(n)||n<min||n>max)throw new Error(label+' must be between '+min+' and '+max+'.');return n;}
    function integer(v,def,min,max,label){var n=number(v,def,min,max,label);if(n!==Math.floor(n))throw new Error(label+' must be a whole number.');return n;}
    function bounds(pts){var b=[Infinity,Infinity,-Infinity,-Infinity];for(var i=0;i<pts.length;i++){var q=pts[i];b[0]=Math.min(b[0],q[0]);b[1]=Math.min(b[1],q[1]);b[2]=Math.max(b[2],q[0]);b[3]=Math.max(b[3],q[1]);}return b;}
    function rect(x,y,w,h){return [[x,y],[x+w,y],[x+w,y+h],[x,y+h]];}
    function stage(type,w,h,r){
        if(type==='rounded10'){type='rounded';r=10;}if(type==='rounded20'){type='rounded';r=20;}
        if(type==='ellipse3d')type='ellipse';if(type==='platform')type='thrust';if(type==='simple')type='rect';
        if(!has(shapes,type))throw new Error('Unknown stage shape: '+type);
        w=number(w,400,20,10000,'Stage width');h=number(h,80,10,10000,'Stage height');
        r=Math.min(number(r,12,0,5000,'Corner radius'),w/2,h/2);
        var pts=[],i,t,cx=w/2,cy=h/2,rx=w/2,ry=h/2;
        if(type==='circle'){rx=ry=Math.min(w,h)/2;}
        if(type==='rect')pts=rect(0,0,w,h);
        else if(type==='rounded'){
            var corners=[[w-r,r,-90],[w-r,h-r,0],[r,h-r,90],[r,r,180]];
            for(var c=0;c<4;c++)for(i=0;i<=8;i++){t=(corners[c][2]+i*90/8)*Math.PI/180;pts.push([corners[c][0]+r*Math.cos(t),corners[c][1]+r*Math.sin(t)]);}
        }else if(type==='ellipse'||type==='circle')for(i=0;i<64;i++){t=2*Math.PI*i/64;pts.push([cx+rx*Math.cos(t),cy+ry*Math.sin(t)]);}
        else if(type==='semicircle'||type==='arch'){
            var base=type==='arch'?h*0.65:h;
            for(i=0;i<=48;i++){t=Math.PI+i*Math.PI/48;pts.push([cx+rx*Math.cos(t),base+base*Math.sin(t)]);}
            if(type==='arch'){pts.push([w,h]);pts.push([0,h]);}
        }else if(type==='trapezoid')pts=[[w*.15,0],[w*.85,0],[w,h],[0,h]];
        else if(type==='thrust')pts=[[w*.12,0],[w*.88,0],[w*.88,h*.25],[w,h*.25],[w,h*.75],[w*.88,h*.75],[w*.88,h],[w*.12,h],[w*.12,h*.75],[0,h*.75],[0,h*.25],[w*.12,h*.25]];
        else if(type==='tstage')pts=[[0,0],[w,0],[w,h*.35],[w*.65,h*.35],[w*.65,h],[w*.35,h],[w*.35,h*.35],[0,h*.35]];
        else if(type==='ustage')pts=[[0,0],[w*.25,0],[w*.25,h*.65],[w*.75,h*.65],[w*.75,0],[w,0],[w,h],[0,h]];
        else if(type==='runway')pts=[[0,0],[w,0],[w,h*.35],[w*.57,h*.35],[w*.57,h],[w*.43,h],[w*.43,h*.35],[0,h*.35]];
        else if(type==='hexagon')pts=[[w*.2,0],[w*.8,0],[w,h*.5],[w*.8,h],[w*.2,h],[0,h*.5]];
        return pts;
    }
    function transform(pts,x,y,rotation){var b=bounds(pts),cx=(b[0]+b[2])/2,cy=(b[1]+b[3])/2,a=rotation*Math.PI/180,out=[];for(var i=0;i<pts.length;i++){var dx=pts[i][0]-cx,dy=pts[i][1]-cy;out.push([cx+dx*Math.cos(a)-dy*Math.sin(a)+x,cy+dx*Math.sin(a)+dy*Math.cos(a)+y]);}return out;}
    function hull(points){
        points.sort(function(a,b){return a[0]-b[0]||a[1]-b[1];});
        function cross(o,a,b){return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0]);}
        var lo=[],hi=[],i;for(i=0;i<points.length;i++){while(lo.length>=2&&cross(lo[lo.length-2],lo[lo.length-1],points[i])<=0)lo.pop();lo.push(points[i]);}
        for(i=points.length-1;i>=0;i--){while(hi.length>=2&&cross(hi[hi.length-2],hi[hi.length-1],points[i])<=0)hi.pop();hi.push(points[i]);}lo.pop();hi.pop();return lo.concat(hi);
    }
    function sectionFor(seats){var pts=[];for(var i=0;i<seats.length;i++){var s=seats[i];pts=pts.concat(rect(s.x-6,s.y-6,19,19));}return hull(pts);}
    function map(p){
        p=p||{};var type=String(p.type||'normal');if(!has(layouts,type))throw new Error('Unknown map layout: '+type);
        var nz=integer(p.zones,4,1,12,'Zones'),ns=integer(p.seatsPerZone,80,1,5000,'Seats per zone'),nr=integer(p.rowsPerZone,8,1,200,'Rows per zone');
        if(nr>ns)throw new Error('Rows cannot exceed the seat count.');if(nz*ns>20000)throw new Error('Limit each generated map to 20,000 seats.');
        var cols=Math.ceil(ns/nr),pitch=9,gap=70,pad=6,w=(cols-1)*pitch+7,h=(nr-1)*pitch+7,stageW=number(p.stageWidth,220,20,2000,'Stage width'),stageH=number(p.stageHeight,70,10,2000,'Stage height');
        var font=number(p.labelSize,24,8,120,'Zone label size'),labelGap=number(p.labelGap,10,0,100,'Label gap'),names=p.prefixes||[],seen={},zones=[],all=[],i,j,z;
        var gridCols=Math.min(4,nz),span=type==='fan'?110:160,slot=span/nz,inner=Math.max(stageW/2+80,(cols*13+30)/(slot*Math.PI/180)*1.3),outer=inner+(nr-1)*13;
        var ringSlot=360/nz,ringInner=Math.max(stageW/2+100,stageH/2+100,(cols*13+50)/(ringSlot*Math.PI/180)*1.3);
        var stageType=p.stageType||((type==='oval')?'ellipse':type==='thrust'?'tstage':'rect');
        var sp=stage(stageType,stageW,stageH,p.cornerRadius),stageX=-stageW/2,stageY=-stageH/2;
        for(z=0;z<nz;z++){
            var name=String(names[z]||String.fromCharCode(65+z)).replace(/^\s+|\s+$/g,'').toUpperCase();
            if(!/^[A-Z0-9]+$/.test(name))throw new Error('Zone names must use letters and numbers only: '+name);
            if(seen['$'+name])throw new Error('Duplicate zone name: '+name);seen['$'+name]=true;
            var seats=[],x0=0,y0=0,ang=0,radius=0,section=null,table=null;
            if(type==='normal'||type==='tiered'){
                var row=Math.floor(z/gridCols),col=z%gridCols;
                x0=(col-(gridCols-1)/2)*(w+gap)-w/2+(type==='tiered'?row*(w+gap)*.22:0);
                y0=stageH/2+80+row*(h+gap);
            }else if(type==='arena'||type==='thrust'){
                var sides=type==='arena'?4:3,side=z%sides,rank=Math.floor(z/sides),per=Math.ceil(nz/sides),step=Math.max(w,h)+gap;
                var dist=Math.max(stageW,stageH)/2+Math.max(w,h)/2+100+per*step/2;
                var off=(rank-(per-1)/2)*step;
                if(side===0){x0=-dist-w/2;y0=off-h/2;}
                else if(side===1){x0=off-w/2;y0=dist-h/2;}
                else if(side===2){x0=dist-w/2;y0=off-h/2;}
                else{x0=off-w/2;y0=-dist-h/2;}
            }else if(type==='club'){
                radius=Math.max(30,cols*13/(2*Math.PI));var rr=radius+(nr-1)*13;
                var clubCols=Math.ceil(Math.sqrt(nz)),clubStep=2*rr+gap;
                x0=(z%clubCols-(clubCols-1)/2)*clubStep;y0=stageH/2+100+rr+Math.floor(z/clubCols)*clubStep;
                table={x:x0,y:y0,r:Math.max(10,radius-15)};
            }
            for(i=0;i<ns;i++){
                var rowIndex=Math.floor(i/cols),c=i%cols,inRow=Math.min(cols,ns-rowIndex*cols),x,y;
                if(type==='amphitheatre'||type==='oval'){
                    var sl=type==='oval'?ringSlot:slot,mid=(type==='oval'?-90:90-span/2)+(z+.5)*sl;
                    var rad=(type==='oval'?ringInner:inner)+rowIndex*13;
                    ang=(mid+(c-(inRow-1)/2)*13/rad*180/Math.PI)*Math.PI/180;
                    x=rad*Math.cos(ang)-3.5;y=rad*Math.sin(ang)-3.5;
                }else if(type==='fan'){
                    ang=(90-span/2+(z+.5)*slot)*Math.PI/180;var depth=inner+rowIndex*13,tangent=(c-(inRow-1)/2)*13;
                    x=depth*Math.cos(ang)-tangent*Math.sin(ang)-3.5;y=depth*Math.sin(ang)+tangent*Math.cos(ang)-3.5;
                }else if(type==='club'){
                    ang=(c/inRow*360-90)*Math.PI/180;var cr=radius+rowIndex*13;
                    x=x0+cr*Math.cos(ang)-3.5;y=y0+cr*Math.sin(ang)-3.5;
                }else{x=x0+c*pitch+(type==='tiered'?rowIndex*3:0);y=y0+rowIndex*pitch;}
                seats.push({x:x,y:y,row:rowIndex,col:c,number:c+1});
            }
            if(type==='amphitheatre'||type==='oval'){
                var sl2=type==='oval'?ringSlot:slot,mid2=(type==='oval'?-90:90-span/2)+(z+.5)*sl2,ri=type==='oval'?ringInner:inner;
                var half=Math.min(sl2/2-0.2,(cols*13/2+14)/ri*180/Math.PI),outerR=ri+(nr-1)*13+10,innerR=ri-10;section=[];
                for(j=0;j<=40;j++){ang=(mid2-half+j*2*half/40)*Math.PI/180;section.push([innerR*Math.cos(ang),innerR*Math.sin(ang)]);}
                for(j=40;j>=0;j--){ang=(mid2-half+j*2*half/40)*Math.PI/180;section.push([outerR*Math.cos(ang),outerR*Math.sin(ang)]);}
            }else section=sectionFor(seats);
            var b=bounds(section),size=Math.min(font,Math.max(8,(b[2]-b[0]-8)/(name.length*.65)));
            var label={x:(b[0]+b[2])/2,y:b[1]-labelGap,size:size,text:name};
            zones.push({name:name,seats:seats,section:section,label:label,table:table});all=all.concat(section,[[label.x-name.length*size*.35,label.y-size],[label.x+name.length*size*.35,label.y]]);
        }
        sp=transform(sp,stageX,stageY,0);all=all.concat(sp);var b=bounds(all),dx=60-b[0],dy=60-b[1],width=Math.ceil(b[2]-b[0]+120),height=Math.ceil(b[3]-b[1]+120);
        if(width>15000||height>15000)throw new Error('Layout exceeds 15,000 px. Reduce rows or seats per zone.');
        function shift(pts){for(var k=0;k<pts.length;k++){pts[k][0]+=dx;pts[k][1]+=dy;}}
        shift(sp);for(z=0;z<zones.length;z++){var zone=zones[z];shift(zone.section);zone.label.x+=dx;zone.label.y+=dy;for(i=0;i<zone.seats.length;i++){zone.seats[i].x+=dx;zone.seats[i].y+=dy;}if(zone.table){zone.table.x+=dx;zone.table.y+=dy;}}
        return {type:type,width:width,height:height,zones:zones,totalSeats:nz*ns,stage:{points:sp,label:{x:stageX+stageW/2+dx,y:stageY+stageH/2+dy,text:'STAGE'}},overlayFill:p.overlayFill==='white'?'white':'outline'};
    }
    return {shapes:shapes,layouts:layouts,number:number,stage:stage,transform:transform,bounds:bounds,map:map};
})();

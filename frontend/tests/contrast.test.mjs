import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const css=readFileSync(new URL('../src/style.css',import.meta.url),'utf8');
function luminance(hex){const rgb=hex.match(/\w\w/g).map(v=>parseInt(v,16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2];}
function contrast(a,b){const values=[luminance(a),luminance(b)].sort((a,b)=>b-a);return (values[0]+.05)/(values[1]+.05);}
test('reading, help and action colors meet WCAG AA on their actual surfaces',()=>{
 for(const [fg,bg] of [['152f36','f3f5f3'],['42594f','ffffff'],['42594f','e6ece7'],['ffffff','184e43'],['345f51','f3f6f3'],['4a6358','ffffff'],['91513f','fff0ee']])assert.ok(contrast(fg,bg)>=4.5,`${fg}/${bg}`);
 assert.ok(contrast('71857a','fcfdfc')>=3);assert.ok(contrast('184e43','ffffff')>=3);
 assert.doesNotMatch(css,/font-size:(10|11|12|13)px/);
});

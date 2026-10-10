import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const css=readFileSync(new URL('../src/style.css',import.meta.url),'utf8');
function luminance(hex){const rgb=hex.match(/\w\w/g).map(v=>parseInt(v,16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2];}
function contrast(a,b){const values=[luminance(a),luminance(b)].sort((a,b)=>b-a);return (values[0]+.05)/(values[1]+.05);}
test('reading, help and action colors meet WCAG AA on their actual surfaces',()=>{
 for(const [fg,bg] of [['19344a','f1f5fa'],['425a70','ffffff'],['425a70','e6eef6'],['ffffff','155b91'],['285674','f1f5fa'],['425a70','ffffff'],['91513f','fff0ee']])assert.ok(contrast(fg,bg)>=4.5,`${fg}/${bg}`);
 assert.ok(contrast('637e96','fbfdff')>=3);assert.ok(contrast('155b91','ffffff')>=3);
 // 12–13px are limited to the explicitly compact case grid and toolbar.
 const smallRules=css.match(/[^{}]+\{[^{}]*font-size:(?:10|11|12|13)px[^{}]*\}/g)??[];
 for(const rule of smallRules)assert.match(rule,/compact-workplace-toolbar|compact-color-toggle|workplace-case-context|case-explorer \.explorer-table/);
 assert.ok(contrast('19344a','fff6cc')>=4.5);
 assert.ok(contrast('19344a','fbd9d9')>=4.5);
 assert.ok(contrast('ffffff','256da8')>=4.5);
});

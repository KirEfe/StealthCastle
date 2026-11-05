const fs = require('fs');
const html = fs.readFileSync(process.argv[2], 'utf-8');
const scripts = html.match(/<script type="module">(.*?)<\/script>/s);
if (!scripts) { console.error('NO MODULE SCRIPT'); process.exit(1); }
let js = scripts[1];

// Заглушки браузерных API
globalThis.window = {
  speechSynthesis: { getVoices: () => [], speak: () => {}, cancel: () => {}, onvoiceschanged: null },
  addEventListener: () => {}, devicePixelRatio: 2, agentSpeak: undefined
};
globalThis.devicePixelRatio = 2;
globalThis.localStorage = {
  getItem: () => null,
  setItem: () => {}
};
globalThis.document = {
  getElementById: (id) => ({ value: 0, innerHTML: '', appendChild: () => {}, add: () => {}, addEventListener: () => {}, classList: { add(){}, remove(){} } }),
  querySelector: () => null
};
globalThis.SpeechSynthesisUtterance = class { constructor(t){this.text=t;} };
globalThis.performance = { now: () => Date.now() };
globalThis.requestAnimationFrame = (cb) => setTimeout(cb, 16);
globalThis.clearInterval = () => {};
globalThis.clearTimeout = () => {};
globalThis.THREE = {
  Scene: class { constructor(){this.children=[];} add(c){this.children.push(c);} },
  PerspectiveCamera: class { constructor(){this.aspect=1;this.position={set(x,y,z){this.x=x;this.y=y;this.z=z;}};} updateProjectionMatrix(){} },
  WebGLRenderer: class { setSize(){} render(){} setPixelRatio(){} set shadowMapEnabled(v){} get shadowMap(){return {enabled:true};} },
  HemisphereLight: class {},
  DirectionalLight: class { constructor(){this.position={set(){}};} },
  Fog: class {},
  Color: class { constructor(v){this.v=v;} },
  Mesh: class { constructor(){this.position={set(x,y,z){this.x=x;this.y=y;this.z=z;}};this.rotation={set(x,y,z){this.x=x;this.y=y;this.z=z;}};this.scale={set(x,y,z){this.x=x;this.y=y;this.z=z;}};} add(){} },
  Group: class { constructor(){this.position={set(x,y,z){this.x=x;this.y=y;this.z=z;}};this.rotation={set(x,y,z){this.x=x;this.y=y;this.z=z;}};this.scale={set(x,y,z){this.x=x;this.y=y;this.z=z;}};this.children=[];} add(c){this.children.push(c);} },
  SphereGeometry: class {}, CapsuleGeometry: class {}, CylinderGeometry: class {}, TorusGeometry: class {},
  MeshStandardMaterial: class { constructor(){this.emissiveIntensity=0;} },
  OrbitControls: class { constructor(){this.target={set(){}};this.enableDamping=false;this.minDistance=1;this.maxDistance=10;} update(){} },
  Clock: class { constructor(){this.t=Date.now();} getDelta(){const n=Date.now();const d=(n-this.t)/1000;this.t=n;return Math.min(d,0.1);} }
};
globalThis.OrbitControls = globalThis.THREE.OrbitControls;

js = js.replace("import * as THREE from 'three';", '');
js = js.replace("import { OrbitControls } from 'three/addons/controls/OrbitControls.js';", '');

try {
  new Function(js)();
  console.log('SYNTAX OK');
  console.log('agentSpeak:', typeof window.agentSpeak);
  const id = window.agentSpeak({ say: "Привет! Я твой ИИ-агент.", emotion: "happy", animation: "wave" });
  console.log('agentSpeak returned id:', id);
  setTimeout(() => { console.log('DONE'); process.exit(0); }, 300);
} catch (e) {
  console.error('SYNTAX ERROR:', e.message);
  console.error(e.stack.split('\n').slice(0, 8).join('\n'));
  const m = e.stack.match(/<anonymous>:(\d+):(\d+)/);
  if (m) {
    const lines = js.split('\n');
    const ln = parseInt(m[1]);
    console.log('--- line', ln, '---');
    console.log(lines.slice(ln - 3, ln + 2).join('\n'));
  }
  process.exit(1);
}

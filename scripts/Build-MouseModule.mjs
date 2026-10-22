import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dependencyRoot = path.resolve(process.argv[2] || path.join(root, 'Modules/Mouse/node_modules'));
const { build } = await import(pathToFileURL(path.join(dependencyRoot, 'vite/dist/node/index.js')).href);
const { default: ts } = await import(pathToFileURL(path.join(dependencyRoot,'typescript/lib/typescript.js')).href);
const program = ts.createProgram(['entry.tsx','MiceApp.tsx','validation.ts'].map(file => path.join(root,'Modules/Mouse/source',file)), {
  noEmit:true, strict:true, jsx:ts.JsxEmit.React, target:ts.ScriptTarget.ES2022,
  module:ts.ModuleKind.ESNext, moduleResolution:ts.ModuleResolutionKind.Bundler,
  allowSyntheticDefaultImports:true, resolveJsonModule:true, skipLibCheck:true,
  baseUrl:dependencyRoot, typeRoots:[path.join(dependencyRoot,'@types')], types:['react','react-dom'],
  paths:{react:[path.join(dependencyRoot,'@types/react/index.d.ts')], 'react-dom/*':[path.join(dependencyRoot,'@types/react-dom/*')]}
});
const diagnostics = ts.getPreEmitDiagnostics(program);
if (diagnostics.length) throw new Error(ts.formatDiagnostics(diagnostics.slice(0,20), {
  getCanonicalFileName:file=>file, getCurrentDirectory:()=>root, getNewLine:()=> '\n'
}));
console.log('PASS: strict TypeScript checking of sanitized mouse module.');
await build({ configFile:false, root:path.join(root,'Modules/Mouse'), publicDir:false,
  define:{'process.env.NODE_ENV':'"production"'},
  resolve:{alias:{'react-dom':path.join(dependencyRoot,'react-dom'),react:path.join(dependencyRoot,'react')}},
  build:{outDir:path.join(root,'artifacts/mouse-bundle'),emptyOutDir:true,lib:{entry:path.join(root,'Modules/Mouse/source/entry.tsx'),formats:['es'],fileName:()=> 'app.js'},minify:true} });

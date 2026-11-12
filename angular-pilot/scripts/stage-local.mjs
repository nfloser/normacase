import { cp, mkdir, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = path.join(root, 'angular-pilot/dist/angular-pilot/browser');
const apiOutput = path.join(root, 'src/NormaCase.Api/bin/Release/net10.0');
const destination = path.join(apiOutput, 'wwwroot/angular-pilot');
try {
  await stat(path.join(source, 'index.html'));
  await stat(path.join(apiOutput, 'NormaCase.Api.dll'));
  await mkdir(destination, { recursive: true });
  await cp(source, destination, { recursive: true });
  console.log('Angular-Pilot in den lokalen API-Build kopiert.');
} catch {
  console.error('Zuerst Angular-Pilot und NormaCase.Api im Release-Modus bauen.');
  process.exitCode = 1;
}

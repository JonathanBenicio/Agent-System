import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
const defaultOutput = resolve(import.meta.dirname, '../../tests/TestResults/backend-core-remediation/current');

function validationTarget() {
  const project = process.env.BACKEND_VALIDATION_COMPOSE_PROJECT;
  const database = process.env.BACKEND_VALIDATION_DATABASE;
  if (!project || !/^[a-z0-9][a-z0-9_-]*$/.test(project)) throw new Error('BACKEND_VALIDATION_COMPOSE_PROJECT is required.');
  if (!database || !/^review_pr152_[a-z0-9_]+$/.test(database)) throw new Error('BACKEND_VALIDATION_DATABASE must be an exclusive review_pr152_ database.');
  return { project, database };
}

export function assertApiTargetMatches(outputDirectory = process.env.BACKEND_VALIDATION_OUTPUT_DIR || defaultOutput) {
  const { project, database } = validationTarget();
  const targetPath = resolve(outputDirectory, 'api-target.json');
  let target;
  try { target = JSON.parse(readFileSync(targetPath, 'utf8')); }
  catch { throw new Error('API target manifest is missing or invalid; start the API with the same Compose project and database.'); }
  if (target.composeProject !== project || target.database !== database || target.host !== '127.0.0.1' || Number(target.port) !== 55432)
    throw new Error('API and diagnostic SQL targets do not match the selected review_pr152_* Compose database.');
}

export function validationSql(statement) {
  const { project, database } = validationTarget();
  const compose = resolve(import.meta.dirname, 'compose.yml');
  const args = ['compose', '-f', compose, '-p', project];
  const config = JSON.parse(execFileSync('docker', [...args, 'config', '--format', 'json'], { encoding: 'utf8' }));
  const ports = config.services?.postgres?.ports || [];
  if (!ports.some(port => String(port.published) === '55432' && port.host_ip === '127.0.0.1') || ports.some(port => String(port.published) === '5432'))
    throw new Error('Validation Compose must publish PostgreSQL exclusively on loopback 55432.');
  return execFileSync('docker', [...args, 'exec', '-T', 'postgres', 'psql', '-v', 'ON_ERROR_STOP=1', '-U', 'validation', '-d', database, '-At'], { input: statement, encoding: 'utf8' }).trim();
}

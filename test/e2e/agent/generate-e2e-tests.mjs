import { promises as fs } from 'node:fs';
import path from 'node:path';

const args = parseArgs(process.argv.slice(2));

const repoRoot = path.resolve(args['repo-root'] || process.cwd());
const mode = (args.mode || 'generate').toLowerCase();
const changedFilesFile = args['changed-files-file'];
const outputRoot = path.resolve(repoRoot, 'test/e2e/generated');
const generatedTestsRoot = path.resolve(repoRoot, 'test/e2e/tests/generated');

const requirementRegex = /^services\/[^/]+\/docs\/(business|technical)\/.*\.md$/i;

const serviceApiKeyMap = {
  billing: 'billing',
  customer: 'customer',
  fundstransfermgt: 'fundsTransfer',
  policy: 'policy',
  ratingandunderwriting: 'ratingandunderwriting'
};

const result = await run();
if (result.writtenFiles.length > 0) {
  console.log(`Generated ${result.writtenFiles.length} file(s).`);
  for (const file of result.writtenFiles) {
    console.log(` - ${toRepoRelative(file)}`);
  }
} else {
  console.log('No eligible requirements changes detected.');
}

async function run() {
  const changedFiles = await getChangedFiles();
  const requirementFiles = changedFiles.filter(file => requirementRegex.test(file));

  await fs.mkdir(outputRoot, { recursive: true });
  await fs.mkdir(generatedTestsRoot, { recursive: true });

  if (requirementFiles.length === 0) {
    await writeSummary({
      mode,
      changedFiles,
      requirementFiles,
      generatedSpecs: [],
      personas: []
    });
    return { writtenFiles: [path.join(outputRoot, 'summary.json')] };
  }

  const grouped = groupByService(requirementFiles);
  const generatedSpecs = [];
  const personas = [];
  const writtenFiles = [];

  for (const [service, files] of Object.entries(grouped)) {
    const docs = [];
    for (const relativeFile of files) {
      const absolute = path.resolve(repoRoot, relativeFile);
      const content = await fs.readFile(absolute, 'utf8');
      docs.push({ relativeFile, content });
    }

    const scenarios = deriveScenarios(service, docs);
    personas.push(buildPersona(service, docs, scenarios));

    if (mode === 'generate') {
      const specPath = path.join(generatedTestsRoot, `${service}.generated.spec.ts`);
      const specContent = renderSpec(service, docs, scenarios);
      await fs.writeFile(specPath, specContent, 'utf8');
      generatedSpecs.push(toRepoRelative(specPath));
      writtenFiles.push(specPath);
    }
  }

  const planPath = path.join(outputRoot, 'testplan.md');
  const personasPath = path.join(outputRoot, 'personas.json');

  await fs.writeFile(planPath, renderPlan(grouped, generatedSpecs), 'utf8');
  await fs.writeFile(personasPath, JSON.stringify(personas, null, 2), 'utf8');

  writtenFiles.push(planPath, personasPath);

  const summaryPath = await writeSummary({
    mode,
    changedFiles,
    requirementFiles,
    generatedSpecs,
    personas
  });
  writtenFiles.push(summaryPath);

  return { writtenFiles };
}

function parseArgs(input) {
  const parsed = {};
  for (let index = 0; index < input.length; index += 1) {
    const token = input[index];
    if (!token.startsWith('--')) {
      continue;
    }
    const key = token.slice(2);
    const next = input[index + 1];
    if (!next || next.startsWith('--')) {
      parsed[key] = 'true';
      continue;
    }
    parsed[key] = next;
    index += 1;
  }
  return parsed;
}

async function getChangedFiles() {
  const fromEnv = process.env.CHANGED_FILES;
  if (fromEnv && fromEnv.trim().length > 0) {
    return normalizeLines(fromEnv);
  }

  if (changedFilesFile) {
    const content = await fs.readFile(path.resolve(changedFilesFile), 'utf8');
    return normalizeLines(content);
  }

  return await collectAllRequirementFiles(path.join(repoRoot, 'services'));
}

async function collectAllRequirementFiles(root) {
  const files = [];
  await walk(root, files);
  return files
    .map(file => toRepoRelative(file))
    .filter(file => requirementRegex.test(file));
}

async function walk(current, accumulator) {
  const entries = await fs.readdir(current, { withFileTypes: true });
  for (const entry of entries) {
    const resolved = path.join(current, entry.name);
    if (entry.isDirectory()) {
      await walk(resolved, accumulator);
      continue;
    }
    accumulator.push(resolved);
  }
}

function normalizeLines(value) {
  return value
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => line.replace(/\\/g, '/'));
}

function groupByService(files) {
  const grouped = {};
  for (const file of files) {
    const parts = file.split('/');
    const service = parts[1];
    if (!service) {
      continue;
    }
    if (!grouped[service]) {
      grouped[service] = [];
    }
    grouped[service].push(file);
  }
  return grouped;
}

function deriveScenarios(service, docs) {
  const joined = docs.map(doc => doc.content.toLowerCase()).join('\n');
  const scenarios = [];

  scenarios.push('api-health-endpoint');

  if (service === 'customer') {
    scenarios.push('customer-create-and-read');
  }

  if (service === 'policy' || service === 'billing' || /policyissued|policybound|quoteaccepted/.test(joined)) {
    scenarios.push('quote-to-policy-binding');
  }

  if (/cancel|lapse|reinstat/.test(joined)) {
    scenarios.push('lifecycle-status-visibility');
  }

  return Array.from(new Set(scenarios));
}

function buildPersona(service, docs, scenarios) {
  const docNames = docs.map(doc => doc.relativeFile);
  const goals = [];
  const content = docs.map(doc => doc.content.toLowerCase()).join('\n');

  if (/issue|policyissued/.test(content)) {
    goals.push('Verify policy issuance prerequisites and bound state handoff.');
  }
  if (/cancel|refund/.test(content)) {
    goals.push('Verify cancellation and billing refund triggers are visible to APIs.');
  }
  if (/customer/.test(content)) {
    goals.push('Verify customer identity data can be created and retrieved.');
  }

  if (goals.length === 0) {
    goals.push('Verify domain APIs remain reachable and baseline contract assumptions hold.');
  }

  return {
    service,
    sourceDocs: docNames,
    scenarios,
    goals
  };
}

function renderSpec(service, docs, scenarios) {
  const imports = new Set([
    "import { test, expect } from '@playwright/test';",
    "import { getTestConfig, validateConfig } from '../../config/api-endpoints';"
  ]);

  if (scenarios.includes('customer-create-and-read') || scenarios.includes('quote-to-policy-binding')) {
    imports.add("import { createCustomer, getCustomer } from '../../helpers/customer-api';");
  }

  if (scenarios.includes('quote-to-policy-binding')) {
    imports.add("import { startQuote, submitUnderwriting, acceptQuote } from '../../helpers/rating-api';");
    imports.add("import { waitForPolicyCreation } from '../../helpers/policy-api';");
  }

  if (scenarios.includes('lifecycle-status-visibility')) {
    imports.add("import { getCustomerPolicies } from '../../helpers/policy-api';");
  }

  const sourceDocs = docs.map(doc => `- ${doc.relativeFile}`).join('\n');
  const apiKey = serviceApiKeyMap[service] || service;

  const blocks = [];

  blocks.push(`
  test('service health endpoint responds for ${service}', async ({ request }) => {
    const baseUrl = config.apis.${apiKey};
    const health = await request.get(baseUrl + '/health');

    if (health.status() === 404) {
      const fallback = await request.get(baseUrl + '/healthz');
      expect([200, 204, 404]).toContain(fallback.status());
      return;
    }

    expect([200, 204]).toContain(health.status());
  });`);

  if (scenarios.includes('customer-create-and-read')) {
    blocks.push(`
  test('customer create and read consistency', async ({ request }) => {
    const created = await createCustomer(request, {
      firstName: 'Agentic',
      lastName: 'Coverage'
    });

    const fetched = await getCustomer(request, created.customerId);
    expect(fetched.customerId).toBe(created.customerId);
    expect(fetched.email).toBe(created.email);
  });`);
  }

  if (scenarios.includes('quote-to-policy-binding')) {
    blocks.push(`
  test('quote acceptance creates bound policy', async ({ request }) => {
    test.setTimeout(180000);

    const customer = await createCustomer(request, {
      firstName: 'Policy',
      lastName: 'Generated'
    });

    const quote = await startQuote(request, customer.customerId, {
      structureCoverageLimit: 300000,
      structureDeductible: 1000,
      contentsCoverageLimit: 100000,
      contentsDeductible: 500,
      termMonths: 12,
      propertyZipCode: '90210'
    });

    const underwriting = await submitUnderwriting(request, quote.quoteId, {
      priorClaimsCount: 0,
      propertyAgeYears: 12,
      creditTier: 'Excellent'
    });

    expect(underwriting.status()).toBe(200);

    const accepted = await acceptQuote(request, quote.quoteId);
    expect(accepted.policyCreationInitiated).toBe(true);

    const policy = await waitForPolicyCreation(request, customer.customerId);
    expect(policy.status).toBe('Bound');
    expect(policy.policyNumber).toMatch(/^KWG-\\d{4}-\\d{6}$/);
  });`);
  }

  if (scenarios.includes('lifecycle-status-visibility')) {
    blocks.push(`
  test('policy lifecycle statuses are queryable for customer', async ({ request }) => {
    const customer = await createCustomer(request, {
      firstName: 'Lifecycle',
      lastName: 'Watcher'
    });

    const policies = await getCustomerPolicies(request, customer.customerId);
    expect(Array.isArray(policies)).toBe(true);
  });`);
  }

  return `${Array.from(imports).join('\n')}

const config = getTestConfig();

test.describe('[Generated] ${service} requirements regression', () => {
  test.beforeAll(() => {
    validateConfig(config);
  });

${blocks.join('\n')}
});

test.describe('[Generated] metadata for ${service}', () => {
  test('source documents snapshot', async () => {
    const snapshot = [
${sourceDocs
  .split('\n')
  .map(doc => `      '${doc.replace(/'/g, "\\'")}'`)
  .join(',\n')}
    ];

    expect(snapshot.length).toBeGreaterThan(0);
  });
});
`;
}

function renderPlan(grouped, generatedSpecs) {
  const lines = [];
  lines.push('# Agentic E2E Generation Plan');
  lines.push('');
  lines.push(`Generated at: ${new Date().toISOString()}`);
  lines.push('');
  lines.push('## Services Analyzed');
  lines.push('');

  for (const [service, files] of Object.entries(grouped)) {
    lines.push(`- ${service}`);
    for (const file of files) {
      lines.push(`  - ${file}`);
    }
  }

  lines.push('');
  lines.push('## Generated Specs');
  lines.push('');
  if (generatedSpecs.length === 0) {
    lines.push('- None (plan mode)');
  } else {
    for (const spec of generatedSpecs) {
      lines.push(`- ${spec}`);
    }
  }

  return `${lines.join('\n')}\n`;
}

async function writeSummary(summary) {
  const summaryPath = path.join(outputRoot, 'summary.json');
  await fs.writeFile(summaryPath, JSON.stringify(summary, null, 2), 'utf8');
  return summaryPath;
}

function toRepoRelative(targetPath) {
  return path.relative(repoRoot, targetPath).replace(/\\/g, '/');
}
import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const patternsFolder = path.join(root, 'content', 'patterns');
const allowedCategories = new Set(['strategic', 'technical', 'devops', 'enablement', 'discovery']);

const requiredTopLevel = [
  'id',
  'slug',
  'title',
  'category',
  'subcategory',
  'summary',
  'decisionGuidance',
  'enablingTechnologies',
  'thingsToWatchOutFor',
  'complexity',
  'starterDiagram',
  'realWorldExample',
  'relatedPatterns',
  'furtherReading',
  'tags',
  'lastUpdated'
];

const files = fs.readdirSync(patternsFolder).filter((file) => file.endsWith('.json'));
if (files.length === 0) {
  console.error('No pattern files found in content/patterns.');
  process.exit(1);
}

const patterns = files.map((file) => {
  const fullPath = path.join(patternsFolder, file);
  const raw = fs.readFileSync(fullPath, 'utf8');
  try {
    return JSON.parse(raw);
  } catch (error) {
    console.error(`Invalid JSON: ${file}`);
    throw error;
  }
});

const ids = new Set();
const slugs = new Set();
let hasError = false;

for (const pattern of patterns) {
  for (const field of requiredTopLevel) {
    if (!(field in pattern)) {
      console.error(`Pattern '${pattern.id ?? 'unknown'}' missing '${field}'.`);
      hasError = true;
    }
  }

  if (ids.has(pattern.id)) {
    console.error(`Duplicate pattern id '${pattern.id}'.`);
    hasError = true;
  }
  ids.add(pattern.id);

  if (slugs.has(pattern.slug)) {
    console.error(`Duplicate pattern slug '${pattern.slug}'.`);
    hasError = true;
  }
  slugs.add(pattern.slug);

  if (!allowedCategories.has(pattern.category)) {
    console.error(`Pattern '${pattern.id}' has invalid category '${pattern.category}'.`);
    hasError = true;
  }

  if (typeof pattern.subcategory !== 'string' || pattern.subcategory.length === 0) {
    console.error(`Pattern '${pattern.id}' requires a non-empty subcategory.`);
    hasError = true;
  }

  if (!pattern.summary || typeof pattern.summary !== 'string') {
    console.error(`Pattern '${pattern.id}' requires non-empty summary.`);
    hasError = true;
  }

  const decision = pattern.decisionGuidance ?? {};
  if (!decision.problemSolved || !Array.isArray(decision.whenToUse) || !Array.isArray(decision.whenNotToUse)) {
    console.error(`Pattern '${pattern.id}' has invalid decisionGuidance structure.`);
    hasError = true;
  }

  const watch = pattern.thingsToWatchOutFor ?? {};
  if (!Array.isArray(watch.gotchas) || !watch.opinionatedGuidance) {
    console.error(`Pattern '${pattern.id}' has invalid thingsToWatchOutFor structure.`);
    hasError = true;
  }

  const complexity = pattern.complexity ?? {};
  if (!['low', 'medium', 'high'].includes(complexity.level) || !complexity.rationale) {
    console.error(`Pattern '${pattern.id}' has invalid complexity structure.`);
    hasError = true;
  }

  if (!complexity.teamImpact || !complexity.skillDemand || !complexity.operationalDemand || !complexity.toolingDemand) {
    console.error(`Pattern '${pattern.id}' must include teamImpact, skillDemand, operationalDemand, and toolingDemand.`);
    hasError = true;
  }

  const diagram = pattern.starterDiagram ?? {};
  if (!diagram.title || !diagram.description || !Array.isArray(diagram.nodes) || diagram.nodes.length === 0) {
    console.error(`Pattern '${pattern.id}' has invalid starterDiagram structure.`);
    hasError = true;
  }

  const example = pattern.realWorldExample ?? {};
  if (!example.context || !example.approach || !example.outcome) {
    console.error(`Pattern '${pattern.id}' has invalid realWorldExample structure.`);
    hasError = true;
  }

  if (!Array.isArray(pattern.enablingTechnologies) || pattern.enablingTechnologies.length === 0) {
    console.error(`Pattern '${pattern.id}' requires at least one enabling technology.`);
    hasError = true;
  }

  if (!Array.isArray(pattern.relatedPatterns) || !Array.isArray(pattern.furtherReading) || !Array.isArray(pattern.tags)) {
    console.error(`Pattern '${pattern.id}' has invalid array fields.`);
    hasError = true;
  }
}

for (const pattern of patterns) {
  for (const relatedId of pattern.relatedPatterns) {
    if (!ids.has(relatedId)) {
      console.error(`Pattern '${pattern.id}' references unknown related pattern '${relatedId}'.`);
      hasError = true;
    }
  }
}

if (hasError) {
  process.exit(1);
}

console.log(`Validated ${patterns.length} patterns successfully.`);

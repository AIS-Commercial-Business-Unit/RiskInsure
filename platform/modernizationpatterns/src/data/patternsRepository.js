import { categoryMetadata, categoryOrder, subcategoryMetadata } from './taxonomy.js';

const patternModules = import.meta.glob('../../content/patterns/*.json', { eager: true });
const sourceModules = import.meta.glob('../../content/sources/*.json', { eager: true });

const requiredFields = [
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

function getModuleValue(moduleItem) {
  return moduleItem?.default ?? moduleItem;
}

function validatePattern(pattern) {
  for (const field of requiredFields) {
    if (pattern[field] === undefined || pattern[field] === null) {
      throw new Error(`Pattern '${pattern?.id ?? 'unknown'}' is missing required field '${field}'.`);
    }
  }

  if (!categoryMetadata[pattern.category]) {
    throw new Error(`Pattern '${pattern.id}' has invalid category '${pattern.category}'.`);
  }

  if (!pattern.subcategory || typeof pattern.subcategory !== 'string') {
    throw new Error(`Pattern '${pattern.id}' must include subcategory.`);
  }
}

export function getSources() {
  const sourceFile = Object.values(sourceModules).map(getModuleValue).find((item) => Array.isArray(item));
  return sourceFile ?? [];
}

export function getPatterns() {
  const patterns = Object.values(patternModules).map(getModuleValue);
  patterns.forEach(validatePattern);
  return patterns.sort((a, b) => a.title.localeCompare(b.title));
}

export function getPatternBySlug(slug) {
  return getPatterns().find((pattern) => pattern.slug === slug);
}

export function getPatternById(id) {
  return getPatterns().find((pattern) => pattern.id === id);
}

export function getCategories() {
  const patterns = getPatterns();
  return categoryOrder.map((key) => {
    const patternCount = patterns.filter((pattern) => pattern.category === key).length;
    return {
      ...categoryMetadata[key],
      patternCount
    };
  });
}

export function getPatternsForCategory(categoryKey) {
  return getPatterns().filter((pattern) => pattern.category === categoryKey);
}

export function getSubcategoryInfo(categoryKey, subcategoryKey) {
  return subcategoryMetadata?.[categoryKey]?.[subcategoryKey] ?? null;
}

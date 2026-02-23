const args = process.argv.slice(2);

function readArg(name, fallback) {
  const flag = `--${name}`;
  const index = args.indexOf(flag);
  if (index === -1 || index === args.length - 1) {
    return fallback;
  }
  return args[index + 1];
}

const teamImpact = readArg('teamImpact', 'single-team');
const skillDemand = readArg('skillDemand', 'medium');
const operationalDemand = readArg('operationalDemand', 'medium');
const toolingDemand = readArg('toolingDemand', 'medium');

const teamWeights = {
  'single-team': 1,
  'multi-team': 3
};

const demandWeights = {
  low: 1,
  medium: 2,
  high: 3
};

function requireValue(label, value, allowed) {
  if (!allowed.includes(value)) {
    console.error(`${label} must be one of: ${allowed.join(', ')}`);
    process.exit(1);
  }
}

requireValue('teamImpact', teamImpact, ['single-team', 'multi-team']);
requireValue('skillDemand', skillDemand, ['low', 'medium', 'high']);
requireValue('operationalDemand', operationalDemand, ['low', 'medium', 'high']);
requireValue('toolingDemand', toolingDemand, ['low', 'medium', 'high']);

const score =
  teamWeights[teamImpact] +
  demandWeights[skillDemand] +
  demandWeights[operationalDemand] +
  demandWeights[toolingDemand];

let level = 'medium';
if (score <= 6) {
  level = 'low';
} else if (score >= 10) {
  level = 'high';
}

console.log('Complexity suggestion');
console.log('---------------------');
console.log(`teamImpact:        ${teamImpact}`);
console.log(`skillDemand:       ${skillDemand}`);
console.log(`operationalDemand: ${operationalDemand}`);
console.log(`toolingDemand:     ${toolingDemand}`);
console.log(`score:             ${score}`);
console.log(`suggestedLevel:    ${level}`);

console.log('');
console.log('Suggested template block:');
console.log(JSON.stringify({
  complexity: {
    level,
    rationale: 'Initial suggestion from helper. Refine with domain context.',
    teamImpact,
    skillDemand: `Describe why '${skillDemand}' skill demand applies.`,
    operationalDemand: `Describe why '${operationalDemand}' operational demand applies.`,
    toolingDemand: `Describe why '${toolingDemand}' tooling demand applies.`
  }
}, null, 2));

import {
  Activity,
  ArrowLeftRight,
  BadgeCheck,
  BarChart3,
  Bird,
  Bot,
  BrainCircuit,
  Cable,
  Circle,
  Columns2,
  Compass,
  Database,
  DatabaseZap,
  Diamond,
  DollarSign,
  Ear,
  FastForward,
  FileStack,
  Gauge,
  GitBranch,
  Inbox,
  Layers,
  Layers2,
  LineChart,
  Map,
  Network,
  PanelTop,
  Radar,
  Repeat,
  RotateCcw,
  Shield,
  ShieldCheck,
  SlidersHorizontal,
  SplitSquareVertical,
  Target,
  Clock,
  ToggleLeft,
  Users,
  Workflow,
  Server,
  GitMerge
} from 'lucide-react';

/**
 * Maps pattern IDs to Lucide React icons.
 * Uses explicit mappings where specified, falls back to category defaults.
 */
export const patternIconMap = {
  // Discovery patterns
  'listening-tour': Ear,
  'business-context-outcomes': Target,
  'event-storming': Workflow,
  'success-metrics-before-migration': BarChart3,
  'platform-simplification-mandate': Layers,
  'ai-based-discovery': Radar,

  // Strategic patterns
  'bounded-context-first': Map,
  'context-mapping': Network,
  'core-domain-focus': Diamond,
  'anti-corruption-layer': Shield,
  'domain-event-contracts': Activity,
  'composite-integration-layer': Layers2,
  'event-driven-architecture': Activity,
  'distributed-architecture-constraints': SlidersHorizontal,
  'bus-over-broker-strategy': ArrowLeftRight,

  // Migration/Technical patterns (using from user's mapping)
  'strangler-fig-migration': GitBranch,
  'branch-by-abstraction': SplitSquareVertical,
  'parallel-run-reconciliation': Columns2,
  'minimal-viable-increment': FastForward,
  'feature-flag-release': ToggleLeft,
  'change-data-capture-transition': DatabaseZap,
  'shim-and-facade': PanelTop,
  'canary-deployment': Bird,

  // Technical patterns
  'saga-process-manager': GitMerge, // Changed from Timeline (not available)
  'durable-file-processing': FileStack,
  'centralized-caching': Database,
  'outbox-idempotency': Repeat,
  'replay-reprocessing': RotateCcw,
  'composite-api': Layers2,
  'messaging-abstraction-layer': Cable,
  'dead-letter-governance': Inbox,
  'ai-ready-integration': BrainCircuit,

  // Enablement patterns
  'platform-team-model': Users,
  'enablement-team-model': Compass,
  'center-of-excellence-timeboxed': BadgeCheck,
  'devsecops-first-class': ShieldCheck,
  'developer-experience-kpi': Gauge,
  'observability-first': LineChart,
  'operational-ai-agent': Bot,
  'finops-transparency': DollarSign,
  'ai-ready-enablement': BrainCircuit,

  // DevOps patterns
  'time-as-ticks': Clock,
  'everything-runs-locally': Server
};

/**
 * Category fallback icons when pattern-specific mapping doesn't exist
 */
const categoryFallbackIcons = {
  discovery: Radar,
  strategic: Map,
  technical: Workflow,
  enablement: Users,
  devops: GitMerge
};

/**
 * Get the icon component for a given pattern.
 * @param {Object} pattern - Pattern object with id and category
 * @returns {React.Component} Lucide icon component
 */
export function getPatternIcon(pattern) {
  // Try explicit pattern mapping first
  if (patternIconMap[pattern.id]) {
    return patternIconMap[pattern.id];
  }

  // Fall back to category default
  if (categoryFallbackIcons[pattern.category]) {
    return categoryFallbackIcons[pattern.category];
  }

  // Ultimate fallback
  return Circle;
}

export const categories = [
  {
    slug: 'enablement',
    label: 'Enablement patterns',
    title: 'Enablement patterns',
    summary: 'Remove friction, align teams, and unblock delivery paths.',
    tagline: 'Enablement patterns unblock momentum and keep teams aligned.',
    purpose: 'This category focuses on removing organizational and platform friction so teams can move fast without breaking trust.',
    diagramTitle: 'Enablement flywheel',
    diagramNodes: ['Clarity', 'Access', 'Enable', 'Sustain'],
    related: ['devops', 'technical', 'complexity'],
    examples: [
      {
        title: 'Golden path delivery',
        focus: 'Platform enablement',
        summary: 'Offer a paved path so teams can ship quickly with consistent guardrails.',
        diagram: 'Path stages'
      },
      {
        title: 'Capability onboarding',
        focus: 'Team alignment',
        summary: 'Standardize how teams adopt core services and shared tooling.',
        diagram: 'Onboarding loop'
      },
      {
        title: 'Governance as code',
        focus: 'Compliance flow',
        summary: 'Automate controls so governance stays ahead of delivery.',
        diagram: 'Policy pipeline'
      }
    ]
  },
  {
    slug: 'technical',
    label: 'Technical patterns',
    title: 'Technical patterns',
    summary: 'Choose platform capabilities, data patterns, and integration styles.',
    tagline: 'Technical patterns shape the platform choices that scale modern systems.',
    purpose: 'This category frames the structural and capability choices that power modernization decisions.',
    diagramTitle: 'Capability stack',
    diagramNodes: ['Runtime', 'Data', 'Integration', 'Observability'],
    related: ['distributed-architecture', 'enablement', 'complexity'],
    examples: [
      {
        title: 'Strangler-by-domain',
        focus: 'Modernization approach',
        summary: 'Replace legacy functionality in slices aligned to business domains.',
        diagram: 'Domain handoff'
      },
      {
        title: 'Event-first integration',
        focus: 'Integration',
        summary: 'Model systems around event streams that describe what happened.',
        diagram: 'Event spine'
      },
      {
        title: 'Composable data layer',
        focus: 'Data design',
        summary: 'Separate operational and analytical needs while sharing trusted schemas.',
        diagram: 'Data mesh'
      }
    ]
  },
  {
    slug: 'complexity',
    label: 'Complexity patterns',
    title: 'Complexity patterns',
    summary: 'Reduce hidden coupling and keep scope under control.',
    tagline: 'Complexity patterns fight the sprawl that slows modernization down.',
    purpose: 'This category focuses on managing scope, dependencies, and change fatigue across large transformations.',
    diagramTitle: 'Complexity filter',
    diagramNodes: ['Scope', 'Dependencies', 'Change', 'Stability'],
    related: ['enablement', 'technical', 'devops'],
    examples: [
      {
        title: 'Thin-slice increments',
        focus: 'Scope control',
        summary: 'Release modernization in small, value-focused increments.',
        diagram: 'Slice ladder'
      },
      {
        title: 'Dependency guardrails',
        focus: 'Coupling control',
        summary: 'Make dependencies explicit and enforce architectural contracts.',
        diagram: 'Guardrail map'
      },
      {
        title: 'Stability budgets',
        focus: 'Risk management',
        summary: 'Balance change speed with the stability of critical services.',
        diagram: 'Risk gauge'
      }
    ]
  },
  {
    slug: 'distributed-architecture',
    label: 'Distributed architecture patterns',
    title: 'Distributed architecture patterns',
    summary: 'Define how services communicate, scale, and recover.',
    tagline: 'Distributed architecture patterns keep systems coherent at scale.',
    purpose: 'This category captures the ways distributed systems communicate and recover while staying observable and resilient.',
    diagramTitle: 'Distributed heartbeat',
    diagramNodes: ['Events', 'Contracts', 'Resilience', 'Telemetry'],
    related: ['technical', 'complexity', 'devops'],
    examples: [
      {
        title: 'Contract-first messaging',
        focus: 'Messaging',
        summary: 'Define message contracts early to keep services aligned.',
        diagram: 'Contract spine'
      },
      {
        title: 'Resilience cell design',
        focus: 'Reliability',
        summary: 'Isolate failure domains so recovery is predictable.',
        diagram: 'Cell map'
      },
      {
        title: 'Observability mesh',
        focus: 'Telemetry',
        summary: 'Unify traces, logs, and metrics around shared identifiers.',
        diagram: 'Telemetry mesh'
      }
    ]
  },
  {
    slug: 'devops',
    label: 'DevOps patterns',
    title: 'DevOps patterns',
    summary: 'Keep delivery flowing and the factory floor clean.',
    tagline: 'DevOps patterns accelerate delivery while keeping operations calm.',
    purpose: 'This category exists to keep delivery flow, automation, and operational discipline moving forward quickly without creating a messy factory floor.',
    diagramTitle: 'Flow and feedback loop',
    diagramNodes: ['Plan', 'Build', 'Release', 'Learn'],
    related: ['enablement', 'complexity', 'distributed-architecture'],
    examples: [
      {
        title: 'Progressive delivery',
        focus: 'Release flow',
        summary: 'Ship in guarded increments with fast rollback points.',
        diagram: 'Rollout stages'
      },
      {
        title: 'Pipeline standardization',
        focus: 'Automation',
        summary: 'Create a shared delivery pipeline that scales across teams.',
        diagram: 'Pipeline chain'
      },
      {
        title: 'Clean handoffs',
        focus: 'Operations',
        summary: 'Define operational readiness checks before change goes live.',
        diagram: 'Handoff gate'
      }
    ]
  }
];

export const categoryOrder = ['strategic', 'technical', 'devops', 'enablement', 'discovery'];

export const categoryMetadata = {
  strategic: {
    key: 'strategic',
    title: 'Strategic patterns',
    summary: 'Program-level decisions, sequencing, and migration direction.',
    colorKey: 'strategic'
  },
  technical: {
    key: 'technical',
    title: 'Technical patterns',
    summary: 'Engineering architecture and implementation patterns.',
    colorKey: 'technical'
  },
  devops: {
    key: 'devops',
    title: 'DevOps patterns',
    summary: 'Delivery flow, reliability, and operational execution patterns.',
    colorKey: 'devops'
  },
  enablement: {
    key: 'enablement',
    title: 'Enablement patterns',
    summary: 'Team acceleration patterns that remove friction.',
    colorKey: 'enablement'
  },
  discovery: {
    key: 'discovery',
    title: 'Discovery patterns',
    summary: 'Assessment and mapping patterns that shape better decisions.',
    colorKey: 'discovery'
  }
};

export const subcategoryMetadata = {
  strategic: {
    'modernization-strategy': {
      key: 'modernization-strategy',
      title: 'Modernization strategy',
      summary: 'Patterns that shape migration direction and sequencing.'
    },
    'modernization-guardrails': {
      key: 'modernization-guardrails',
      title: 'Modernization guardrails',
      summary: 'Patterns that define boundaries, constraints, and safety rules.'
    },
    'architecture-boundaries': {
      key: 'architecture-boundaries',
      title: 'Architecture boundaries',
      summary: 'Patterns that clarify system and domain boundaries.'
    },
    'platform-direction': {
      key: 'platform-direction',
      title: 'Platform direction',
      summary: 'Patterns that guide platform strategy and sequencing.'
    },
    'integration-architecture': {
      key: 'integration-architecture',
      title: 'Integration architecture',
      summary: 'Patterns that shape integration strategy and topology.'
    },
    guardrails: {
      key: 'guardrails',
      title: 'Guardrails',
      summary: 'Patterns that set non-negotiable rules for modernization.'
    },
    'contract-strategy': {
      key: 'contract-strategy',
      title: 'Contract strategy',
      summary: 'Patterns that define how contracts and interfaces evolve.'
    },
    'top-level-architecture': {
      key: 'top-level-architecture',
      title: 'Top-level architecture',
      summary: 'Patterns that shape the overall system structure.'
    },
    'transition-strategy': {
      key: 'transition-strategy',
      title: 'Transition strategy',
      summary: 'Patterns for moving workloads between old and new platforms.'
    },
    'release-safety': {
      key: 'release-safety',
      title: 'Release safety',
      summary: 'Patterns that reduce risk during cutover and rollback.'
    },
    'data-transition': {
      key: 'data-transition',
      title: 'Data transition',
      summary: 'Patterns for data migration, synchronization, and reconciliation.'
    },
    'delivery-strategy': {
      key: 'delivery-strategy',
      title: 'Delivery strategy',
      summary: 'Patterns for sequencing work into safe incremental releases.'
    },
    'validation-strategy': {
      key: 'validation-strategy',
      title: 'Validation strategy',
      summary: 'Patterns that validate system behavior during migration.'
    },
    compatibility: {
      key: 'compatibility',
      title: 'Compatibility',
      summary: 'Patterns that keep old and new systems interoperable.'
    }
  },
  technical: {
    'messaging-based-patterns': {
      key: 'messaging-based-patterns',
      title: 'Messaging-based patterns',
      summary: 'Patterns for asynchronous collaboration and event-driven coordination.'
    },
    'contract-patterns': {
      key: 'contract-patterns',
      title: 'Contract patterns',
      summary: 'Patterns that stabilize integration through explicit contracts.'
    },
    'ai-readiness': {
      key: 'ai-readiness',
      title: 'AI readiness',
      summary: 'Patterns that enable AI workloads and responsible usage.'
    },
    'performance-patterns': {
      key: 'performance-patterns',
      title: 'Performance patterns',
      summary: 'Patterns that improve performance and efficiency.'
    },
    'api-patterns': {
      key: 'api-patterns',
      title: 'API patterns',
      summary: 'Patterns for API composition and boundary management.'
    },
    operability: {
      key: 'operability',
      title: 'Operability',
      summary: 'Patterns that improve run-time reliability and diagnostics.'
    },
    'file-processing': {
      key: 'file-processing',
      title: 'File processing',
      summary: 'Patterns for durable, scalable file workflows.'
    },
    'platform-implementation': {
      key: 'platform-implementation',
      title: 'Platform implementation',
      summary: 'Patterns that shape platform building blocks.'
    },
    reliability: {
      key: 'reliability',
      title: 'Reliability',
      summary: 'Patterns that strengthen resilience and stability.'
    },
    'workflow-consistency': {
      key: 'workflow-consistency',
      title: 'Workflow consistency',
      summary: 'Patterns that align process and workflow design.'
    }
  },
  devops: {
    'delivery-flow': {
      key: 'delivery-flow',
      title: 'Delivery flow',
      summary: 'Patterns for consistent CI/CD and release throughput.'
    },
    'operational-excellence': {
      key: 'operational-excellence',
      title: 'Operational excellence',
      summary: 'Patterns for reliability, observability, and operational maturity.'
    },
    'security-delivery': {
      key: 'security-delivery',
      title: 'Security delivery',
      summary: 'Patterns for integrating security into release flow.'
    },
    finops: {
      key: 'finops',
      title: 'FinOps',
      summary: 'Patterns for cost transparency and optimization.'
    }
  },
  enablement: {
    'platform-acceleration': {
      key: 'platform-acceleration',
      title: 'Platform acceleration',
      summary: 'Patterns that remove onboarding friction and speed execution.'
    },
    governance: {
      key: 'governance',
      title: 'Governance',
      summary: 'Patterns for guardrails, policy, and decision ownership.'
    },
    dx: {
      key: 'dx',
      title: 'Developer experience',
      summary: 'Patterns that improve developer productivity and feedback.'
    },
    'team-topologies': {
      key: 'team-topologies',
      title: 'Team topologies',
      summary: 'Patterns for organizing teams and responsibilities.'
    },
    'ai-adoption': {
      key: 'ai-adoption',
      title: 'AI adoption',
      summary: 'Patterns that enable AI adoption across teams.'
    },
    'ai-ops': {
      key: 'ai-ops',
      title: 'AI ops',
      summary: 'Patterns for operating AI systems responsibly.'
    }
  },
  discovery: {
    'assessment-mapping': {
      key: 'assessment-mapping',
      title: 'Assessment mapping',
      summary: 'Patterns that reveal constraints and opportunities before implementation.'
    },
    'ai-discovery': {
      key: 'ai-discovery',
      title: 'AI discovery',
      summary: 'Patterns that accelerate discovery using AI analysis.'
    },
    'business-alignment': {
      key: 'business-alignment',
      title: 'Business alignment',
      summary: 'Patterns that align modernization with business outcomes.'
    },
    'domain-modeling': {
      key: 'domain-modeling',
      title: 'Domain modeling',
      summary: 'Patterns for mapping domains, capabilities, and flows.'
    },
    alignment: {
      key: 'alignment',
      title: 'Alignment',
      summary: 'Patterns that build shared understanding and consensus.'
    },
    'platform-strategy': {
      key: 'platform-strategy',
      title: 'Platform strategy',
      summary: 'Patterns that shape platform modernization decisions.'
    },
    'program-governance': {
      key: 'program-governance',
      title: 'Program governance',
      summary: 'Patterns for governing modernization programs.'
    }
  }
};

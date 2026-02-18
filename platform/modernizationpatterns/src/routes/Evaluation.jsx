import { useState } from 'react';
import { Link } from 'react-router-dom';

// Evaluation data grouped by area
const evaluationData = [
  {
    area: 'General Features',
    requirements: [
      { name: 'once-and-only-once delivery', ais: 'Partial', aisPlus: 'Yes', comment: 'ASB dedupe is scoped; code can enforce end-to-end.' },
      { name: 'at-least-once delivery', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'best effort delivery', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Retry support', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Backoff retry support', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Throughput throttling', ais: 'Partial', aisPlus: 'Yes', comment: 'LA throttles exist; code can shape load precisely.' },
      { name: 'Message delivery scheduling', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Message expiration', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Duplicate detection / Idempotency', ais: 'Partial', aisPlus: 'Yes', comment: 'Built-in varies by connector + workflow design.' },
      { name: 'Max supported message size / claims-check', ais: 'Partial', aisPlus: 'Yes', comment: 'Use Blob claims-check pattern either way.' },
      { name: 'Transport translation (REST↔SOAP, etc.)', ais: 'Yes', aisPlus: 'Yes', comment: 'LA connectors help; code gives full control.' },
      { name: 'Message payload versioning', ais: 'Partial', aisPlus: 'Yes', comment: 'LA supports schemas/versions; code enforces contracts.' },
      { name: 'Message encryption', ais: 'Yes', aisPlus: 'Yes', comment: 'Uses platform encryption + app-layer options.' },
      { name: 'Locking transport / subscriber controls', ais: 'Yes', aisPlus: 'Yes', comment: 'ASB + IAM either way.' },
      { name: 'Transactional handling (durability)', ais: 'Partial', aisPlus: 'Yes', comment: 'LA durability is workflow/state; code adds outbox, etc.' },
      { name: 'Poison message handling', ais: 'Partial', aisPlus: 'Yes', comment: 'ASB DLQ exists; handling is more complete in code.' },
      { name: 'Ack on sending', ais: 'Partial', aisPlus: 'Yes', comment: 'Often "accepted" vs "processed" semantics.' }
    ]
  },
  {
    area: 'Messaging Patterns',
    requirements: [
      { name: 'Message transformation', ais: 'Yes', aisPlus: 'Yes', comment: 'LA has built-ins; code gives stronger testability.' },
      { name: 'Long running processes (Saga/Process manager)', ais: 'Partial', aisPlus: 'Yes', comment: 'LA stateful ok; complex sagas easier in code.' },
      { name: 'Async (fire and forget)', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Async response / request-reply', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Competing consumers', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Publish-subscribe', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Static routing', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Convention-based routing', ais: 'Partial', aisPlus: 'Yes', comment: 'Code frameworks typically stronger here.' },
      { name: 'Message sequencing / ordered handling', ais: 'Partial', aisPlus: 'Yes', comment: 'ASB sessions vs app-level sequencing.' },
      { name: 'Message aggregation', ais: 'Partial', aisPlus: 'Yes', comment: 'LA possible; code is more explicit and testable.' },
      { name: 'Polymorphic dispatch', ais: 'Partial', aisPlus: 'Yes', comment: 'Usually easier to express in code.' },
      { name: 'Outbox pattern', ais: 'No', aisPlus: 'Yes', comment: 'Outbox is code/data pattern, not LA-native.' }
    ]
  },
  {
    area: 'Operational Patterns',
    requirements: [
      { name: 'Replay/resubmit events safely', ais: 'Partial', aisPlus: 'Yes', comment: 'LA resubmit exists; code supports richer replay controls.' },
      { name: 'Modify in-flight workflow behavior', ais: 'No', aisPlus: 'Partial', comment: 'LA has limits; code can version handlers more predictably.' },
      { name: 'Backpressure / load shedding', ais: 'Partial', aisPlus: 'Yes', comment: 'Code can implement policies per endpoint/consumer.' }
    ]
  },
  {
    area: 'Observability',
    requirements: [
      { name: 'End-to-end correlation across steps', ais: 'Partial', aisPlus: 'Yes', comment: 'LA has run history; cross-service tracing stronger in code.' },
      { name: 'Standardized logs/metrics/traces', ais: 'Partial', aisPlus: 'Yes', comment: 'Consistency depends on connector + config.' },
      { name: 'Conversation tracing/debugging', ais: 'Partial', aisPlus: 'Yes', comment: 'LA run history helps; code supports richer tooling.' },
      { name: 'Message audit support', ais: 'Partial', aisPlus: 'Yes', comment: 'Both can do it; code typically more configurable.' }
    ]
  },
  {
    area: 'Monitoring',
    requirements: [
      { name: 'Endpoint health checks', ais: 'Partial', aisPlus: 'Yes', comment: 'LA can implement checks; code can standardize probes.' },
      { name: 'Dependency health checks', ais: 'Partial', aisPlus: 'Yes', comment: 'Usually a platform+code responsibility.' },
      { name: 'Alerting/notifications', ais: 'Yes', aisPlus: 'Yes', comment: '' }
    ]
  },
  {
    area: 'Testability',
    requirements: [
      { name: 'Unit testability', ais: 'Partial', aisPlus: 'Yes', comment: 'LA testing is harder; code is straightforward.' },
      { name: 'Performance testability', ais: 'Partial', aisPlus: 'Yes', comment: 'LA load testing is doable; code is more controllable.' },
      { name: 'Traceability (in-process)', ais: 'Partial', aisPlus: 'Yes', comment: '' },
      { name: 'Traceability (cross-process)', ais: 'Partial', aisPlus: 'Yes', comment: '' }
    ]
  },
  {
    area: 'DevOps / Delivery',
    requirements: [
      { name: 'CI/CD support', ais: 'Yes', aisPlus: 'Yes', comment: 'LA supports DevOps deployment patterns.' },
      { name: 'Local dev/test loop', ais: 'Yes', aisPlus: 'Yes', comment: 'Standard supports local development.' },
      { name: 'Containerized deployment option', ais: 'Yes', aisPlus: 'Yes', comment: 'LA Standard supports containers.' }
    ]
  },
  {
    area: 'Runtime Fit',
    requirements: [
      { name: 'Aligns with Kubernetes operating model', ais: 'Partial', aisPlus: 'Yes', comment: 'LA can containerize; code services fit naturally.' }
    ]
  },
  {
    area: 'Integration',
    requirements: [
      { name: '3rd-party connectors (OOB)', ais: 'Yes', aisPlus: 'Yes', comment: 'AIS+ includes LA connectors; code-first offers programmatic alternatives.' },
      { name: 'Database connector', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'HTTP connector', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Azure Service Bus connector', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Cosmos DB connector', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'SOAP connector', ais: 'Yes', aisPlus: 'Yes', comment: '' },
      { name: 'Salesforce connector', ais: 'Yes', aisPlus: 'Yes', comment: 'AIS+ includes LA connector; custom code gives more control.' }
    ]
  },
  {
    area: 'AI / Data',
    requirements: [
      { name: 'AI/ML integration', ais: 'Partial', aisPlus: 'Yes', comment: 'AIS+ makes AI a first-class platform capability.' },
      { name: 'Big data feeding / batch movement', ais: 'Partial', aisPlus: 'Yes', comment: 'AIS+ uses Data Factory patterns more naturally.' }
    ]
  },
  {
    area: 'Agentic SDLC',
    requirements: [
      { name: 'Copilot/agents integrated into delivery', ais: 'Partial', aisPlus: 'Yes', comment: 'AIS+ enables code-first agentic workflows.' },
      { name: 'Guardrails via repo instructions/patterns', ais: 'Partial', aisPlus: 'Yes', comment: 'AIS+ can enforce via templates + policy gates.' }
    ]
  }
];

function StatusBadge({ status }) {
  const className = `status-badge status-${status.toLowerCase()}`;
  return <span className={className}>{status}</span>;
}

function RequirementRow({ requirement }) {
  return (
    <tr>
      <td className="requirement-name">{requirement.name}</td>
      <td className="status-cell">
        <StatusBadge status={requirement.ais} />
      </td>
      <td className="status-cell">
        <StatusBadge status={requirement.aisPlus} />
      </td>
      <td className="comment-cell">{requirement.comment}</td>
    </tr>
  );
}

function AreaSection({ section, isOpen, onToggle }) {
  return (
    <div className="area-section">
      <button className="area-header" onClick={onToggle}>
        <span className="area-title">{section.area}</span>
        <span className="area-count">({section.requirements.length} requirements)</span>
        <span className="toggle-icon">{isOpen ? '▼' : '▶'}</span>
      </button>
      
      {isOpen && (
        <div className="area-content">
          <table className="evaluation-table">
            <thead>
              <tr>
                <th className="requirement-col">Requirement</th>
                <th className="status-col">AIS</th>
                <th className="status-col">AIS+</th>
                <th className="comment-col">Comment</th>
              </tr>
            </thead>
            <tbody>
              {section.requirements.map((req, idx) => (
                <RequirementRow key={idx} requirement={req} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default function Evaluation() {
  const [openSections, setOpenSections] = useState(
    evaluationData.reduce((acc, section) => {
      acc[section.area] = true; // All sections open by default
      return acc;
    }, {})
  );

  const toggleSection = (area) => {
    setOpenSections(prev => ({
      ...prev,
      [area]: !prev[area]
    }));
  };

  return (
    <section className="page evaluation-page">
      <div className="evaluation-header">
        <Link to="/" className="back-link">← Back to Patterns</Link>
        <h1>Integration Platform Evaluation</h1>
        <p className="page-subtitle">Comparing Azure Integration Services (AIS) vs AIS+</p>
      </div>

      <div className="evaluation-summary">
        <div className="comparison-grid">
          <div className="platform-column ais-column">
            <h2>Azure Integration Services (AIS)</h2>
            <p className="platform-description">
              Microsoft's comprehensive cloud-based integration platform for connecting applications, data, and services across on-premises and cloud environments.
            </p>
            <div className="features-list">
              <h3>Core Components</h3>
              <ul>
                <li><strong>Azure API Management (APIM)</strong> - API gateway with security, versioning, throttling, and analytics</li>
                <li><strong>Azure Logic Apps</strong> - No-code/low-code workflow automation with 600+ pre-built connectors</li>
                <li><strong>Azure Service Bus</strong> - Enterprise messaging with queues, topics, and dead-letter queues</li>
                <li><strong>Azure Event Grid</strong> - Event routing service for real-time event-driven architectures</li>
                <li><strong>Azure Functions</strong> - Serverless compute for event-driven execution</li>
                <li><strong>Azure Data Factory (ADF)</strong> - Data integration and ETL/ELT service for data pipelines</li>
                <li><strong>Azure Storage / Blob</strong> - Data persistence and claims-check patterns</li>
                <li><strong>Azure IoT Hub</strong> - IoT device communication and management</li>
              </ul>
              <h3>Key Strengths</h3>
              <ul>
                <li>Speed to market through visual workflow designer</li>
                <li>600+ pre-built connectors (Salesforce, SAP, Office 365, etc.)</li>
                <li>Ideal for citizen developers and rapid prototyping</li>
                <li>Comprehensive managed services for integration scenarios</li>
              </ul>
            </div>
          </div>

          <div className="platform-column ais-plus-column">
            <h2>Azure Integration Services+ (AIS+)</h2>
            <p className="platform-description">
              <strong>All of AIS</strong> plus code-first patterns, architectural guardrails, and modern engineering practices for enterprise-scale integration.
            </p>
            <div className="features-list">
              <h3>Includes All AIS Components</h3>
              <ul className="inherited-features">
                <li>Azure API Management (APIM)</li>
                <li>Azure Logic Apps (where it adds value)</li>
                <li>Azure Service Bus</li>
                <li>Azure Event Grid</li>
                <li>Azure Functions</li>
                <li>Azure Data Factory (ADF)</li>
                <li>Azure Storage / Blob</li>
                <li>Azure IoT Hub</li>
              </ul>
              <h3>Plus Additional Capabilities</h3>
              <ul className="additional-features">
                <li><strong>Patterns framework</strong> - Architectural guardrails (outbox, saga, anti-corruption layer)</li>
                <li><strong>Container Based Workload</strong> - Kubernetes/ARO - based microservices processing</li>
                <li><strong>Azure AI Foundry</strong> - AI/ML integration and agentic workflows</li>
                <li><strong>Agentic Development with GitHub Copilot + SpecKit</strong> - Agentic engineering with repository instructions</li>
                <li><strong>Consistent CI/CD</strong> - ADO Pipelines or GitHub Actions(preferred), policy gates, automated testing</li>
                <li><strong>Managed Identity everywhere</strong> - Zero secrets, secure by default</li>
                <li><strong>Enterprise testing</strong> - Unit tests, integration tests, contract tests</li>
                <li><strong>Advanced observability</strong> - Structured logging, distributed tracing, Application Insights</li>
              </ul>
              <h3>Key Strengths</h3>
              <ul>
                <li>Testability, observability, and architectural consistency</li>
                <li>Choose the right tool for each scenario (visual OR code)</li>
                <li>AI-ready by design with transparent data flows</li>
                <li>Production-grade patterns for complex business logic</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="summary-callout">
          <h3>The AIS+ Advantage</h3>
          <p>
            AIS+ is <strong>not a replacement</strong> for Azure Integration Services—it's an <strong>extension</strong>. 
            Organizations get all the managed services and connectors of AIS, plus the ability to implement code-first patterns 
            where business logic complexity demands testability, maintainability, and architectural rigor. 
            Use Logic Apps for visual workflows and partner integrations; use code-first services for complex sagas, 
            domain-driven design, and AI-enabled integration scenarios.
          </p>
        </div>

        <div className="legend-section">
          <h3>Status Legend</h3>
          <div className="legend-items">
            <div className="legend-item">
              <StatusBadge status="Yes" />
              <span>Fully supported with mature tooling and patterns</span>
            </div>
            <div className="legend-item">
              <StatusBadge status="Partial" />
              <span>Supported with limitations or additional configuration</span>
            </div>
            <div className="legend-item">
              <StatusBadge status="No" />
              <span>Not supported or impractical with current tooling</span>
            </div>
          </div>
        </div>
      </div>

      <div className="evaluation-sections">
        {evaluationData.map((section) => (
          <AreaSection
            key={section.area}
            section={section}
            isOpen={openSections[section.area]}
            onToggle={() => toggleSection(section.area)}
          />
        ))}
      </div>

      <div className="evaluation-footer">
        <p className="footnote">
          <strong>About this evaluation:</strong> Azure Integration Services components are documented by Microsoft and described in 
          <a href="https://thecodeblogger.com/2025/03/10/high-level-overview-of-azure-integration-services/" target="_blank" rel="noopener noreferrer"> this comprehensive overview</a>. 
          Logic Apps Standard is built on single-tenant runtime using Azure Functions extensibility model, 
          supporting container deployment and CI/CD patterns. AIS+ <strong>includes all AIS capabilities</strong> and extends them with 
          code-first patterns, architectural frameworks, and modern engineering practices.
        </p>
        <p className="footnote">
          <strong>Choosing between AIS and AIS+:</strong> AIS+ enables organizations to use the right tool for each integration scenario—visual workflows (Logic Apps) 
          for rapid prototyping and partner integrations, code-first services for complex business logic, testable sagas, and AI-enabled workflows. 
          Both approaches leverage the same Azure messaging and data infrastructure (Service Bus, Event Grid, API Management, Data Factory).
        </p>
      </div>
    </section>
  );
}

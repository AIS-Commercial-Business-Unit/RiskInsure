import { Link, useParams } from 'react-router-dom';
import { getAgenticLifecycleBySlug, getAgenticPillarBySlug, getAgenticLifecyclePhases } from '../data/agenticRepository.js';

function ListCard({ label, items, numbered }) {
  if (!items?.length) return null;
  return (
    <div className="ae-dp-card">
      <div className="ae-dp-card-label">{label}</div>
      <ul className={`ae-dp-card-list${numbered ? ' ae-dp-card-list--numbered' : ''}`}>
        {items.map((item, i) => <li key={i}>{item}</li>)}
      </ul>
    </div>
  );
}

function ChipCard({ label, items, accent }) {
  if (!items?.length) return null;
  return (
    <div className="ae-dp-card">
      <div className="ae-dp-card-label">{label}</div>
      <div className="ae-dp-chip-grid">
        {items.map((item, i) => (
          <span key={i} className={`ae-dp-chip${accent ? ' ae-dp-chip--accent' : ''}`}>{item}</span>
        ))}
      </div>
    </div>
  );
}

export default function AgenticLifecycle() {
  const { slug } = useParams();
  const phase = getAgenticLifecycleBySlug(slug);

  if (!phase) {
    return (
      <section className="ae-dp">
        <div className="ae-dp-card" style={{ textAlign: 'center', padding: '3rem' }}>
          <h2 style={{ color: '#020617' }}>Lifecycle phase not found</h2>
          <p style={{ color: '#64748B' }}>No lifecycle phase matches "{slug}".</p>
          <Link className="ae-dp-back" to="/agentic">← Back to Operating Model</Link>
        </div>
      </section>
    );
  }

  // Determine phase number from the lifecycle order
  const allPhases = getAgenticLifecyclePhases();
  const phaseIndex = allPhases.findIndex((p) => p.slug === slug);
  const phaseNum = phaseIndex >= 0 ? phaseIndex + 1 : null;

  const relatedPillars = (phase.relatedPillars || [])
    .map(getAgenticPillarBySlug)
    .filter(Boolean);

  return (
    <section className="ae-dp">
      {/* Breadcrumb */}
      <nav className="ae-dp-breadcrumb">
        <Link to="/agentic">Operating Model</Link>
        <span>/</span>
        <span>Lifecycle</span>
        <span>/</span>
        <span>{phase.title}</span>
      </nav>

      {/* Hero */}
      <div className="ae-dp-hero">
        <div className="ae-dp-eyebrow">
          Lifecycle phase{phaseNum ? ` · Phase ${phaseNum}` : ''}
        </div>
        <h1 className="ae-dp-title">{phase.title}</h1>
        <p className="ae-dp-summary">{phase.summary}</p>
      </div>

      {/* Why It Matters */}
      {phase.whyItMatters && (
        <div className="ae-dp-strategy">
          <div className="ae-dp-strategy-label">Why it matters</div>
          <p className="ae-dp-strategy-text">{phase.whyItMatters}</p>
        </div>
      )}

      {/* Key Decisions + Major Activities */}
      <div className="ae-dp-two-col">
        <ListCard label="Key Decisions" items={phase.keyDecisions} numbered />
        <ListCard label="Major Activities" items={phase.majorActivities} />
      </div>

      {/* Human Roles + Agent Roles */}
      <div className="ae-dp-two-col">
        <ChipCard label="Human Roles" items={phase.humanRoles} />
        <ChipCard label="Agent Roles" items={phase.agentRoles} accent />
      </div>

      {/* Inputs + Outputs */}
      <div className="ae-dp-two-col">
        <ListCard label="Inputs" items={phase.inputs} />
        <ListCard label="Outputs" items={phase.outputs} />
      </div>

      {/* Artifacts, Controls, Metrics */}
      <div className="ae-dp-three-col">
        <ChipCard label="Artifacts" items={phase.artifacts} />
        <ChipCard label="Controls" items={phase.controls} />
        <ChipCard label="Metrics" items={phase.metrics} accent />
      </div>

      {/* Related Pillars */}
      {relatedPillars.length > 0 && (
        <div className="ae-dp-card">
          <div className="ae-dp-card-label">Related Pillars</div>
          <div className="ae-dp-related-grid">
            {relatedPillars.map((p) => (
              <Link key={p.slug} to={`/agentic/pillar/${p.slug}`} className="ae-dp-related-link">
                {p.title}
                <span className="ae-dp-related-arrow">→</span>
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* Back */}
      <Link to="/agentic" className="ae-dp-back">← Back to Operating Model</Link>
    </section>
  );
}

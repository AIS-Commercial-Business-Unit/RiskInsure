import { Link, useParams } from 'react-router-dom';
import { getAgenticPillarBySlug, getAgenticLifecycleBySlug, getAgenticPolishedDiagrams } from '../data/agenticRepository.js';

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

export default function AgenticPillar() {
  const { slug } = useParams();
  const pillar = getAgenticPillarBySlug(slug);

  if (!pillar) {
    return (
      <section className="ae-dp">
        <div className="ae-dp-card" style={{ textAlign: 'center', padding: '3rem' }}>
          <h2 style={{ color: '#020617' }}>Pillar not found</h2>
          <p style={{ color: '#64748B' }}>No agentic pillar matches "{slug}".</p>
          <Link className="ae-dp-back" to="/agentic">← Back to Operating Model</Link>
        </div>
      </section>
    );
  }

  const relatedPhases = (pillar.relatedLifecyclePhases || [])
    .map(getAgenticLifecycleBySlug)
    .filter(Boolean);

  const polishedDiagrams = slug === 'governance-risk' ? getAgenticPolishedDiagrams() : null;
  const governanceDiagram = polishedDiagrams?.governanceVisual;

  return (
    <section className="ae-dp">
      {/* Breadcrumb */}
      <nav className="ae-dp-breadcrumb">
        <Link to="/agentic">Operating Model</Link>
        <span>/</span>
        <span>Pillars</span>
        <span>/</span>
        <span>{pillar.title}</span>
      </nav>

      {/* Hero */}
      <div className="ae-dp-hero">
        <div className="ae-dp-eyebrow">Cross-cutting pillar</div>
        <h1 className="ae-dp-title">{pillar.title}</h1>
        <p className="ae-dp-summary">{pillar.summary}</p>
      </div>

      {/* Why It Matters */}
      {pillar.whyItMatters && (
        <div className="ae-dp-strategy">
          <div className="ae-dp-strategy-label">Why it matters</div>
          <p className="ae-dp-strategy-text">{pillar.whyItMatters}</p>
        </div>
      )}

      {/* Executive Questions */}
      <ListCard label="Executive Questions" items={pillar.executiveQuestions} numbered />

      {/* Key Decisions + Major Activities */}
      <div className="ae-dp-two-col">
        <ListCard label="Key Decisions" items={pillar.keyDecisions} />
        <ListCard label="Major Activities" items={pillar.majorActivities} />
      </div>

      {/* Model Components */}
      <ChipCard label="Model Components" items={pillar.modelComponents} />

      {/* Human Roles + Agent Roles */}
      <div className="ae-dp-two-col">
        <ChipCard label="Human Roles" items={pillar.humanRoles} />
        <ChipCard label="Agent Roles" items={pillar.agentRoles} accent />
      </div>

      {/* Inputs + Outputs */}
      <div className="ae-dp-two-col">
        <ListCard label="Inputs" items={pillar.inputs} />
        <ListCard label="Outputs" items={pillar.outputs} />
      </div>

      {/* Artifacts, Controls, Metrics */}
      <div className="ae-dp-three-col">
        <ChipCard label="Artifacts" items={pillar.artifacts} />
        <ChipCard label="Controls" items={pillar.controls} />
        <ChipCard label="Metrics" items={pillar.metrics} accent />
      </div>

      {/* Related Lifecycle Phases */}
      {relatedPhases.length > 0 && (
        <div className="ae-dp-card">
          <div className="ae-dp-card-label">Related Lifecycle Phases</div>
          <div className="ae-dp-related-grid">
            {relatedPhases.map((p) => (
              <Link key={p.slug} to={`/agentic/lifecycle/${p.slug}`} className="ae-dp-related-link">
                {p.title}
                <span className="ae-dp-related-arrow">→</span>
              </Link>
            ))}
          </div>
        </div>
      )}

      {/* Governance Drill-Down (governance-risk pillar only) */}
      {governanceDiagram && (
        <div className="ae-dp-card">
          <div className="ae-dp-card-label">Governance Drill-Down</div>
          <p style={{ fontSize: '0.875rem', color: '#64748B', margin: '0 0 1rem' }}>
            How autonomy is bounded and trust maintained across risk governance, accountability, controls, compliance, and enablement.
          </p>
          <div className="ae-diagram ae-diagram--governance">
            <div className="ae-diagram-header">
              <h4 className="ae-diagram-title">{governanceDiagram.title}</h4>
              <p className="ae-diagram-subtitle">Layered governance model — each layer builds on the one below</p>
            </div>
            {governanceDiagram.bands?.map((band, idx) => {
              const isCore = band.label.toLowerCase() === 'core';
              return (
                <div key={idx} className={isCore ? 'ae-band ae-band--core' : 'ae-band'}>
                  <div className="ae-band-label">{band.label}</div>
                  <div className="ae-band-items">
                    {band.items.map((item, i) => (
                      <span key={i} className="ae-chip">{item}</span>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Back */}
      <Link to="/agentic" className="ae-dp-back">← Back to Operating Model</Link>
    </section>
  );
}

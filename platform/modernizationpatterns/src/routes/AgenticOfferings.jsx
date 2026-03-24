import { Link } from 'react-router-dom';
import { getAgenticOfferings, getAgenticPillarBySlug, getAgenticLifecycleBySlug, getAgenticCta } from '../data/agenticRepository.js';

export default function AgenticOfferings() {
  const offerings = getAgenticOfferings();
  const cta = getAgenticCta();

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/agentic">Agentic Engineering</Link>
        <span>/</span>
        <span>Offerings</span>
      </div>

      <h2>Offerings</h2>
      <p className="index-summary">Commercial engagement models for adopting agentic engineering.</p>

      {offerings.map((offering) => (
        <div key={offering.slug} className="content-section">
          <h3>{offering.title}</h3>
          <p>{offering.summary}</p>

          {offering.duration && (
            <p><strong>Duration:</strong> {offering.duration}</p>
          )}

          {offering.whoItsFor && (
            <>
              <h4>Who It's For</h4>
              <ul>
                {offering.whoItsFor.map((item, idx) => (
                  <li key={idx}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {offering.outcomes && (
            <>
              <h4>Outcomes</h4>
              <ul>
                {offering.outcomes.map((item, idx) => (
                  <li key={idx}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {offering.deliverables && (
            <>
              <h4>Deliverables</h4>
              <ul>
                {offering.deliverables.map((item, idx) => (
                  <li key={idx}>{item}</li>
                ))}
              </ul>
            </>
          )}

          {offering.relatedPillars?.length > 0 && (
            <>
              <h4>Related Pillars</h4>
              <ul>
                {offering.relatedPillars.map((slug) => {
                  const p = getAgenticPillarBySlug(slug);
                  return p ? (
                    <li key={slug}><Link to={`/agentic/pillar/${slug}`}>{p.title}</Link></li>
                  ) : null;
                })}
              </ul>
            </>
          )}

          {offering.relatedLifecyclePhases?.length > 0 && (
            <>
              <h4>Related Lifecycle Phases</h4>
              <ul>
                {offering.relatedLifecyclePhases.map((slug) => {
                  const lc = getAgenticLifecycleBySlug(slug);
                  return lc ? (
                    <li key={slug}><Link to={`/agentic/lifecycle/${slug}`}>{lc.title}</Link></li>
                  ) : null;
                })}
              </ul>
            </>
          )}
        </div>
      ))}

      {cta?.cards?.length > 0 && (
        <div className="content-section" style={{ marginTop: '2rem' }}>
          <h3>{cta.title}</h3>
          <p>{cta.summary}</p>
          <div className="pattern-card-grid">
            {cta.cards.map((card, idx) => (
              <div key={idx} className="pattern-card">
                <div className="pattern-card-body">
                  <h4 className="pattern-card-title">{card.title}</h4>
                  <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.5rem' }}>{card.audience}</p>
                  <p className="pattern-card-summary">{card.description}</p>
                  <p style={{ fontWeight: 600, marginTop: '0.5rem' }}>{card.ctaLabel} &rarr;</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}

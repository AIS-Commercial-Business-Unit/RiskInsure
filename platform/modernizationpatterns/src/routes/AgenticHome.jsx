import { Link } from 'react-router-dom';
import { getAgenticHomepage, getAgenticCta, getAgenticHomepagePolish, getAgenticSectionAnchors } from '../data/agenticRepository.js';

function AnchorNav({ anchors }) {
  if (!anchors?.length) return null;
  return (
    <nav className="ae-anchor-nav">
      {anchors.map((a) => (
        <a key={a.id} href={`#${a.id}`}>{a.label}</a>
      ))}
    </nav>
  );
}

export default function AgenticHome() {
  const homepage = getAgenticHomepage();
  const cta = getAgenticCta();
  const polish = getAgenticHomepagePolish();
  const anchors = getAgenticSectionAnchors();

  if (!homepage) {
    return (
      <section className="page">
        <h2>Agentic Engineering</h2>
        <p>Content not found.</p>
      </section>
    );
  }

  const hero = polish?.hero ?? homepage.hero;
  const proofPoints = polish?.proofPoints ?? homepage.proofPoints;
  const { sections, featuredLinks } = homepage;

  /* Split sections array: first is "why", second is "what makes this different" */
  const whySection = sections?.[0];
  const diffSection = sections?.[1];

  return (
    <section className="ae-page page">
      {/* Hero Band */}
      <div className="ae-hero" id="overview">
        <div className="ae-hero-inner">
          {hero.eyebrow && <p className="ae-eyebrow">{hero.eyebrow}</p>}
          <h2 className="ae-hero-title">{hero.title ?? hero.headline}</h2>
          <p className="ae-hero-summary">{hero.summary}</p>

          {hero.signalTitle && (
            <div className="ae-signal">
              <span className="ae-signal-label">{hero.signalTitle}</span>
              <span className="ae-signal-text">{hero.signalText}</span>
            </div>
          )}

          <div className="ae-hero-cta-row">
            {hero.primaryCta && (
              <Link to={hero.primaryCta.href} className="ae-btn-primary">{hero.primaryCta.label}</Link>
            )}
            {hero.secondaryCta && (
              <Link to={hero.secondaryCta.href} className="ae-btn-secondary">{hero.secondaryCta.label}</Link>
            )}
          </div>
        </div>
      </div>

      {/* Anchor Nav */}
      <AnchorNav anchors={anchors?.agenticHome} />

      <div className="ae-body">
        {/* Strategy Band — "Why this matters" + differentiators */}
        {(whySection || diffSection) && (
          <div className="ae-strategy-band">
            {whySection && (
              <>
                <h3>{whySection.title}</h3>
                <p>{whySection.body}</p>
              </>
            )}
            {proofPoints?.length > 0 && (
              <div className="ae-differentiators">
                {proofPoints.map((point, idx) => (
                  <div key={idx} className="ae-diff-item">
                    <span className="ae-diff-marker">&mdash;</span>
                    <span>{point}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* What makes this different — as a lighter section */}
        {diffSection && (
          <div className="ae-section">
            <h3 className="ae-section-title">{diffSection.title}</h3>
            <p className="ae-section-intro">{diffSection.body}</p>
          </div>
        )}

        {/* Featured Links */}
        {featuredLinks?.length > 0 && (
          <div className="ae-section">
            <h3 className="ae-section-title">Explore the model</h3>
            <p className="ae-section-intro">Drill into the components that make up the agentic engineering operating model.</p>
            <div className="ae-card-grid">
              {featuredLinks.map((link, idx) => (
                <Link key={idx} to={link.href} className="ae-card">
                  <h4 className="ae-card-title">{link.title}</h4>
                  <p className="ae-card-summary">{link.summary}</p>
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* CTA Band */}
        {cta?.cards?.length > 0 && (
          <div className="ae-cta-band" id="cta">
            <h3>{cta.title}</h3>
            <p>{cta.summary}</p>
            <div className="ae-cta-cards">
              {cta.cards.map((card, idx) => (
                <div key={idx} className="ae-cta-card">
                  <h4>{card.title}</h4>
                  <p className="ae-audience">{card.audience}</p>
                  <p>{card.description}</p>
                  <p className="ae-cta-label">{card.ctaLabel} &rarr;</p>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

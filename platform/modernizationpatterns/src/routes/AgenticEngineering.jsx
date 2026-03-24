import { useState, useRef, useEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { useNavigate } from 'react-router-dom';
import {
  getAgenticOverview, getAgenticPillars, getAgenticLifecyclePhases,
  getAgenticCta,
  getAgenticPolishedDiagrams,
  getAgenticRoles
} from '../data/agenticRepository.js';

function FixedTooltip({ anchorRef, position, children }) {
  const [style, setStyle] = useState(null);
  useEffect(() => {
    if (!anchorRef?.current) return;
    const r = anchorRef.current.getBoundingClientRect();
    if (position === 'right') {
      setStyle({ position: 'fixed', zIndex: 9999, left: r.right + 12, top: r.top });
    } else if (position === 'left') {
      setStyle({ position: 'fixed', zIndex: 9999, right: window.innerWidth - r.left + 12, top: r.top });
    } else if (position === 'above-left') {
      setStyle({ position: 'fixed', zIndex: 9999, left: r.left, bottom: window.innerHeight - r.top + 8 });
    } else {
      setStyle({ position: 'fixed', zIndex: 9999, left: r.left, bottom: window.innerHeight - r.top + 8 });
    }
  }, [anchorRef, position]);
  if (!style) return null;
  return createPortal(
    <div className="ae-om-fixed-tooltip" style={style}>{children}</div>,
    document.body
  );
}

function RoleTooltipContent({ role }) {
  return (
    <>
      <div className="ae-om-tooltip-title">{role.title}</div>
      <div className="ae-om-tooltip-purpose">{role.purpose}</div>
      {role.responsibilities?.length > 0 && (
        <ul className="ae-om-tooltip-list">
          {role.responsibilities.map((r, i) => <li key={i}>{r}</li>)}
        </ul>
      )}
    </>
  );
}

function PillarTooltipContent({ pillar }) {
  return (
    <>
      <div className="ae-om-tooltip-title">{pillar.title}</div>
      <div className="ae-om-tooltip-purpose">{pillar.summary}</div>
      {pillar.modelComponents?.length > 0 && (
        <ul className="ae-om-tooltip-list">
          {pillar.modelComponents.slice(0, 5).map((c, i) => <li key={i}>{c}</li>)}
        </ul>
      )}
    </>
  );
}

export default function AgenticEngineering() {
  const overview = getAgenticOverview();
  const pillars = getAgenticPillars();
  const phases = getAgenticLifecyclePhases();
  const cta = getAgenticCta();
  const polishedDiagrams = getAgenticPolishedDiagrams();
  const roles = getAgenticRoles();
  const navigate = useNavigate();

  const [hoveredRole, setHoveredRole] = useState(null);
  const [hoveredPillar, setHoveredPillar] = useState(null);
  const [hoveredPhase, setHoveredPhase] = useState(null);
  const hoveredRef = useRef(null);

  if (!overview) {
    return (
      <section className="page">
        <h2>Agentic Engineering</h2>
        <p>Content not found.</p>
      </section>
    );
  }

  const humanRoles = roles?.humanRoles ?? [];
  const agentRoles = roles?.agentRoles ?? [];
  const foundation = polishedDiagrams?.operatingModelVisual?.bands?.find(
    (b) => b.label === 'Foundation'
  )?.items ?? [];

  // Map pillar titles to pillar objects (for tooltip + drill-down)
  const pillarByTitle = Object.fromEntries(pillars.map((p) => [p.title, p]));
  // Also map with slight variation for "Human-Agent Collaboration" vs "Human + Agent Collaboration"
  const findPillar = (title) => {
    if (pillarByTitle[title]) return pillarByTitle[title];
    return pillars.find((p) => title.includes(p.title) || p.title.includes(title.replace(' + ', '-').replace(' & ', ' & ')));
  };

  // Which lifecycle phase slugs does a pillar relate to?
  const highlightedPhaseSlugs = hoveredPillar
    ? (findPillar(hoveredPillar)?.relatedLifecyclePhases ?? [])
    : [];

  return (
    <section className="ae-page page">
      {/* ===== Big 4 Operating Model ===== */}
      <div className="ae-om" id="overview">
        {/* Header Card */}
        <div className="ae-om-header">
          <div>
            <div className="ae-om-eyebrow">Enterprise modernization operating model</div>
            <h1 className="ae-om-title">{overview.title}</h1>
            <p className="ae-om-summary">{overview.summary}</p>
          </div>
          {overview.northStar && (
            <div className="ae-om-target">
              <div className="ae-om-target-label">Target outcome</div>
              <div className="ae-om-target-value">{overview.northStar}</div>
            </div>
          )}
        </div>

        {/* 12-column Grid */}
        <div className="ae-om-grid" id="operating-model">
          {/* Left Rail — Human Roles */}
          <div className="ae-om-rail">
            <div className="ae-om-label">Human roles</div>
            <div className="ae-om-chips">
              {humanRoles.map((r) => (
                <div
                  key={r.title}
                  className="ae-om-chip ae-om-chip--interactive"
                  onMouseEnter={(e) => { hoveredRef.current = e.currentTarget; setHoveredRole({ ...r, side: 'right' }); }}
                  onMouseLeave={() => setHoveredRole(null)}
                >
                  {r.title}
                </div>
              ))}
            </div>
          </div>

          {/* Center Content */}
          <div className="ae-om-center">
            {/* Pillars Strip */}
            <div className="ae-om-card">
              <div className="ae-om-card-header">
                <div className="ae-om-label" style={{ marginBottom: 0 }}>Cross-cutting pillars</div>
              </div>
              <div className="ae-om-pillars-grid">
                {overview.pillars.map((p) => {
                  const pillarObj = findPillar(p);
                  const isActive = hoveredPillar === p;
                  return (
                    <div
                      key={p}
                      className={`ae-om-pillar-cell ae-om-pillar-cell--link${isActive ? ' ae-om-pillar-cell--active' : ''}`}
                      onClick={() => pillarObj && navigate(`/agentic/pillar/${pillarObj.slug}`)}
                      onMouseEnter={(e) => { hoveredRef.current = e.currentTarget; setHoveredPillar(p); }}
                      onMouseLeave={() => setHoveredPillar(null)}
                    >
                      {p}
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Lifecycle */}
            <div className="ae-om-card">
              <div className="ae-om-card-header ae-om-card-header--between">
                <div>
                  <div className="ae-om-label" style={{ marginBottom: 0 }}>Lifecycle</div>
                  <div className="ae-om-card-subtitle">The delivery spine for enterprise modernization</div>
                </div>
                <div className="ae-om-badge">Human accountability · agent acceleration</div>
              </div>
              <div className="ae-om-lifecycle-grid">
                {phases.map((phase, i) => {
                  const isHighlighted = highlightedPhaseSlugs.includes(phase.slug);
                  return (
                    <div
                      key={phase.slug}
                      className={`ae-om-phase ae-om-phase--link${isHighlighted ? ' ae-om-phase--highlight' : ''}`}
                      onClick={() => navigate(`/agentic/lifecycle/${phase.slug}`)}
                      onMouseEnter={(e) => { hoveredRef.current = e.currentTarget; setHoveredPhase(phase.slug); }}
                      onMouseLeave={() => setHoveredPhase(null)}
                    >
                      <div className="ae-om-phase-header">
                        <div className="ae-om-phase-num">{i + 1}</div>
                        <h3 className="ae-om-phase-title">{phase.title}</h3>
                      </div>
                      <div className="ae-om-phase-items">
                        {(phase.majorActivities ?? []).slice(0, 4).map((item, idx) => (
                          <div key={idx} className="ae-om-phase-item">
                            <span className="ae-om-dot" />
                            <span>{item}</span>
                          </div>
                        ))}
                      </div>
                      <div className="ae-om-phase-cta">View details &rarr;</div>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Enterprise Foundation */}
            <div className="ae-om-card ae-om-foundation-card">
              <div className="ae-om-label">Enterprise foundation</div>
              <div className="ae-om-foundation-grid">
                {foundation.map((item) => (
                  <div key={item} className="ae-om-foundation-chip">{item}</div>
                ))}
              </div>
            </div>
          </div>

          {/* Right Rail — Agent Roles */}
          <div className="ae-om-rail ae-om-rail--agent">
            <div className="ae-om-label">Agent roles</div>
            <div className="ae-om-chips">
              {agentRoles.map((r) => (
                <div
                  key={r.title}
                  className="ae-om-chip ae-om-chip--agent ae-om-chip--interactive"
                  onMouseEnter={(e) => { hoveredRef.current = e.currentTarget; setHoveredRole({ ...r, side: 'left' }); }}
                  onMouseLeave={() => setHoveredRole(null)}
                >
                  {r.title}
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Portal-based tooltips (rendered to document.body, bypass all overflow clipping) */}
        {hoveredRole && (
          <FixedTooltip anchorRef={hoveredRef} position={hoveredRole.side}>
            <RoleTooltipContent role={hoveredRole} />
          </FixedTooltip>
        )}
        {hoveredPillar && findPillar(hoveredPillar) && (
          <FixedTooltip anchorRef={hoveredRef} position="above-left">
            <PillarTooltipContent pillar={findPillar(hoveredPillar)} />
          </FixedTooltip>
        )}
        {hoveredPhase && (() => {
          const p = phases.find(ph => ph.slug === hoveredPhase);
          return p ? (
            <FixedTooltip anchorRef={hoveredRef} position="above-left">
              <div className="ae-om-tooltip-title">{p.title}</div>
              <div className="ae-om-tooltip-purpose">{p.summary}</div>
            </FixedTooltip>
          ) : null;
        })()}

      </div>

      {/* ===== Drill-Down Sections ===== */}
      <div className="ae-body">
        {/* CTA */}
        {cta?.cards?.length > 0 && (
          <div className="ae-cta-band" id="cta">
            <h3>{cta.title}</h3>
            <p>{cta.summary}</p>
            <div className="ae-cta-cards">
              {cta.cards.map((card, idx) => {
                const cls = `ae-cta-card${card.featured ? ' ae-cta-card--featured' : ''}`;
                if (card.link) {
                  return (
                    <div key={idx} className={cls} onClick={() => navigate(card.link)} style={{ cursor: 'pointer' }}>
                      <h4>{card.title}</h4>
                      <p className="ae-audience">{card.audience}</p>
                      <p>{card.description}</p>
                      <p className="ae-cta-label">{card.ctaLabel} &rarr;</p>
                    </div>
                  );
                }
                return (
                  <div key={idx} className={cls}>
                    <h4>{card.title}</h4>
                    <p className="ae-audience">{card.audience}</p>
                    <p>{card.description}</p>
                    <p className="ae-cta-label">{card.ctaLabel} &rarr;</p>
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

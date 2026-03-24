import { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { getAgenticVirtualArchitect } from '../data/agenticRepository.js';

function Tooltip({ anchorRef, children }) {
  const [style, setStyle] = useState(null);
  useEffect(() => {
    if (!anchorRef?.current) return;
    const r = anchorRef.current.getBoundingClientRect();
    setStyle({
      position: 'fixed',
      zIndex: 9999,
      left: r.left + r.width / 2,
      top: r.bottom + 8,
      transform: 'translateX(-50%)',
    });
  }, [anchorRef]);
  if (!style) return null;
  return createPortal(
    <div className="ae-va-tooltip" style={style}>{children}</div>,
    document.body
  );
}

function StepCard({ step, isActive, onHover, onLeave, anchorRef }) {
  return (
    <div
      className={`ae-va-step${isActive ? ' ae-va-step--active' : ''}`}
      onMouseEnter={onHover}
      onMouseLeave={onLeave}
      ref={anchorRef}
    >
      <div className="ae-va-step-num">{step.step}</div>
      <div className="ae-va-step-title">{step.title}</div>
      <div className="ae-va-step-summary">{step.summary}</div>
    </div>
  );
}

export default function AgenticVirtualArchitect() {
  const data = getAgenticVirtualArchitect();
  const [hoveredStep, setHoveredStep] = useState(null);
  const stepRefs = useRef({});

  const getRef = (idx) => {
    if (!stepRefs.current[idx]) stepRefs.current[idx] = { current: null };
    return stepRefs.current[idx];
  };

  return (
    <section className="ae-dp ae-va">
      {/* Breadcrumb */}
      <nav className="ae-dp-breadcrumb">
        <Link to="/agentic">Operating Model</Link>
        <span>/</span>
        <span>{data.title}</span>
      </nav>

      {/* Hero */}
      <div className="ae-va-hero">
        <div className="ae-dp-eyebrow">Flagship Product</div>
        <h1 className="ae-dp-title">{data.title}</h1>
        <p className="ae-va-tagline">{data.tagline}</p>
        <p className="ae-va-vision">{data.vision}</p>
      </div>

      {/* Problem framing */}
      <div className="ae-dp-strategy">
        <div className="ae-dp-strategy-label">The problem</div>
        <p className="ae-dp-strategy-text">{data.problem}</p>
      </div>

      {/* 5-Stage Workflow */}
      <div className="ae-dp-card">
        <div className="ae-dp-card-label">360° Workflow</div>
        <div className="ae-va-workflow">
          {data.workflow.map((step, idx) => (
            <StepCard
              key={step.step}
              step={step}
              isActive={hoveredStep === idx}
              anchorRef={(el) => { getRef(idx).current = el; }}
              onHover={() => setHoveredStep(idx)}
              onLeave={() => setHoveredStep(null)}
            />
          ))}
        </div>
        {hoveredStep !== null && data.workflow[hoveredStep] && (
          <Tooltip anchorRef={getRef(hoveredStep)}>
            <div className="ae-va-tooltip-title">{data.workflow[hoveredStep].title}</div>
            <ul className="ae-va-tooltip-list">
              {data.workflow[hoveredStep].details.map((d, i) => (
                <li key={i}>{d}</li>
              ))}
            </ul>
          </Tooltip>
        )}
      </div>

      {/* Outputs */}
      <div className="ae-dp-card">
        <div className="ae-dp-card-label">What you get</div>
        <div className="ae-va-outputs">
          {data.outputs.map((group) => (
            <div key={group.category} className="ae-va-output-group">
              <div className="ae-va-output-category">{group.category}</div>
              <ul className="ae-dp-card-list">
                {group.items.map((item, i) => <li key={i}>{item}</li>)}
              </ul>
            </div>
          ))}
        </div>
      </div>

      {/* Audiences */}
      <div className="ae-dp-card">
        <div className="ae-dp-card-label">Who it serves</div>
        <div className="ae-va-audiences">
          {data.audiences.map((a) => (
            <div key={a.role} className="ae-va-audience">
              <div className="ae-va-audience-role">{a.role}</div>
              <div className="ae-va-audience-value">{a.value}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Differentiators */}
      <div className="ae-dp-card">
        <div className="ae-dp-card-label">What sets it apart</div>
        <ul className="ae-dp-card-list ae-dp-card-list--numbered">
          {data.differentiators.map((d, i) => <li key={i}>{d}</li>)}
        </ul>
      </div>

      {/* Azure Services Coverage */}
      <div className="ae-dp-card">
        <div className="ae-dp-card-label">Azure services coverage</div>
        <div className="ae-dp-chip-grid">
          {data.azureServices.map((svc, i) => (
            <span key={i} className="ae-dp-chip ae-dp-chip--accent">{svc}</span>
          ))}
        </div>
      </div>

      {/* Back */}
      <Link to="/agentic" className="ae-dp-back">← Back to Operating Model</Link>
    </section>
  );
}

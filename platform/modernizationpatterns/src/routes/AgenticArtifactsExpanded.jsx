import { Link } from 'react-router-dom';
import { getAgenticArtifactsExpanded, getAgenticLifecycleBySlug } from '../data/agenticRepository.js';

export default function AgenticArtifactsExpanded() {
  const artifacts = getAgenticArtifactsExpanded();

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/agentic">Agentic Engineering</Link>
        <span>/</span>
        <span>Artifacts</span>
      </div>

      <h2>Artifacts</h2>
      <p className="index-summary">Key artifacts produced and consumed across the agentic engineering lifecycle.</p>

      {artifacts.map((artifact) => (
        <div key={artifact.slug} className="content-section">
          <h3>{artifact.title}</h3>
          <p>{artifact.summary}</p>

          {artifact.producedIn?.length > 0 && (
            <>
              <h4>Produced In</h4>
              <ul>
                {artifact.producedIn.map((slug) => {
                  const phase = getAgenticLifecycleBySlug(slug);
                  return phase ? (
                    <li key={slug}><Link to={`/agentic/lifecycle/${slug}`}>{phase.title}</Link></li>
                  ) : (
                    <li key={slug}>{slug}</li>
                  );
                })}
              </ul>
            </>
          )}

          {artifact.usedBy?.length > 0 && (
            <>
              <h4>Used By</h4>
              <ul>
                {artifact.usedBy.map((role, idx) => (
                  <li key={idx}>{role}</li>
                ))}
              </ul>
            </>
          )}
        </div>
      ))}
    </section>
  );
}

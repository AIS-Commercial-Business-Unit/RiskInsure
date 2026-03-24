import { Link } from 'react-router-dom';
import { getAgenticUseCases } from '../data/agenticRepository.js';

export default function AgenticUseCases() {
  const data = getAgenticUseCases();

  if (!data) {
    return (
      <section className="page">
        <h2>Use Cases</h2>
        <p>Content not found.</p>
      </section>
    );
  }

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/agentic">Agentic Engineering</Link>
        <span>/</span>
        <span>Use Cases</span>
      </div>

      <h2>{data.title}</h2>
      <p className="index-summary">{data.summary}</p>

      {data.items?.map((uc) => (
        <div key={uc.slug} className="content-section">
          <h3>{uc.title}</h3>
          <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.5rem' }}>{uc.industry}</p>

          <h4>Problem</h4>
          <p>{uc.problem}</p>

          <h4>Approach</h4>
          <p>{uc.approach}</p>

          {uc.outcomes?.length > 0 && (
            <>
              <h4>Outcomes</h4>
              <ul>
                {uc.outcomes.map((item, idx) => (
                  <li key={idx}>{item}</li>
                ))}
              </ul>
            </>
          )}
        </div>
      ))}
    </section>
  );
}

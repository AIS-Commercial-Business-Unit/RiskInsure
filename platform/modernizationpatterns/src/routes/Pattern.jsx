import { Link, useParams } from 'react-router-dom';
import { getPatternBySlug, getPatternById, getSources, getSubcategoryInfo } from '../data/patternsRepository.js';
import { categoryMetadata } from '../data/taxonomy.js';

function toTitleCase(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

export default function Pattern() {
  const { slug } = useParams();
  const pattern = getPatternBySlug(slug);

  if (!pattern) {
    return (
      <section className="page">
        <div className="empty-state">
          <h1>Pattern not found</h1>
          <p>The pattern slug does not match any curated file in content/patterns.</p>
          <Link className="primary-button" to="/">
            Back to index
          </Link>
        </div>
      </section>
    );
  }

  const category = categoryMetadata[pattern.category];
  const subcategory = getSubcategoryInfo(pattern.category, pattern.subcategory);
  const sources = getSources();

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/">Index</Link>
        <span>/</span>
        <Link to={`/category/${category.key}`}>{category.title}</Link>
        <span>/</span>
        <span>{subcategory?.title ?? pattern.subcategory}</span>
        <span>/</span>
        <span className={`crumb-chip category-${category.colorKey}`}>{pattern.title}</span>
      </div>

      <div className="pattern-header">
        <div>
          <p className={`category-label category-text-${category.colorKey}`}>{category.title}</p>
          <h1>{pattern.title}</h1>
          <p className="lede">{pattern.summary}</p>
        </div>
        <div className="meta-panel">
          <div>
            <strong>Complexity</strong>
            <p>{toTitleCase(pattern.complexity.level)}</p>
          </div>
          <div>
            <strong>Updated</strong>
            <p>{pattern.lastUpdated}</p>
          </div>
        </div>
      </div>

      <div className="pattern-grid">
        <article className="detail-card">
          <h2>Problem and fit</h2>
          <p>{pattern.decisionGuidance.problemSolved}</p>
          <h3>When to use</h3>
          <ul>
            {pattern.decisionGuidance.whenToUse.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
          <h3>When not to use</h3>
          <ul>
            {pattern.decisionGuidance.whenNotToUse.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </article>

        <article className="detail-card">
          <h2>Enabling technologies</h2>
          <ul>
            {pattern.enablingTechnologies.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </article>

        <article className="detail-card">
          <h2>Things to watch out for</h2>
          <ul>
            {pattern.thingsToWatchOutFor.gotchas.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
          <h3>Opinionated guidance</h3>
          <p>{pattern.thingsToWatchOutFor.opinionatedGuidance}</p>
        </article>

        <article className="detail-card">
          <h2>Complexity assessment</h2>
          <p><strong>Rationale:</strong> {pattern.complexity.rationale}</p>
          <p><strong>Team impact:</strong> {pattern.complexity.teamImpact}</p>
          <p><strong>Skill demand:</strong> {pattern.complexity.skillDemand}</p>
          <p><strong>Operational demand:</strong> {pattern.complexity.operationalDemand}</p>
          <p><strong>Tooling demand:</strong> {pattern.complexity.toolingDemand}</p>
        </article>

        <article className="detail-card">
          <h2>Starter diagram</h2>
          <p>{pattern.starterDiagram.description}</p>
          <div className="diagram">
            {pattern.starterDiagram.nodes.map((node) => (
              <div key={node} className="diagram-node">
                <span>{node}</span>
              </div>
            ))}
          </div>
        </article>

        <article className="detail-card">
          <h2>Real-world example</h2>
          <p><strong>Context:</strong> {pattern.realWorldExample.context}</p>
          <p><strong>Approach:</strong> {pattern.realWorldExample.approach}</p>
          <p><strong>Outcome:</strong> {pattern.realWorldExample.outcome}</p>
        </article>

        <article className="detail-card">
          <h2>Related patterns</h2>
          <div className="related-links">
            {pattern.relatedPatterns.map((relatedId) => {
              const relatedPattern = getPatternById(relatedId);
              if (!relatedPattern) {
                return <span key={relatedId}>{relatedId}</span>;
              }
              return (
                <Link key={relatedId} to={`/pattern/${relatedPattern.slug}`}>
                  {relatedPattern.title}
                </Link>
              );
            })}
          </div>
        </article>

        <article className="detail-card">
          <h2>Further reading</h2>
          <ul>
            {pattern.furtherReading.map((item) => {
              const source = sources.find((entry) => entry.id === item.sourceId);
              return (
                <li key={`${item.title}-${item.sourceId ?? 'none'}`}>
                  <strong>{item.title}</strong>
                  {item.link ? (
                    <span> - <a href={item.link} target="_blank" rel="noreferrer">Source link</a></span>
                  ) : null}
                  {source?.citationRequired || item.citationRequired ? (
                    <span> (Citation required)</span>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </article>
      </div>
    </section>
  );
}

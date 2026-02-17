import { Link, useParams } from 'react-router-dom';
import { getPatternBySlug, getPatternById, getSources } from '../data/patternsRepository.js';
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
  const sources = getSources();

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/">Index</Link>
        <span>/</span>
        <span>{pattern.title}</span>
      </div>

      {/* Two-column layout: 2/3 content + 1/3 metadata */}
      <div className="pattern-detail-layout">
        {/* Left column: Content (2/3) - SINGLE BOX */}
        <div className="pattern-content">
          {/* Title + Category Chip */}
          <div className="pattern-detail-header">
            <span className={`category-chip category-${category.colorKey}`}>
              {category.title}
            </span>
            <h1>{pattern.title}</h1>
          </div>

          {/* Summary */}
          <p className="lede">{pattern.summary}</p>

          {/* Problem Solved */}
          <div className="content-section">
            <h2>Problem Solved</h2>
            <p>{pattern.decisionGuidance.problemSolved}</p>
          </div>

          {/* When to Use */}
          <div className="content-section">
            <h2>When to Use</h2>
            <ul>
              {pattern.decisionGuidance.whenToUse.map((item, idx) => (
                <li key={idx}>{item}</li>
              ))}
            </ul>
          </div>

          {/* When Not to Use */}
          <div className="content-section">
            <h2>When Not to Use</h2>
            <ul>
              {pattern.decisionGuidance.whenNotToUse.map((item, idx) => (
                <li key={idx}>{item}</li>
              ))}
            </ul>
          </div>

          {/* Opinionated Guidance */}
          <div className="content-section">
            <h2>Opinionated Guidance</h2>
            <p>{pattern.thingsToWatchOutFor.opinionatedGuidance}</p>
          </div>

          {/* Gotchas */}
          <div className="content-section">
            <h2>Things to Watch Out For</h2>
            <ul>
              {pattern.thingsToWatchOutFor.gotchas.map((item, idx) => (
                <li key={idx}>{item}</li>
              ))}
            </ul>
          </div>

          {/* Starter Diagram */}
          {pattern.starterDiagram && (
            <div className="content-section">
              <h2>Starter Diagram</h2>
              <p className="diagram-description">{pattern.starterDiagram.description}</p>
              <div className="diagram">
                {pattern.starterDiagram.nodes.map((node, idx) => (
                  <div key={idx} className="diagram-node">
                    <span>{node}</span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Real-world Example */}
          <div className="content-section">
            <h2>Real-World Example</h2>
            <div className="example-block">
              <p><strong>Context:</strong> {pattern.realWorldExample.context}</p>
              <p><strong>Approach:</strong> {pattern.realWorldExample.approach}</p>
              <p><strong>Outcome:</strong> {pattern.realWorldExample.outcome}</p>
            </div>
          </div>

          {/* Further Reading */}
          {pattern.furtherReading && pattern.furtherReading.length > 0 && (
            <div className="content-section">
              <h2>Further Reading</h2>
              <ul>
                {pattern.furtherReading.map((item, idx) => {
                  const source = sources.find((entry) => entry.id === item.sourceId);
                  return (
                    <li key={idx}>
                      <strong>{item.title}</strong>
                      {item.link && (
                        <span> - <a href={item.link} target="_blank" rel="noreferrer">Source link</a></span>
                      )}
                      {(source?.citationRequired || item.citationRequired) && (
                        <span> (Citation required)</span>
                      )}
                    </li>
                  );
                })}
              </ul>
            </div>
          )}
        </div>

        {/* Right column: Metadata (1/3) - SINGLE BOX */}
        <aside className="pattern-metadata">
          {/* Complexity */}
          <div className="meta-group">
            <h3>Complexity</h3>
            <p className="complexity-level">{toTitleCase(pattern.complexity.level)}</p>
            <p className="metadata-detail">{pattern.complexity.rationale}</p>
          </div>

          {/* Team Impact */}
          <div className="meta-group">
            <h3>Team Impact</h3>
            <p className="metadata-detail">{pattern.complexity.teamImpact}</p>
          </div>

          {/* Skill Demand */}
          <div className="meta-group">
            <h3>Skill Demand</h3>
            <p className="metadata-detail">{pattern.complexity.skillDemand}</p>
          </div>

          {/* Operational Demand */}
          <div className="meta-group">
            <h3>Operational Demand</h3>
            <p className="metadata-detail">{pattern.complexity.operationalDemand}</p>
          </div>

          {/* Tooling Demand */}
          <div className="meta-group">
            <h3>Tooling Demand</h3>
            <p className="metadata-detail">{pattern.complexity.toolingDemand}</p>
          </div>

          {/* Related Patterns */}
          {pattern.relatedPatterns && pattern.relatedPatterns.length > 0 && (
            <div className="meta-group">
              <h3>Related Patterns</h3>
              <div className="related-pattern-chips">
                {pattern.relatedPatterns.map((relatedId) => {
                  const relatedPattern = getPatternById(relatedId);
                  if (!relatedPattern) {
                    return <span key={relatedId} className="related-chip">{relatedId}</span>;
                  }
                  const relatedCategory = categoryMetadata[relatedPattern.category];
                  return (
                    <Link
                      key={relatedId}
                      to={`/pattern/${relatedPattern.slug}`}
                      className={`related-chip category-${relatedCategory.colorKey}`}
                    >
                      {relatedPattern.title}
                    </Link>
                  );
                })}
              </div>
            </div>
          )}

          {/* Last Updated */}
          <div className="meta-group">
            <h3>Last Updated</h3>
            <p className="metadata-detail">{pattern.lastUpdated}</p>
          </div>
        </aside>
      </div>
    </section>
  );
}

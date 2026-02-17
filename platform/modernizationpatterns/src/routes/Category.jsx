import { Link, useParams } from 'react-router-dom';

function Diagram({ nodes }) {
  return (
    <div className="diagram">
      {nodes.map((node, index) => (
        <div key={`${node}-${index}`} className="diagram-node">
          <span>{node}</span>
          {index < nodes.length - 1 && <span className="diagram-arrow">→</span>}
        </div>
      ))}
    </div>
  );
}

export default function Category({ categories }) {
  const { slug } = useParams();
  const category = categories.find((item) => item.slug === slug);

  if (!category) {
    return (
      <section className="page">
        <div className="empty-state">
          <h1>Category not found</h1>
          <p>Pick another pattern family to explore.</p>
          <Link className="primary-button" to="/">
            Back to home
          </Link>
        </div>
      </section>
    );
  }

  const related = categories.filter((item) => category.related.includes(item.slug));

  return (
    <section className="page">
      <div className="category-hero">
        <div>
          <p className="eyebrow">{category.label}</p>
          <h1>{category.title}</h1>
          <p className="lede">{category.tagline}</p>
        </div>
        <div className="category-purpose">
          <h2>Why this category exists</h2>
          <p>{category.purpose}</p>
        </div>
      </div>

      <div className="diagram-card">
        <div>
          <p className="category-label">Signature flow</p>
          <h2>{category.diagramTitle}</h2>
        </div>
        <Diagram nodes={category.diagramNodes} />
      </div>

      <div className="section-header">
        <div>
          <h2>Example patterns</h2>
          <p>High-level starters to confirm the drill-in experience.</p>
        </div>
        <Link to="/" className="secondary-link">
          Browse other categories
        </Link>
      </div>

      <div className="grid">
        {category.examples.map((example) => (
          <div key={example.title} className="example-card">
            <p className="category-label">{example.focus}</p>
            <h3>{example.title}</h3>
            <p>{example.summary}</p>
            <div className="example-meta">Diagram placeholder: {example.diagram}</div>
          </div>
        ))}
      </div>

      <div className="related-panel">
        <div>
          <h2>Related categories</h2>
          <p>Keep moving across the map without losing momentum.</p>
        </div>
        <div className="related-links">
          {related.map((item) => (
            <Link key={item.slug} to={`/patterns/${item.slug}`}>
              {item.title}
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}

import { Link } from 'react-router-dom';

export default function Home({ categories, patterns }) {
  const totalPatterns = patterns.length;

  return (
    <section className="page">
      <div className="index-header">
        <h1>Modernization Patterns</h1>
        <p className="lede">{totalPatterns} curated patterns</p>
      </div>

      <div className="grid">
        {categories.map((category) => (
          <Link key={category.key} to={`/category/${category.key}`} className={`category-card category-${category.colorKey}`}>
            <div>
              <h3>{category.title}</h3>
              <p className="category-summary">{category.summary}</p>
            </div>
            <span className="card-cta">{category.patternCount} patterns</span>
          </Link>
        ))}
      </div>
    </section>
  );
}

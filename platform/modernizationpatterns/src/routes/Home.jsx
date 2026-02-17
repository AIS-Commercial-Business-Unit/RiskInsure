import { Link } from 'react-router-dom';

export default function Home({ categories }) {
  return (
    <section className="page">
      <div className="hero">
        <div className="hero-content">
          <p className="eyebrow">Modernization Patterns</p>
          <h1>Find the pattern, then move with momentum.</h1>
          <p className="lede">
            Start with five pattern families. Each family highlights what to focus on, when to use it, and
            where to drill deeper later.
          </p>
        </div>
        <div className="hero-card">
          <h2>Five lenses for fast decisions</h2>
          <ul>
            <li>Enablement - remove friction and unblock execution</li>
            <li>Technical - core platform and capability choices</li>
            <li>Complexity - steer scope and reduce hidden coupling</li>
            <li>Distributed Architecture - govern how systems communicate</li>
            <li>DevOps - keep delivery flow and the factory floor clean</li>
          </ul>
        </div>
      </div>

      <div className="grid">
        {categories.map((category) => (
          <Link key={category.slug} to={`/patterns/${category.slug}`} className="category-card">
            <div>
              <p className="category-label">{category.label}</p>
              <h3>{category.title}</h3>
              <p>{category.summary}</p>
            </div>
            <span className="card-cta">Explore patterns</span>
          </Link>
        ))}
      </div>

      <section className="reading-strip">
        <div>
          <h2>Rapid scan, deeper dives later</h2>
          <p>
            Each category keeps content lightweight now, with placeholders for diagrams and recommended
            reading you will add later.
          </p>
        </div>
        <div className="reading-cards">
          <div className="reading-card">
            <p className="category-label">Next additions</p>
            <h3>Curated sources</h3>
            <p>Modernization architecture, enterprise integration patterns, and more.</p>
          </div>
          <div className="reading-card">
            <p className="category-label">Interactive layer</p>
            <h3>Future chat assistant</h3>
            <p>Ask questions across the patterns once content is indexed.</p>
          </div>
        </div>
      </section>
    </section>
  );
}

import { Link } from 'react-router-dom';

export default function Home({ categories }) {
  return (
    <section className="page">
      <div className="hero">
        <div className="hero-content">
          <p className="eyebrow">Modernization Patterns</p>
          <h1>Simple map. Complex ideas.</h1>
          <p className="lede">
            Start with five pattern families. Pick one, understand intent, and move to action.
          </p>
        </div>
        <div className="hero-card">
          <h2>Five lenses</h2>
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
          <h2>Clarity over volume</h2>
          <p>Each page stays concise. You can add depth only where it helps decisions.</p>
        </div>
        <div className="reading-cards">
          <div className="reading-card">
            <p className="category-label">Next additions</p>
            <h3>Curated sources</h3>
            <p>Add references when they strengthen understanding.</p>
          </div>
          <div className="reading-card">
            <p className="category-label">Interactive layer</p>
            <h3>Future chat assistant</h3>
            <p>Ask targeted questions across your curated pattern content.</p>
          </div>
        </div>
      </section>
    </section>
  );
}

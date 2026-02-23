import { Link, useParams } from 'react-router-dom';
import { getPatternsForCategory, getSubcategoryInfo } from '../data/patternsRepository.js';
import { categoryMetadata } from '../data/taxonomy.js';

function getComplexityIcon(level) {
  const icons = {
    low: '⚡',
    medium: '⚙️',
    high: '🔥'
  };
  return icons[level] || '•';
}

export default function Category() {
  const { category } = useParams();
  const categoryInfo = categoryMetadata[category];

  if (!categoryInfo) {
    return (
      <section className="page">
        <div className="empty-state">
          <h1>Category not found</h1>
          <p>Pick a valid category from the index page.</p>
          <Link className="primary-button" to="/">
            Back to index
          </Link>
        </div>
      </section>
    );
  }

  const patterns = getPatternsForCategory(category);
  const groupedPatterns = patterns.reduce((accumulator, pattern) => {
    const key = pattern.subcategory;
    if (!accumulator[key]) {
      accumulator[key] = [];
    }
    accumulator[key].push(pattern);
    return accumulator;
  }, {});
  const subcategoryEntries = Object.entries(groupedPatterns);

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/">Index</Link>
        <span>/</span>
        <span className={`crumb-chip category-${categoryInfo.colorKey}`}>{categoryInfo.title}</span>
        <span className="pattern-count">({patterns.length} patterns)</span>
      </div>

      {subcategoryEntries.map(([subcategoryKey, subcategoryPatterns]) => {
        const subcategoryInfo = getSubcategoryInfo(category, subcategoryKey);
        return (
          <div key={subcategoryKey} className="subcategory-block">
            <div className="subcategory-label">{subcategoryInfo?.title ?? subcategoryKey}</div>
            <div className="pattern-list">
              {subcategoryPatterns.map((pattern) => (
                <Link key={pattern.id} to={`/pattern/${pattern.slug}`} className="pattern-row">
                  <span className="complexity-icon" title={`Complexity: ${pattern.complexity.level}`}>
                    {getComplexityIcon(pattern.complexity.level)}
                  </span>
                  <div className="pattern-info">
                    <div className="pattern-title">{pattern.title}</div>
                    <div className="pattern-hint">{pattern.decisionGuidance.whenToUse[0]}</div>
                  </div>
                </Link>
              ))}
            </div>
          </div>
        );
      })}


    </section>
  );
}

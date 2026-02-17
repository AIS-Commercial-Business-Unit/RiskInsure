import { useState } from 'react';
import { Link } from 'react-router-dom';
import { categoryMetadata } from '../data/taxonomy.js';
import { getPatternIcon } from '../data/patternIcons.js';

export default function Home({ categories, patterns }) {
  const [activeFilter, setActiveFilter] = useState('all');

  // Filter patterns based on active category
  const filteredPatterns =
    activeFilter === 'all'
      ? patterns
      : patterns.filter((p) => p.category === activeFilter);

  const filterOptions = [
    { key: 'all', label: 'All', count: patterns.length },
    { key: 'discovery', label: 'Discovery', count: patterns.filter(p => p.category === 'discovery').length },
    { key: 'strategic', label: 'Strategic', count: patterns.filter(p => p.category === 'strategic').length },
    { key: 'technical', label: 'Technical', count: patterns.filter(p => p.category === 'technical').length },
    { key: 'migration', label: 'Migration', count: patterns.filter(p => p.category === 'migration').length },
    { key: 'devops', label: 'DevOps', count: patterns.filter(p => p.category === 'devops').length },
    { key: 'enablement', label: 'Enablement', count: patterns.filter(p => p.category === 'enablement').length }
  ];

  return (
    <section className="page pattern-index">
      <div className="index-intro">
        <p className="index-summary">
          Curated patterns for mainframe, middleware, and COTS modernization. 
          Domain-driven design, cloud-native architecture, and pragmatic migration strategies.
        </p>
      </div>

      <div className="filter-bar">
        {filterOptions.map((option) => (
          <button
            key={option.key}
            className={`filter-button ${activeFilter === option.key ? 'active' : ''} ${
              option.key !== 'all' ? `filter-${option.key}` : ''
            }`}
            onClick={() => setActiveFilter(option.key)}
          >
            {option.label}
            <span className="filter-count">{option.count}</span>
          </button>
        ))}
      </div>

      <div className="pattern-card-grid">
        {filteredPatterns.map((pattern) => {
          const Icon = getPatternIcon(pattern);
          const category = categoryMetadata[pattern.category];

          return (
            <Link
              key={pattern.id}
              to={`/pattern/${pattern.slug}`}
              className="pattern-card"
            >
              <div className="pattern-card-icon">
                <Icon size={18} strokeWidth={1.5} />
              </div>
              <div className="pattern-card-content">
                <div className="pattern-card-header">
                  <h3 className="pattern-card-title">{pattern.title}</h3>
                  <span className={`category-chip category-${category.colorKey}`}>
                    {category.title}
                  </span>
                </div>
                <p className="pattern-card-summary">{pattern.summary}</p>
              </div>
            </Link>
          );
        })}
      </div>

      {filteredPatterns.length === 0 && (
        <div className="empty-state">
          <p>No patterns found in this category.</p>
        </div>
      )}
    </section>
  );
}

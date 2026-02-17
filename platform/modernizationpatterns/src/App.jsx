import { BrowserRouter, Link, Route, Routes } from 'react-router-dom';
import Home from './routes/Home.jsx';
import Category from './routes/Category.jsx';
import { categories } from './data/patterns.js';

const navItems = categories.map((category) => ({
  slug: category.slug,
  title: category.title
}));

export default function App() {
  return (
    <BrowserRouter>
      <div className="app-shell">
        <header className="site-header">
          <div className="brand">
            <Link to="/" className="brand-mark">
              Modernization Patterns Atlas
            </Link>
            <p className="brand-tagline">
              Clear patterns for complex systems.
            </p>
          </div>
          <nav className="top-nav">
            {navItems.map((item) => (
              <Link key={item.slug} to={`/patterns/${item.slug}`}>
                {item.title}
              </Link>
            ))}
          </nav>
        </header>

        <main>
          <Routes>
            <Route path="/" element={<Home categories={categories} />} />
            <Route path="/patterns/:slug" element={<Category categories={categories} />} />
          </Routes>
        </main>

        <footer className="site-footer">
          <p>Built for clarity, intent, and practical decisions.</p>
        </footer>
      </div>
    </BrowserRouter>
  );
}

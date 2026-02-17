import { BrowserRouter, Route, Routes } from 'react-router-dom';
import Home from './routes/Home.jsx';
import Category from './routes/Category.jsx';
import Pattern from './routes/Pattern.jsx';
import { getCategories, getPatterns } from './data/patternsRepository.js';

export default function App() {
  const categories = getCategories();
  const patterns = getPatterns();

  return (
    <BrowserRouter>
      <div className="app-shell">
        <header className="simple-header">
          <h1>Modernization Patterns</h1>
        </header>

        <main>
          <Routes>
            <Route path="/" element={<Home categories={categories} patterns={patterns} />} />
            <Route path="/category/:category" element={<Category />} />
            <Route path="/pattern/:slug" element={<Pattern />} />
          </Routes>
        </main>

        <footer className="site-footer">
          <p>Built for clarity, intent, and practical decisions.</p>
        </footer>
      </div>
    </BrowserRouter>
  );
}

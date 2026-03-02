import { BrowserRouter, Route, Routes, Link, useLocation } from 'react-router-dom';
import Home from './routes/Home.jsx';
import Pattern from './routes/Pattern.jsx';
import Evaluation from './routes/Evaluation.jsx';
import { ChatWidget } from './components/ChatWidget/ChatWidget.jsx';
import './components/ChatWidget/ChatWidget.css';
import { getCategories, getPatterns } from './data/patternsRepository.js';

function Navigation() {
  const location = useLocation();

  return (
    <nav className="main-nav">
      <Link
        to="/"
        className={`nav-link ${location.pathname === '/' ? 'active' : ''}`}
      >
        Patterns
      </Link>
      <Link
        to="/evaluation"
        className={`nav-link ${location.pathname === '/evaluation' ? 'active' : ''}`}
      >
        Platform Evaluation
      </Link>
    </nav>
  );
}

export default function App() {
  const categories = getCategories();
  const patterns = getPatterns();

  return (
    <BrowserRouter>
      <div className="app-shell">
        <header className="simple-header">
          <h1>Modernization Patterns</h1>
          <Navigation />
        </header>

        <main>
          <Routes>
            <Route path="/" element={<Home categories={categories} patterns={patterns} />} />
            <Route path="/pattern/:slug" element={<Pattern />} />
            <Route path="/evaluation" element={<Evaluation />} />
          </Routes>
        </main>

        <footer className="site-footer">
          <p>Built for clarity, intent, and practical decisions.</p>
        </footer>

        <ChatWidget />
      </div>
    </BrowserRouter>
  );
}

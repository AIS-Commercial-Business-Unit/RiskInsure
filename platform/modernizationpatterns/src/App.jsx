import { BrowserRouter, Route, Routes, Link, useLocation } from 'react-router-dom';
import { useEffect } from 'react';
import Home from './routes/Home.jsx';
import Pattern from './routes/Pattern.jsx';
import Evaluation from './routes/Evaluation.jsx';
import AgenticEngineering from './routes/AgenticEngineering.jsx';
import AgenticPillar from './routes/AgenticPillar.jsx';
import AgenticLifecycle from './routes/AgenticLifecycle.jsx';
import AgenticOfferings from './routes/AgenticOfferings.jsx';
import AgenticRoles from './routes/AgenticRoles.jsx';
import AgenticArtifactsExpanded from './routes/AgenticArtifactsExpanded.jsx';
import AgenticHome from './routes/AgenticHome.jsx';
import AgenticUseCases from './routes/AgenticUseCases.jsx';
import AgenticVirtualArchitect from './routes/AgenticVirtualArchitect.jsx';
import { ChatWidget } from './components/ChatWidget/ChatWidget.jsx';
import './components/ChatWidget/ChatWidget.css';
import { getCategories, getPatterns } from './data/patternsRepository.js';

function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => { window.scrollTo(0, 0); }, [pathname]);
  return null;
}

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
      <Link
        to="/agentic"
        className={`nav-link ${location.pathname.startsWith('/agentic') ? 'active' : ''}`}
      >
        Agentic Engineering
      </Link>
    </nav>
  );
}

function AppContent() {
  const location = useLocation();
  const categories = getCategories();
  const patterns = getPatterns();
  const isAgentic = location.pathname.startsWith('/agentic');

  return (
    <div className="app-shell">
      {!isAgentic && (
        <header className="simple-header">
          <h1>Modernization Patterns</h1>
          <Navigation />
        </header>
      )}

      <main>
        <Routes>
          <Route path="/" element={<Home categories={categories} patterns={patterns} />} />
          <Route path="/pattern/:slug" element={<Pattern />} />
          <Route path="/evaluation" element={<Evaluation />} />
          <Route path="/agentic-home" element={<AgenticHome />} />
          <Route path="/agentic" element={<AgenticEngineering />} />
          <Route path="/agentic/pillar/:slug" element={<AgenticPillar />} />
          <Route path="/agentic/lifecycle/:slug" element={<AgenticLifecycle />} />
          <Route path="/agentic/offerings" element={<AgenticOfferings />} />
          <Route path="/agentic/roles" element={<AgenticRoles />} />
          <Route path="/agentic/artifacts" element={<AgenticArtifactsExpanded />} />
          <Route path="/agentic/use-cases" element={<AgenticUseCases />} />
          <Route path="/agentic/virtual-architect" element={<AgenticVirtualArchitect />} />
        </Routes>
      </main>

      {!isAgentic && (
        <footer className="site-footer">
          <p>Built for clarity, intent, and practical decisions.</p>
        </footer>
      )}

      <ChatWidget />
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <ScrollToTop />
      <AppContent />
    </BrowserRouter>
  );
}

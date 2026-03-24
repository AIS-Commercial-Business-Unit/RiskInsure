import { Link } from 'react-router-dom';
import { getAgenticRoles } from '../data/agenticRepository.js';

function RoleCard({ role }) {
  return (
    <div className="content-section">
      <h4>{role.title}</h4>
      <p>{role.purpose}</p>
      {role.responsibilities?.length > 0 && (
        <ul>
          {role.responsibilities.map((item, idx) => (
            <li key={idx}>{item}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default function AgenticRoles() {
  const roles = getAgenticRoles();

  return (
    <section className="page">
      <div className="breadcrumb">
        <Link to="/agentic">Agentic Engineering</Link>
        <span>/</span>
        <span>Roles</span>
      </div>

      <h2>Roles</h2>
      <p className="index-summary">Human and agent roles that operate across the agentic engineering model.</p>

      {roles.humanRoles?.length > 0 && (
        <>
          <h3>Human Roles</h3>
          {roles.humanRoles.map((role) => (
            <RoleCard key={role.title} role={role} />
          ))}
        </>
      )}

      {roles.agentRoles?.length > 0 && (
        <>
          <h3>Agent Roles</h3>
          {roles.agentRoles.map((role) => (
            <RoleCard key={role.title} role={role} />
          ))}
        </>
      )}
    </section>
  );
}

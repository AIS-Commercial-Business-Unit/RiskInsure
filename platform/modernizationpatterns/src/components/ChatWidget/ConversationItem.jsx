import { Trash2 } from 'lucide-react';

export function ConversationItem({
  id,
  preview,
  timestamp,
  isActive,
  onClick,
  onDelete,
}) {
  const formatTime = (date) => {
    const now = new Date();
    const messageDate = new Date(date);
    const diffMs = now - messageDate;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 60) return `${diffMins}m`;
    if (diffHours < 24) return `${diffHours}h`;
    if (diffDays < 7) return `${diffDays}d`;
    return messageDate.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
    });
  };

  const handleDelete = (e) => {
    e.stopPropagation();
    if (onDelete) {
      onDelete(id);
    }
  };

  return (
    <div className="conversation-item-wrapper">
      <button
        onClick={onClick}
        className={`conversation-item ${isActive ? 'active' : ''}`}
        title={preview}
      >
        <div className="conversation-item-content">
          <div className="conversation-item-preview">{preview}</div>
          <div className="conversation-item-time">{formatTime(timestamp)}</div>
        </div>
        {isActive && <span className="conversation-item-indicator">❯</span>}
      </button>
      <button
        onClick={handleDelete}
        className="conversation-item-delete"
        title="Delete conversation"
        aria-label="Delete"
      >
        <Trash2 size={14} />
      </button>
    </div>
  );
}

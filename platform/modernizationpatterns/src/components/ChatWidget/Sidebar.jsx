import { Plus, Sun, Moon } from 'lucide-react';
import { ConversationItem } from './ConversationItem';

export function Sidebar({
  conversations,
  currentConversationId,
  onNewChat,
  onSelectConversation,
  onDeleteConversation,
  theme,
  onToggleTheme,
  onClose,
}) {
  // Group conversations by date
  const groupedConversations = conversations.reduce((groups, conv) => {
    const date = new Date(conv.createdAt);
    const now = new Date();
    const diffDays = Math.floor((now - date) / 86400000);

    let group = 'Today';
    if (diffDays === 1) group = 'Yesterday';
    else if (diffDays > 1 && diffDays <= 7) group = 'Last Week';
    else if (diffDays > 7 && diffDays <= 30) group = 'Last Month';
    else group = 'Earlier';

    if (!groups[group]) groups[group] = [];
    groups[group].push(conv);
    return groups;
  }, {});

  const groupOrder = ['Today', 'Yesterday', 'Last Week', 'Last Month', 'Earlier'];

  return (
    <div className="chat-sidebar">
      <div className="sidebar-header">
        <button
          onClick={onNewChat}
          className="sidebar-new-chat-btn"
          title="Start a new conversation"
        >
          <Plus size={18} />
          <span>New Chat</span>
        </button>
        <button
          onClick={onClose}
          className="sidebar-close-btn"
          title="Close sidebar"
        >
          ✕
        </button>
      </div>

      <div className="sidebar-content">
        {conversations.length === 0 ? (
          <div className="sidebar-empty">
            <p>No conversations yet</p>
            <p className="sidebar-empty-hint">Start a new chat to begin</p>
          </div>
        ) : (
          <div className="sidebar-conversations">
            {groupOrder.map((group) => {
              const groupConvs = groupedConversations[group];
              if (!groupConvs || groupConvs.length === 0) return null;

              return (
                <div key={group} className="conversation-group">
                  <div className="conversation-group-label">{group}</div>
                  <div className="conversation-group-items">
                    {groupConvs.map((conv) => (
                      <ConversationItem
                        key={conv.id}
                        id={conv.id}
                        preview={
                          conv.messages?.[0]?.message?.substring(0, 40) ||
                          'New conversation'
                        }
                        timestamp={conv.createdAt}
                        isActive={conv.id === currentConversationId}
                        onClick={() => onSelectConversation(conv.id)}
                        onDelete={onDeleteConversation}
                      />
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div className="sidebar-footer">
        <button
          onClick={onToggleTheme}
          className="sidebar-theme-btn"
          title={theme === 'light' ? 'Switch to dark mode' : 'Switch to light mode'}
        >
          {theme === 'light' ? (
            <>
              <Moon size={16} />
              <span>Dark</span>
            </>
          ) : (
            <>
              <Sun size={16} />
              <span>Light</span>
            </>
          )}
        </button>
      </div>
    </div>
  );
}

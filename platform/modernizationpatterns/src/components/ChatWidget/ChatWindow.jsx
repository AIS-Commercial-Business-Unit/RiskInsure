import { X, Maximize2, Minimize2, Menu } from 'lucide-react';
import { MessageList } from './MessageList';
import { InputBox } from './InputBox';

export function ChatWindow({
  isOpen,
  isExpanded,
  isSidebarOpen,
  showMenuButton,
  onClose,
  onExpand,
  onToggleSidebar,
  messages,
  inputValue,
  setInputValue,
  onSend,
  isLoading,
  messageListRef,
}) {
  if (!isOpen) return null;

  const windowClasses = [
    'chat-window',
    isExpanded ? 'expanded' : '',
    isExpanded && isSidebarOpen ? 'sidebar-open' : '',
  ].filter(Boolean).join(' ');

  return (
    <div className={windowClasses}>
      <div className="chat-header">
        {showMenuButton && (
          <button
            onClick={onToggleSidebar}
            className="chat-menu-btn"
            title="Toggle conversation history"
            aria-label="Menu"
          >
            <Menu size={20} />
          </button>
        )}
        <div className="chat-header-title">
          <span className="chat-icon">📘</span>
          <div>
            <h3>Modernization Patterns Chat</h3>
            <p>Ask about architecture & migration patterns</p>
          </div>
        </div>
        <div className="chat-header-buttons">
          <button
            onClick={onExpand}
            className="chat-expand-btn"
            title={isExpanded ? 'Exit fullscreen' : 'Fullscreen'}
            aria-label={isExpanded ? 'Exit fullscreen' : 'Fullscreen'}
          >
            {isExpanded ? <Minimize2 size={20} /> : <Maximize2 size={20} />}
          </button>
          <button
            onClick={onClose}
            className="chat-close-btn"
            title="Close chat (Esc)"
            aria-label="Close"
          >
            <X size={20} />
          </button>
        </div>
      </div>

      <MessageList
        messages={messages}
        isLoading={isLoading}
        messageListRef={messageListRef}
      />

      <InputBox
        inputValue={inputValue}
        setInputValue={setInputValue}
        onSend={onSend}
        isLoading={isLoading}
      />
    </div>
  );
}

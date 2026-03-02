import { X } from 'lucide-react';
import { MessageList } from './MessageList';
import { InputBox } from './InputBox';

export function ChatWindow({
  isOpen,
  onClose,
  messages,
  inputValue,
  setInputValue,
  onSend,
  onClear,
  isLoading,
  messageListRef,
}) {
  if (!isOpen) return null;

  return (
    <div className="chat-window">
      <div className="chat-header">
        <div className="chat-header-title">
          <span className="chat-icon">📘</span>
          <div>
            <h3>Modernization Patterns Chat</h3>
            <p>Ask about architecture & migration patterns</p>
          </div>
        </div>
        <button
          onClick={onClose}
          className="chat-close-btn"
          title="Close chat (Esc)"
          aria-label="Close"
        >
          <X size={20} />
        </button>
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
        onClear={onClear}
        isLoading={isLoading}
      />
    </div>
  );
}

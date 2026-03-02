import { Send, Trash2 } from 'lucide-react';

export function InputBox({
  inputValue,
  setInputValue,
  onSend,
  onClear,
  isLoading,
}) {
  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey && !isLoading) {
      e.preventDefault();
      onSend();
    }
  };

  const handleSend = () => {
    if (inputValue.trim() && !isLoading) {
      onSend();
    }
  };

  return (
    <div className="chat-input-section">
      <textarea
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Ask about patterns, CQRS, saga, events..."
        className="chat-input-field"
        rows="2"
        disabled={isLoading}
      />
      <div className="chat-input-actions">
        <button
          onClick={handleSend}
          disabled={!inputValue.trim() || isLoading}
          className="chat-btn chat-btn-send"
          title="Send message (Shift+Enter)"
        >
          <Send size={18} />
        </button>
        <button
          onClick={onClear}
          className="chat-btn chat-btn-clear"
          title="Clear chat history"
        >
          <Trash2 size={18} />
        </button>
      </div>
    </div>
  );
}

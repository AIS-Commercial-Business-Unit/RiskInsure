import { Send } from 'lucide-react';

export function InputBox({
  inputValue,
  setInputValue,
  onSend,
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
      <div className="chat-input-wrapper">
        <textarea
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about patterns, CQRS, saga, events..."
          className="chat-input-field"
          rows="2"
          disabled={isLoading}
        />
        <button
          onClick={handleSend}
          disabled={!inputValue.trim() || isLoading}
          className="chat-btn chat-btn-send-inline"
          title="Send (Enter)"
        >
          <Send size={28} />
        </button>
      </div>
    </div>
  );
}

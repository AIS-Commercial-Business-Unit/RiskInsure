import { useEffect } from 'react';

export function MessageList({ messages, isLoading, messageListRef }) {
  return (
    <div className="chat-messages" ref={messageListRef}>
      {messages.length === 0 ? (
        <div className="chat-empty-state">
          <p>👋 Welcome! Ask me anything about modernization patterns.</p>
          <p className="chat-hint">e.g., "What is CQRS?", "When to use saga pattern?"</p>
        </div>
      ) : (
        messages.map((msg, idx) => {
          const isStreaming = msg.isStreaming && idx === messages.length - 1;
          return (
            <div key={idx} className={`chat-message chat-message-${msg.role}`}>
              <div className={`chat-message-content ${isStreaming ? 'chat-streaming' : ''}`}>
                {msg.role === 'user' ? '👤' : '🤖'} {msg.content}
                {isStreaming && <span className="chat-cursor">▌</span>}
              </div>
              {msg.citations && msg.citations.length > 0 && (
                <div className="chat-citations">
                  <div className="chat-citations-separator"></div>
                  <div className="chat-citations-label">Sources:</div>
                  {msg.citations.map((citation, cidx) => (
                    <div key={cidx} className="chat-citation-item">
                      📌 {citation}
                    </div>
                  ))}
                </div>
              )}
              <div className="chat-message-time">
                {new Date(msg.timestamp).toLocaleTimeString([], {
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </div>
            </div>
          );
        })
      )}

      {isLoading && messages.length > 0 && messages[messages.length - 1]?.role === 'user' && (
        <div className="chat-message chat-message-assistant">
          <div className="chat-message-content">
            <span className="chat-typing">🤖 Thinking</span>
            <span className="chat-cursor">▌</span>
          </div>
        </div>
      )}
    </div>
  );
}

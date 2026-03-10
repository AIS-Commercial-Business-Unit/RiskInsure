import { useEffect } from 'react';

// Parse markdown-like formatting from API responses
function parseMessageContent(content) {
  if (!content) return '';

  // Split into paragraphs
  const paragraphs = content.split('\n\n');

  return paragraphs
    .map((paragraph, pIdx) => {
      // Check if paragraph is a list (lines starting with -)
      const lines = paragraph.split('\n');
      const isListParagraph = lines.some(line => line.trim().startsWith('-'));

      if (isListParagraph) {
        // Handle list items
        const items = lines
          .filter(line => line.trim().startsWith('-'))
          .map(line => {
            const text = line.trim().substring(1).trim();
            // Bold text: **text** → <strong>text</strong>
            const formatted = text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
            return `<li>${formatted}</li>`;
          });
        return items.length > 0 ? `<ul>${items.join('')}</ul>` : '';
      } else {
        // Regular paragraph - parse bold formatting
        const formatted = paragraph.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        return `<p>${formatted}</p>`;
      }
    })
    .filter(p => p.length > 0)
    .join('');
}

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
                {msg.role === 'user' ? '👤' : '🤖'}{' '}
                <div
                  dangerouslySetInnerHTML={{
                    __html: parseMessageContent(msg.content)
                  }}
                  style={{ display: 'inline-block', textAlign: 'left' }}
                />
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
            <span className="chat-typing">🤖 Loading</span>
            <span className="chat-cursor">▌</span>
          </div>
        </div>
      )}
    </div>
  );
}

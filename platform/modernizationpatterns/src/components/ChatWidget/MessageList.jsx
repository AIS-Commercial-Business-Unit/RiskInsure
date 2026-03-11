import { useEffect } from 'react';

// Parse markdown-like formatting from API responses
function parseMessageContent(content) {
  if (!content) return '';

  // Normalize line endings and split into lines
  const lines = content.replace(/\r\n/g, '\n').split('\n');
  const result = [];
  let currentList = [];
  let listType = null; // 'ul' for bullets, 'ol' for numbered

  const flushList = () => {
    if (currentList.length > 0) {
      const tag = listType === 'ol' ? 'ol' : 'ul';
      result.push(`<${tag}>${currentList.join('')}</${tag}>`);
      currentList = [];
      listType = null;
    }
  };

  const formatText = (text) => {
    // Bold: **text** → <strong>text</strong>
    return text.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  };

  for (const line of lines) {
    const trimmed = line.trim();

    if (!trimmed) {
      // Empty line - flush any pending list
      flushList();
      continue;
    }

    // Check for bullet list item: - text
    const bulletMatch = trimmed.match(/^-\s+(.+)$/);
    if (bulletMatch) {
      if (listType !== 'ul') {
        flushList();
        listType = 'ul';
      }
      currentList.push(`<li>${formatText(bulletMatch[1])}</li>`);
      continue;
    }

    // Check for numbered list item: 1. text, 2. text, etc.
    const numberedMatch = trimmed.match(/^(\d+)\.\s+(.+)$/);
    if (numberedMatch) {
      if (listType !== 'ol') {
        flushList();
        listType = 'ol';
      }
      currentList.push(`<li>${formatText(numberedMatch[2])}</li>`);
      continue;
    }

    // Regular text - flush list and add as paragraph
    flushList();
    result.push(`<p>${formatText(trimmed)}</p>`);
  }

  // Flush any remaining list
  flushList();

  return result.join('');
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

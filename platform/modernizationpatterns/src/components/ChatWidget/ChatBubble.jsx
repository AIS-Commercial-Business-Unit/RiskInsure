import { MessageCircle } from 'lucide-react';

export function ChatBubble({ onClick }) {
  return (
    <button
      onClick={onClick}
      className="chat-bubble"
      title="Open RiskInsure ChatBot - Ask about modernization patterns"
      aria-label="Open chat"
    >
      <MessageCircle size={24} strokeWidth={1.5} />
    </button>
  );
}

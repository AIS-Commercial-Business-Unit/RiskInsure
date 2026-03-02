import { useEffect } from 'react';
import { useChatWidget } from './useChatWidget';
import { ChatBubble } from './ChatBubble';
import { ChatWindow } from './ChatWindow';

export function ChatWidget() {
  const {
    isOpen,
    conversationId,
    messages,
    inputValue,
    isLoading,
    messageListRef,
    toggleWindow,
    setInputValue,
    sendMessage,
    clearChat,
    setIsOpen,
  } = useChatWidget();

  // Close on Escape key
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        setIsOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, setIsOpen]);

  return (
    <>
      {!isOpen && <ChatBubble onClick={toggleWindow} />}

      {isOpen && (
        <ChatWindow
          isOpen={isOpen}
          onClose={toggleWindow}
          messages={messages}
          inputValue={inputValue}
          setInputValue={setInputValue}
          onSend={sendMessage}
          onClear={clearChat}
          isLoading={isLoading}
          messageListRef={messageListRef}
        />
      )}
    </>
  );
}

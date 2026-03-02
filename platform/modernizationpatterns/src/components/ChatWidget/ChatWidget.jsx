import { useEffect } from 'react';
import { useChatWidget } from './useChatWidget';
import { ChatBubble } from './ChatBubble';
import { ChatWindow } from './ChatWindow';
import { Sidebar } from './Sidebar';

export function ChatWidget() {
  const {
    isOpen,
    isExpanded,
    isSidebarOpen,
    conversationId,
    messages,
    inputValue,
    isLoading,
    messageListRef,
    conversations,
    theme,
    toggleWindow,
    toggleExpand,
    toggleSidebar,
    setInputValue,
    sendMessage,
    deleteConversation,
    createConversation,
    selectConversation,
    toggleTheme,
    setIsOpen,
  } = useChatWidget();

  // Close on Escape key
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        if (isExpanded) {
          toggleExpand(); // Exit fullscreen first
        } else {
          setIsOpen(false); // Then close window
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, isExpanded, setIsOpen, toggleExpand]);

  return (
    <div className={`chat-widget-container theme-${theme}`}>
      {isSidebarOpen && isOpen && (
        <Sidebar
          conversations={conversations}
          currentConversationId={conversationId}
          onNewChat={() => {
            createConversation();
            setIsOpen(true);
          }}
          onSelectConversation={selectConversation}
          onDeleteConversation={deleteConversation}
          theme={theme}
          onToggleTheme={toggleTheme}
          onClose={toggleSidebar}
        />
      )}

      {!isOpen && <ChatBubble onClick={toggleWindow} />}

      {isOpen && (
        <ChatWindow
          isOpen={isOpen}
          isExpanded={isExpanded}
          showMenuButton={!isSidebarOpen}
          onClose={toggleWindow}
          onExpand={toggleExpand}
          onToggleSidebar={toggleSidebar}
          messages={messages}
          inputValue={inputValue}
          setInputValue={setInputValue}
          onSend={sendMessage}
          isLoading={isLoading}
          messageListRef={messageListRef}
        />
      )}
    </div>
  );
}

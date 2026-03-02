import { useState, useCallback, useRef, useEffect } from 'react';

const API_BASE = 'http://localhost:5000/api/chat';

export function useChatWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [conversationId, setConversationId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [userId, setUserId] = useState(null);
  const [conversations, setConversations] = useState([]);
  const [theme, setThemeState] = useState('light');
  const messageListRef = useRef(null);

  // Initialize with user ID and load theme
  useEffect(() => {
    const uid = `user-${Date.now()}`;
    setUserId(uid);

    // Load theme from localStorage
    const savedTheme = localStorage.getItem('chat-theme') || 'light';
    setThemeState(savedTheme);
    document.documentElement.setAttribute('data-theme', savedTheme);
  }, []);

  // Auto-open sidebar when expanding to fullscreen
  useEffect(() => {
    if (isExpanded) {
      setIsSidebarOpen(true);
    }
  }, [isExpanded]);

  // Hide page scrollbar when expanded, show when collapsed
  useEffect(() => {
    if (isExpanded) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [isExpanded]);

  // Auto-scroll to bottom when messages change
  useEffect(() => {
    if (messageListRef.current) {
      messageListRef.current.scrollTop = messageListRef.current.scrollHeight;
    }
  }, [messages]);

  // Create new conversation
  const createConversation = useCallback(async () => {
    if (!userId) return;
    try {
      const response = await fetch(`${API_BASE}/new?userId=${userId}`, {
        method: 'POST',
      });
      const data = await response.json();

      // Add to conversations list
      const newConv = {
        id: data.conversationId,
        createdAt: new Date().toISOString(),
        messages: [],
      };
      setConversations((prev) => [newConv, ...prev]);

      setConversationId(data.conversationId);
      setMessages([]);
      return data.conversationId;
    } catch (error) {
      console.error('Failed to create conversation:', error);
    }
  }, [userId]);

  // Load existing conversation
  const loadConversation = useCallback(async (convId) => {
    if (!userId) return;
    try {
      const response = await fetch(`${API_BASE}/${convId}?userId=${userId}`);
      const data = await response.json();
      setConversationId(data.id);
      setMessages(data.messages || []);
    } catch (error) {
      console.error('Failed to load conversation:', error);
    }
  }, [userId]);

  // Send message with streaming
  const sendMessage = useCallback(async () => {
    if (!inputValue.trim() || !userId) return;

    // Ensure we have a conversation
    const convId = conversationId || (await createConversation());
    if (!convId) return;

    const userInputContent = inputValue; // Save for API call

    // Add user message to state
    const userMessage = {
      role: 'user',
      content: userInputContent,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, userMessage]);
    setInputValue('');
    setIsLoading(true);

    try {
      // Stream assistant response
      const response = await fetch(`${API_BASE}/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: userInputContent,
          conversationId: convId,
          userId: userId,
        }),
      });

      if (!response.ok) throw new Error('Stream failed');

      // Create assistant message placeholder
      const assistantMessage = {
        role: 'assistant',
        content: '',
        timestamp: new Date().toISOString(),
        isStreaming: true,
      };
      setMessages((prev) => [...prev, assistantMessage]);

      // Parse SSE stream - only process token events
      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let currentEventType = '';
      let citations = [];

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');

        // Keep last incomplete line in buffer
        buffer = lines[lines.length - 1];

        for (let i = 0; i < lines.length - 1; i++) {
          const line = lines[i];

          // Parse event type
          if (line.startsWith('event: ')) {
            currentEventType = line.slice(7).trim();
          }
          // Parse data - only process token events
          else if (line.startsWith('data: ')) {
            const data = line.slice(6).trim();

            // Only add token data to message content
            if (currentEventType === 'token' && data) {
              setMessages((prev) => {
                const updated = [...prev];
                const lastMsg = updated[updated.length - 1];

                // Smart spacing: add space if content exists and doesn't end with space
                if (lastMsg.content && !lastMsg.content.endsWith(' ') && !data.startsWith(' ')) {
                  lastMsg.content += ' ';
                }
                lastMsg.content += data;
                return updated;
              });

              // Small delay for smooth typing effect
              await new Promise(resolve => setTimeout(resolve, 5));
            }
            // Extract citations from response_metadata
            else if (currentEventType === 'response_metadata' && data) {
              // Parse citations from format: "Citations: Pattern1; Pattern2"
              const match = data.match(/Citations:\s*(.+)/);
              if (match) {
                // Split, trim, and deduplicate citations
                const allCitations = match[1].split(';').map(c => c.trim()).filter(c => c);
                citations = [...new Set(allCitations)]; // Remove duplicates while preserving order
              }
            }
          }
        }
      }

      // Process remaining buffer
      if (buffer && buffer.startsWith('data: ')) {
        const data = buffer.slice(6).trim();
        if (data && currentEventType === 'token') {
          setMessages((prev) => {
            const updated = [...prev];
            if (updated[updated.length - 1]) {
              if (updated[updated.length - 1].content && !updated[updated.length - 1].content.endsWith(' ') && !data.startsWith(' ')) {
                updated[updated.length - 1].content += ' ';
              }
              updated[updated.length - 1].content += data;
            }
            return updated;
          });
        }
      }

      // Mark as no longer streaming and add citations
      setMessages((prev) => {
        const updated = [...prev];
        if (updated[updated.length - 1]) {
          updated[updated.length - 1].isStreaming = false;
          if (citations.length > 0) {
            updated[updated.length - 1].citations = citations;
          }
        }
        return updated;
      });
    } catch (error) {
      console.error('Failed to send message:', error);
      setMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          content: '❌ Error: Failed to get response. Please try again.',
          timestamp: new Date().toISOString(),
        },
      ]);
    } finally {
      setIsLoading(false);
    }
  }, [inputValue, conversationId, userId, createConversation]);

  // Clear chat
  const clearChat = useCallback(() => {
    setMessages([]);
    setConversationId(null);
    setInputValue('');
  }, []);

  // Toggle window
  const toggleWindow = useCallback(() => {
    setIsOpen((prev) => {
      if (!prev && !conversationId) {
        // Opening for first time, create new conversation
        createConversation();
      }
      return !prev;
    });
  }, [conversationId, createConversation]);

  // Toggle expand
  const toggleExpand = useCallback(() => {
    setIsExpanded((prev) => !prev);
  }, []);

  // Select conversation
  const selectConversation = useCallback((convId) => {
    setConversationId(convId);
    loadConversation(convId);
  }, [loadConversation]);

  // Toggle theme
  const toggleTheme = useCallback(() => {
    setThemeState((prev) => {
      const newTheme = prev === 'light' ? 'dark' : 'light';
      localStorage.setItem('chat-theme', newTheme);
      document.documentElement.setAttribute('data-theme', newTheme);
      return newTheme;
    });
  }, []);

  // Toggle sidebar
  const toggleSidebar = useCallback(() => {
    setIsSidebarOpen((prev) => !prev);
  }, []);

  // Delete conversation
  const deleteConversation = useCallback((convId) => {
    setConversations((prev) => prev.filter((conv) => conv.id !== convId));

    // If deleted conversation is current, start a new one
    if (conversationId === convId) {
      setConversationId(null);
      setMessages([]);
      setInputValue('');
    }
  }, [conversationId]);

  return {
    // State
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

    // Actions
    toggleWindow,
    toggleExpand,
    toggleSidebar,
    setInputValue,
    sendMessage,
    deleteConversation,
    createConversation,
    loadConversation,
    selectConversation,
    toggleTheme,
    setIsOpen,
  };
}

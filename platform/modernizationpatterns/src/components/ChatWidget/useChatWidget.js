import { useState, useCallback, useRef, useEffect } from 'react';

// Chat API endpoint - auto-detect environment
const API_BASE = window.location.hostname === 'localhost'
  ? 'http://localhost:5001/api/chat'
  : 'https://modernizationpatterns-chat-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/api/chat';

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

  const syncConversationMessages = useCallback((convId, nextMessages) => {
    setConversations((prev) => {
      const exists = prev.some((conv) => conv.id === convId);
      const updatedAt = new Date().toISOString();

      if (!exists) {
        return [{
          id: convId,
          createdAt: updatedAt,
          updatedAt,
          messages: nextMessages,
        }, ...prev];
      }

      return prev.map((conv) => (
        conv.id === convId
          ? { ...conv, messages: nextMessages, updatedAt }
          : conv
      ));
    });
  }, []);

  // Initialize with user ID and load theme
  useEffect(() => {
    const uid = `user-${Date.now()}`;
    setUserId(uid);

    // Load theme from localStorage (but don't apply to DOM yet, wait for component mount)
    const savedTheme = localStorage.getItem('chat-theme') || 'light';
    setThemeState(savedTheme);
  }, []);

  useEffect(() => {
    if (!userId) return;

    const loadUserConversations = async () => {
      try {
        const response = await fetch(`${API_BASE}/user/${userId}/conversations`);
        if (!response.ok) return;

        const data = await response.json();
        if (Array.isArray(data.conversations)) {
          setConversations(data.conversations);
        }
      } catch (error) {
        console.error('Failed to load conversations:', error);
      }
    };

    loadUserConversations();
  }, [userId]);

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

  // Sync theme state with DOM container (only ChatWidget, not whole page)
  useEffect(() => {
    // Apply theme only to the chat widget container, not the whole page
    const container = document.querySelector('.chat-widget-container');
    if (container) {
      container.setAttribute('data-theme', theme);
      console.log(`🎨 ChatWidget theme updated: ${theme}`);
    }
  }, [theme]);

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

    // Keep local snapshot so we can still switch conversations if API retrieval fails.
    const localConversation = conversations.find((conv) => conv.id === convId);

    try {
      const response = await fetch(`${API_BASE}/${convId}?userId=${userId}`);
      if (!response.ok) {
        throw new Error(`Failed to load conversation ${convId}: ${response.status}`);
      }

      const data = await response.json();
      const loadedMessages = data.messages || [];
      setConversationId(data.id);
      setMessages(loadedMessages);
      syncConversationMessages(data.id, loadedMessages);
    } catch (error) {
      console.error('Failed to load conversation from API, using local copy:', error);

      // Fallback to sidebar state so users can keep navigating local chat history.
      if (localConversation) {
        setConversationId(localConversation.id);
        setMessages(localConversation.messages || []);
      }
    }
  }, [userId, conversations, syncConversationMessages]);

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
    setMessages((prev) => {
      const next = [...prev, userMessage];
      syncConversationMessages(convId, next);
      return next;
    });
    setInputValue('');
    setIsLoading(true);

    try {
      console.log(`📤 Sending message to ${API_BASE}/stream`, {
        conversationId: convId,
        message: userInputContent.substring(0, 50) + '...',
      });

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

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`❌ API error: ${response.status}`, errorText);
        throw new Error(`API returned ${response.status}: ${errorText.substring(0, 200)}`);
      }
      console.log('✅ Stream connection established');

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

      // Helper to process a single SSE line
      const processLine = (line) => {
        // Parse event type
        if (line.startsWith('event: ')) {
          currentEventType = line.slice(7).trim();
        }
        // Parse data - only process token events
        else if (line.startsWith('data: ')) {
          const rawData = line.slice(6);  // Don't trim - preserve leading/trailing spaces
          // Decode escaped newlines from SSE transport
          const data = rawData.replace(/\\n/g, '\n');

          // Only add token data to message content
          if (currentEventType === 'token' && data) {
            setMessages((prev) => {
              const updated = [...prev];
              const lastMsg = updated[updated.length - 1];
              // Append token directly - backend already handles spacing
              lastMsg.content += data;
              syncConversationMessages(convId, updated);
              return updated;
            });
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
      };

      while (true) {
        const { done, value } = await reader.read();

        if (value) {
          buffer += decoder.decode(value, { stream: true });
        }

        // Process complete lines from buffer
        const lines = buffer.split('\n');
        // Keep last (potentially incomplete) line in buffer
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.trim()) {
            processLine(line);
          }
        }

        if (done) {
          // Flush remaining buffer with final decode
          buffer += decoder.decode(new Uint8Array(), { stream: false });

          // Process any remaining complete lines
          const finalLines = buffer.split('\n');
          for (const line of finalLines) {
            if (line.trim()) {
              processLine(line);
            }
          }
          break;
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
        syncConversationMessages(convId, updated);
        return updated;
      });
    } catch (error) {
      console.error('❌ Message sending failed:', error);

      // Show user-friendly error message
      const errorMsg = error.message.includes('Failed to fetch')
        ? '❌ Cannot connect to API. Is the server running? Check if http://localhost:5001 is accessible.'
        : error.message.includes('undefined mode')
        ? '❌ Invalid response format from API. Server may not be running correctly.'
        : `❌ Error: ${error.message}`;

      setMessages((prev) => {
        const next = [
          ...prev,
          {
            role: 'assistant',
            content: errorMsg,
            timestamp: new Date().toISOString(),
          },
        ];
        syncConversationMessages(convId, next);
        return next;
      });
    } finally {
      setIsLoading(false);
    }
  }, [inputValue, conversationId, userId, createConversation, syncConversationMessages]);

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
      console.log(`🎨 Toggling theme: ${prev} → ${newTheme}`);
      localStorage.setItem('chat-theme', newTheme);
      // Apply theme only to ChatWidget container, not entire page
      const container = document.querySelector('.chat-widget-container');
      if (container) {
        container.setAttribute('data-theme', newTheme);
        // Force style recalculation
        void container.offsetHeight;
      }
      return newTheme;
    });
  }, []);

  // Toggle sidebar
  const toggleSidebar = useCallback(() => {
    setIsSidebarOpen((prev) => !prev);
  }, []);

  // Delete conversation
  const deleteConversation = useCallback(async (convId) => {
    if (userId) {
      try {
        await fetch(`${API_BASE}/${convId}?userId=${userId}`, {
          method: 'DELETE',
        });
      } catch (error) {
        console.error('Failed to delete conversation on API:', error);
      }
    }

    setConversations((prev) => prev.filter((conv) => conv.id !== convId));

    // If deleted conversation is current, start a new one
    if (conversationId === convId) {
      setConversationId(null);
      setMessages([]);
      setInputValue('');
    }
  }, [conversationId, userId]);

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

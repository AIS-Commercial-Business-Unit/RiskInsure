**ChatWidget — Components Documentation (Layman + Technical Overview)**

**Purpose (one-liner)**
- The ChatWidget provides an embeddable chat UI for asking questions about modernization patterns; it shows a greeting, lets users start new conversations, view conversation history, and stream assistant responses in real time.

**1) Layman Explanation (simple, non-technical)**
- The chat appears as a small round button in the corner. Click it to open a chat window.
- When open, the window shows a header with the chat name, a main area with messages, and a box at the bottom where you type your question.
- There's a left panel (sidebar) that lists prior chats and a big "New Chat" button. On phones the sidebar covers the screen; on desktop it sits side-by-side when you make the chat fullscreen.
- When you send a question, the assistant replies gradually — you see the answer appear as if the assistant is typing.
- The app saves conversations so you can return to them later.

**2) High-level UI pieces and what they do**
- Chat Bubble: tiny round button shown when chat is closed. Click to open or reopen the chat.
- Chat Window: the main panel containing header, messages, and input box. Can be normal or expanded (fullscreen).
- Sidebar: shows conversation list, provides "New Chat" and theme toggle, and lets you select or delete conversations.
- Message List: scrollable area with messages from user and assistant; streaming messages are updated token-by-token.
- Input Box: text area and send button to enter questions.
- Prompt file (server-side): the assistant behaviour and instructions are kept in a separate `system-prompt.txt` on the API side so you can change assistant tone without redeploying frontend.

**3) How the pieces talk to each other (overview)**
- The `useChatWidget` hook manages all UI state (open/expanded/sidebar, messages, conversations, userId) and handles API calls.
- When you click New Chat or open the widget, `useChatWidget` ensures a `userId` exists and calls `POST /api/chat/new` to create a conversation (this call runs in background to keep the UI instant).
- Sending a message: `useChatWidget` posts to `POST /api/chat/stream`. The backend streams token events (SSE). The hook appends tokens to the assistant message in real time.
- When streaming ends the backend sends metadata including citations which the frontend displays with the assistant message.
- Conversation history is retrieved with `GET /api/chat/{conversationId}?userId=...`.

**4) File-by-file mapping and responsibilities**
- `ChatWidget.jsx` (composition)
  - Top-level component that wires the hook (`useChatWidget`) to UI components.
  - Decides when to render `Sidebar`, `ChatWindow`, or `ChatBubble`.
  - Handles Escape key behavior (close/exit fullscreen).

- `useChatWidget.js` (state + side effects)
  - Central hook that contains component state: `isOpen`, `isExpanded`, `isSidebarOpen`, `messages`, `conversations`, `conversationId`, `userId`, `theme`, etc.
  - Creates a `userId` if none exists and caches it in the hook state.
  - Implements `createConversation()`, `loadConversation()`, `sendMessage()` (with SSE stream parsing), `toggleWindow()`, `toggleSidebar()`, `toggleTheme()` and more.
  - Handles auto-scroll, hiding body scrollbar when expanded, and streaming parsing logic that appends tokens and citations.
  - Resolves the API base (`API_BASE`) from env (Vite `VITE_CHAT_API_BASE`, `window.__CHAT_API_BASE__`, fallback to same-origin `/api/chat`). This makes the frontend work locally and when hosted on Azure.

- `ChatWindow.jsx` (UI: header, messages, input)
  - Renders the chat header (title, expand/close buttons), `MessageList` and `InputBox`.
  - Adds CSS classes for `expanded` and `sidebar-open` so the layout adapts (side-by-side on large screens, fullscreen on mobile).
  - Accepts `showMenuButton` to show/hide the sidebar toggle.

- `Sidebar.jsx` (UI: conversation list)
  - Renders "New Chat" button, conversation groups, and theme toggle.
  - Calls `onNewChat` and `onSelectConversation` handlers provided by the hook.

- `MessageList.jsx` (UI: message rendering)
  - Renders messages with different styling for `assistant` and `user` messages.
  - Shows streaming state visually (typing or streaming style), timestamps, and citations if present.

- `InputBox.jsx` (UI: input and send action)
  - Textarea and send button. Handles Enter/Shift+Enter and disables while streaming.
  - Calls `onSend` from the hook to post messages.

- `ConversationItem.jsx` (UI: single conversation row)
  - Compact item showing preview, timestamp, and delete action. Triggers `onClick` to load the conversation.

- `ChatBubble.jsx` (UI: closed-state button)
  - Small circular button shown when widget is closed; opens the widget when clicked.

- `ChatWidget.css` (styling)
  - Contains all styles and responsive rules.
  - Defines `chat-window.expanded.sidebar-open` to shift the chat when the sidebar is open in fullscreen mode.
  - Controls z-index and pointer-events (important to keep header buttons clickable in expanded mode).

- `ENVIRONMENT.md` (doc)
  - Explains how `API_BASE` is configured for production vs dev (Vite env, runtime override, same-origin fallback).

**5) Server-side prompt and API integration notes**
- The system prompt (assistant instructions) has been moved to `src/prompts/system-prompt.txt` on the Chat API service. The backend loads it and replaces `{REFERENCE_MATERIAL}` with the top 3 pattern contents before calling the LLM.
- This separation lets you change assistant behavior without recompiling the backend code (edit the file and restart the service), and it keeps prompt content out of the controller code.

**6) Typical flows (step-by-step)**
- Open chat (first time)
  1. User clicks `ChatBubble` → `toggleWindow()` opens UI and shows greeting instantly.
  2. `createConversation()` runs in background to create a conversation record via `POST /api/chat/new`.
  3. Greeting displayed while backend returns conversation id.

- Send a message
  1. InputBox calls `sendMessage()` on Enter or Send.
  2. Hook ensures conversation exists; if not, it creates one.
  3. Hook posts to `POST /api/chat/stream` and reads server-sent events.
  4. For each `token` event, hook appends text to the assistant message for a streaming effect.
  5. On `completion_done`/`response_metadata` the hook attaches citations and marks streaming finished.
  6. Conversation saved to Cosmos DB by backend.

- New Chat from Sidebar
  1. User clicks "New Chat" → handler opens chat and calls create conversation.
  2. UI shows empty conversation or greeting immediately (depending on implementation).

**7) How to edit behaviour or visuals**
- Change assistant prompt: edit `platform/modernizationpatterns/Api/chat/src/prompts/system-prompt.txt` and restart the Chat API.
- Change backend prompt logic: update `BuildSystemPrompt()` in `ChatController.cs` if you want new placeholders or formatting.
- Change API endpoint host for frontend: update `VITE_CHAT_API_BASE` at build time or set `window.__CHAT_API_BASE__` in `index.html`.
- Visual tweaks: edit `ChatWidget.css` — z-index and pointer-events are sensitive for header button clickability.

**8) Running locally (quick guide)**
- Start backend Chat API (example):
  ```powershell
  cd platform/modernizationpatterns/Api/chat
  $env:ASPNETCORE_ENVIRONMENT='Development'
  dotnet run --no-build -c Debug
  ```
- Start frontend dev server (root or frontend folder):
  ```bash
  npm install
  npm run dev
  ```
- Verify flows in browser devtools (Console + Network): watch `POST /api/chat/new` and `POST /api/chat/stream` SSE messages and the `response_metadata` event for citations.

**9) Troubleshooting checklist (common issues)**
- New Chat does not trigger: check console logs for `Sidebar: New Chat clicked` and `createConversation() start` (these logs were added for debugging). If click logs show but no createConversation log, an overlay or CSS pointer capture is blocking the button.
- Streaming shows nothing: confirm backend is reachable (API_BASE), check Network tab for SSE stream, ensure CORS is allowed if origins differ.
- Greeting not visible immediately: ensure `toggleWindow()` sets messages before awaiting `createConversation()` (it does this by design).

**10) Where to add tests / next improvements**
- Unit test `useChatWidget` logic by mocking fetch and SSE stream.
- Add integration test for `POST /api/chat/new` and `POST /api/chat/stream` in the Api project.
- Add accessibility checks (keyboard navigation and ARIA labels) for header and sidebar controls.

----

If you want, I can:
- Generate a smaller quick-reference cheat-sheet file summarizing only props and exported functions, or
- Add inline file links in the repo (e.g., add references from README to each component file).

Which follow-up would you like? (cheat-sheet / tests scaffold / README links)

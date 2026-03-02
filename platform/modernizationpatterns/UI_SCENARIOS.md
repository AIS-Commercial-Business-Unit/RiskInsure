# ChatBot UI Scenarios - Modernization Patterns Chat

**Project**: RiskInsure Modernization Patterns ChatBot  
**Framework**: React + Tailwind CSS  
**Port**: 3000  
**API Backend**: localhost:5000

---

## Overview

This document outlines 4 UI scenarios for the Modernization Patterns ChatBot widget. Each scenario demonstrates different features and user experiences.

---

## SCENARIO 1: Single Chat (Light Mode) - Conversation History Visible

**Use Case**: Day-time development, reading documentation, reviewing conversation history  
**Features**: Full message history, light background, easy on eyes  

### Visual Layout

```
╔════════════════════════════════════════════════════════════════╗
║   📘 Modernization Patterns Chat              [☀️ Light] [✕]  ║
╠════════════════════════════════════════════════════════════════╣
║                                                                ║
║   RiskInsure RAG ChatBot | 123 Patterns Indexed               ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║                                                                ║
║  👤 You (Today 2:15 PM)                                       ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ What is CQRS pattern and when should I use it?          │ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  🤖 Assistant (2:16 PM) [RAG + Azure OpenAI]                 ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ CQRS (Command Query Responsibility Segregation) is a    │ ║
║  │ pattern that separates your model into two objects:      │ ║
║  │                                                          │ ║
║  │ • Write Model: Handles commands (CreateOrder, Update)   │ ║
║  │ • Read Model: Optimized for queries (GetOrders)         │ ║
║  │                                                          │ ║
║  │ Use CQRS when:                                           │ ║
║  │ ✓ Complex domains with different read/write patterns    │ ║
║  │ ✓ Multiple user perspectives on same data               │ ║
║  │ ✓ Need independent scaling of reads vs writes           │ ║
║  │ ✓ Performance critical systems                          │ ║
║  │                                                          │ ║
║  │ Caution: Adds complexity, eventual consistency needed   │ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  👤 You (2:17 PM)                                             ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ How does event sourcing relate to CQRS?                 │ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  🤖 Assistant (2:18 PM)                                       ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ Event sourcing and CQRS complement each other well...   │ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║                        INPUT SECTION                           ║
║                                                                ║
║  ┌────────────────────────────────────────────────────────────┐ ║
║  │ Ask about architecture patterns, CQRS, saga, events...  │ ║
║  │                                                           │ ║
║  └────────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  [Send 🔤]  [📋 Copy]  [🗑️ Clear Chat]  [↓ Scroll]           ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║ ✓ Connected | 3 Messages | Patterns: 123 | Last: 2:18 PM     ║
╚════════════════════════════════════════════════════════════════╝
```

### Key Features
- ✅ Light mode (white/gray background)
- ✅ User messages right-aligned with light background
- ✅ Assistant messages left-aligned with blue highlight
- ✅ Timestamps for each message
- ✅ Scrollable conversation history
- ✅ Action buttons (Send, Copy, Clear, Scroll)
- ✅ Status bar with connection info

---

## SCENARIO 2: Single Chat (Dark Mode) - Streaming Response Animation

**Use Case**: Night-time testing, real-time response streaming, focus mode  
**Features**: Dark background, streaming animation indicator, live response text  

### Visual Layout

```
╔════════════════════════════════════════════════════════════════╗
║   📘 Modernization Patterns Chat              [🌙 Dark] [✕]  ║
╠════════════════════════════════════════════════════════════════╣
║                                                                ║
║   RiskInsure RAG ChatBot | 123 Patterns Indexed               ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  ║
║                                                                ║
║  👤 You (Today 3:45 PM)                                       ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ Event sourcing captures all state changes as events...  │ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  🤖 Assistant (3:46 PM) [STREAMING NOW...]                    ║
║  ┌──────────────────────────────────────────────────────────┐ ║
║  │ Event sourcing is a powerful pattern where you store    │ ║
║  │ all changes to application state as a sequence of      │ ║
║  │ immutable events in append-only log.                   │ ║
║  │                                                          │ ║
║  │ Benefits:                                               │ ║
║  │ • Complete audit trail of all state changes           │ ║
║  │ • Temporal queries - know state at any point in time  │ ║
║  │ • Event replay for debugging and recovery             │ ║
║  │ • Natural integration with CQRS pattern               │ ║
║  │ • Microservices communication via event bus           │ ║
║  │                                                          │ ║
║  │ Consider event sourcing when:                          │ ║
║  │ ✓ Audit requirements are critical                    │ ║
║  │ ✓ Complex domain models need traceability            │ ║
║  │ ✓ Multiple systems needed to react to state changes  │ ║
║  │ ✓ Performance of reads/writes needed to differ       │ ║
║  │                                                          │ ║
║  │ Trade-offs: Increased storage, eventual consistency... ▌ ║
║  └──────────────────────────────────────────────────────────┘ ║
║                           ⏳ [3.2s elapsed | 45% complete]   ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║                        INPUT SECTION                           ║
║                                                                ║
║  ┌────────────────────────────────────────────────────────────┐ ║
║  │ Type your next question...                              │ ║
║  │                                                           │ ║
║  └────────────────────────────────────────────────────────────┘ ║
║                                                                ║
║  [Send 🔤]  [⏹️ Stop]  [📋 Copy]  [🗑️ Clear]                 ║
║                                                                ║
╠════════════════════════════════════════════════════════════════╣
║ ✓ Connected | 1 Messages | Streaming: YES | Speed: Live       ║
╚════════════════════════════════════════════════════════════════╝
```

### Key Features
- ✅ Dark mode (charcoal/black background)
- ✅ Light text for contrast
- ✅ Streaming animation: `▌` cursor at end of response
- ✅ Progress indicator: elapsed time + completion percentage
- ✅ Blinking cursor effect (visual streamer)
- ✅ Stop button to interrupt streaming
- ✅ Reduced eye strain for long sessions

---

## SCENARIO 3: Conversation List (Left Sidebar) - Single Chat View

**Use Case**: Managing multiple conversations, switching between topics, conversation history  
**Features**: Persistent sidebar with recent chats, organization by date  

### Visual Layout

```
╔═══════════════════╦════════════════════════════════════════════╗
║                   ║   📘 Modernization Chat        [✕]        ║
║   CONVERSATIONS   ╠════════════════════════════════════════════╣
║   ═══════════════ ║                                            ║
║                   ║   RiskInsure RAG ChatBot                   ║
║ 🆕 [New Chat]     ║                                            ║
║                   ║════════════════════════════════════════════║
║ Today             ║                                            ║
║ ───────────────── ║  👤 You (3:50 PM)                         ║
║ ❯ When to use    ║  ┌────────────────────────────────────────┐║
║   saga pattern?  ║  │ When should I use saga vs process mgr? ││
║   (Current)      ║  │                                        ││
║   2:45 PM        ║  └────────────────────────────────────────┘║
║                   ║                                            ║
║ • CQRS basics    ║  🤖 Assistant (3:51 PM)                   ║
║   2:22 PM        ║  ┌────────────────────────────────────────┐║
║                   ║  │ Both saga and process manager patterns ││
║ • Event sourcing ║  │ coordinate workflows, but with key    ││
║   1:15 PM        ║  │ differences...                         ││
║                   ║  │                                        ││
║ Yesterday        ║  │ Saga: Distributed transactions via    ││
║ ───────────────── ║  │ choreography (event-driven) or        ││
║ • Strangler fig  ║  │ orchestration (central coordinator)   ││
║   pattern        ║  │                                        ││
║   5:30 PM        ║  │ Process Manager: Explicitly handles    ││
║                   ║  │ workflow state, timeouts, retries      ││
║ • Anti-patterns  ║  │                                        ││
║   11:20 AM       ║  │ Choose Process Manager when...        ││
║                   ║  │ ✓ Complex multi-step orchestration   ││
║ Last Week        ║  │ ✓ Long-running workflows needed      ││
║ ───────────────── ║  │ ✓ Need explicit compensation logic   ││
║ • Microservices  ║  └────────────────────────────────────────┘║
║   arch 101       ║                                            ║
║ • Docker basics  ║  👤 You (3:52 PM)                         ║
║ • K8s intro      ║  ┌────────────────────────────────────────┐║
║                   ║  │ What about actor model?                ││
║                   ║  └────────────────────────────────────────┘║
║ [⚙️ Settings]    ║                                            ║
║                   ║  🤖 Assistant (typing...)                  ║
║                   ║  [..text coming...]                      ║
║                   ║                                            ║
║                   ║ [Send 🔤] [📋] [🗑️]                       ║
╚═══════════════════╩════════════════════════════════════════════╝
```

### Key Features
- ✅ Left sidebar (300px fixed width, collapsible)
- ✅ "New Chat" button at top
- ✅ Conversations grouped by date (Today, Yesterday, Last Week)
- ✅ Current chat highlighted with arrow (❯)
- ✅ Timestamps for each conversation
- ✅ Preview of first message (truncated)
- ✅ Settings button at bottom
- ✅ Click to switch between conversations
- ✅ Responsive: sidebar hides on mobile

---

## SCENARIO 4: Dual Theme Toggle - Light ↔ Dark Mode

**Use Case**: User preference, accessibility, different lighting conditions  
**Features**: Theme toggle button, persistent preference, smooth transitions  

### Visual Layout

```
═══════════════════════════════════════════════════════════════════════

                    LIGHT MODE                  DARK MODE

╔════════════════════════════╗  ║  ╔════════════════════════════╗
║ 📘 Chat  ☀️ Light | 🌙 Dark║  ║  ║ 📘 Chat  ☀️ Light | 🌙 Dark║
║ ────────────────────────── ║  ║  ║ ────────────────────────── ║
║                            ║  ║  ║                            ║
║ 👤 You: What is saga?     ║  ║  ║ 👤 You: What is saga?     ║
║ ┌──────────────────────┐  ║  ║  ║ ┌──────────────────────┐  ║
║ │ Light background    │  ║  ║  ║ │ Dark background      │  ║
║ │ Dark text           │  ║  ║  ║ │ Light text           │  ║
║ │ Blue hyperlinks     │  ║  ║  ║ │ Cyan hyperlinks      │  ║
║ │ Gray borders        │  ║  ║  ║ │ Light borders        │  ║
║ └──────────────────────┘  ║  ║  ║ └──────────────────────┘  ║
║                            ║  ║  ║                            ║
║ 🤖 Assistant:              ║  ║  ║ 🤖 Assistant:              ║
║ ┌──────────────────────┐  ║  ║  ║ ┌──────────────────────┐  ║
║ │ Light blue bg        │  ║  ║  ║ │ Dark blue bg         │  ║
║ │ Easy on eyes (day)   │  ║  ║  ║ │ Reduces eye strain   │  ║
║ │ Good for daytime     │  ║  ║  ║ │ Good for nighttime   │  ║
║ │ Higher contrast      │  ║  ║  ║ │ Soft contrast        │  ║
║ └──────────────────────┘  ║  ║  ║ └──────────────────────┘  ║
║                            ║  ║  ║                            ║
║ Input: [Type...] [Send]   ║  ║  ║ Input: [Type...] [Send]   ║
║ Status: Connected          ║  ║  ║ Status: Connected          ║
╚════════════════════════════╝  ║  ╚════════════════════════════╝

═══════════════════════════════════════════════════════════════════════

                    🔄 TOGGLE MECHANISM

  Location: Top-right corner of chat window
  Button: [☀️ Light] [🌙 Dark] (pills/toggle)
  
  On Click:
  ┌─────────────────────────────────────────────────────────┐
  │ 1. Smooth CSS transition (0.3s ease-in-out)            │
  │ 2. Background color fade                               │
  │ 3. Text color invert                                   │
  │ 4. Save preference to localStorage                     │
  │ 5. Apply on next page load                             │
  └─────────────────────────────────────────────────────────┘
```

### Color Scheme

#### Light Mode
```yaml
Background:       #FFFFFF (white)
Text:             #1F2937 (dark gray)
Message (User):   #E0F2FE (light blue)
Message (Asst):   #F0F9FF (lighter blue)
Borders:          #D1D5DB (light gray)
Links:            #0284C7 (sky blue)
Buttons:          #3B82F6 (blue)
```

#### Dark Mode
```yaml
Background:       #111827 (almost black)
Text:             #F3F4F6 (light gray)
Message (User):   #1F3A5F (dark blue)
Message (Asst):   #1E3A8A (darker blue)
Borders:          #374151 (dark gray)
Links:            #06B6D4 (cyan)
Buttons:          #0EA5E9 (sky blue)
```

### Key Features
- ✅ Toggle button in header (top-right)
- ✅ Smooth 0.3s transition between themes
- ✅ Preference persists in browser localStorage
- ✅ System preference detection (dark mode @ night)
- ✅ Keyboard shortcut: `Ctrl+Shift+T` to toggle
- ✅ Accessible color contrast ratios (WCAG AA)

---

## COMPARISON MATRIX

| Feature | Scenario 1 | Scenario 2 | Scenario 3 | Scenario 4 |
|---------|-----------|-----------|-----------|-----------|
| **Theme** | Light ☀️ | Dark 🌙 | Both | Toggle 🔄 |
| **Conversations** | Single | Single | Multiple | Any |
| **Streaming** | Static | Real-time | Optional | Any |
| **History** | Full visible | Full visible | Full visible | Any |
| **Sidebar** | No | No | Yes | Optional |
| **Best For** | Daily dev | Night testing | Multi-topic | User choice |
| **Complexity** | Simple | Medium | Advanced | Medium |

---

## Implementation Roadmap

### Phase 1: Core Components
- [ ] ChatWindow component (Scenario 1)
- [ ] Message display (User & Assistant)
- [ ] Input field with send button
- [ ] API integration (localhost:5000)

### Phase 2: Streaming & Animations
- [ ] EventSource streaming (Scenario 2)
- [ ] Typing animation
- [ ] Progress indicator
- [ ] Stop button

### Phase 3: Conversation Management
- [ ] Sidebar component (Scenario 3)
- [ ] Conversation list
- [ ] New chat button
- [ ] Switch conversations

### Phase 4: Theming
- [ ] Theme toggle (Scenario 4)
- [ ] localStorage persistence
- [ ] CSS variable theming
- [ ] System preference detection

### Phase 5: Polish
- [ ] Copy-to-clipboard functionality
- [ ] Clear chat confirmation
- [ ] Auto-scroll to latest message
- [ ] Error boundaries & fallbacks
- [ ] Responsive design (mobile)

---

## Directory Structure

```
platform/modernizationpatterns/
├── Api/
│   ├── chat/                    # Chat API (.NET)
│   └── reindex/                 # Reindex Service (.NET)
├── ui/                          # NEW: React ChatWidget
│   ├── public/
│   │   ├── index.html
│   │   └── favicon.ico
│   ├── src/
│   │   ├── components/
│   │   │   ├── ChatWindow.jsx       # Main chat window
│   │   │   ├── MessageList.jsx      # Messages display
│   │   │   ├── InputBox.jsx         # Input & send
│   │   │   ├── Sidebar.jsx          # Conversation list
│   │   │   └── ThemeToggle.jsx      # Light/Dark toggle
│   │   ├── hooks/
│   │   │   ├── useChat.js           # Chat logic
│   │   │   ├── useTheme.js          # Theme management
│   │   │   └── useStreaming.js      # SSE streaming
│   │   ├── styles/
│   │   │   ├── globals.css          # Tailwind + custom
│   │   │   └── themes.css           # Light/Dark colors
│   │   ├── App.jsx
│   │   └── index.js
│   ├── package.json
│   ├── tailwind.config.js
│   ├── webpack.config.js
│   └── README.md
├── contracts/                   # Existing: Message definitions
├── docs/                        # Existing: Documentation
└── UI_SCENARIOS.md              # This file
```

---

## Running the UI

```bash
# Install dependencies
cd platform/modernizationpatterns/ui
npm install

# Development mode (hot reload)
npm start
# Opens: http://localhost:3000

# Build for production
npm run build

# Run tests
npm test
```

---

## API Contract

### Chat Endpoints
```
POST /api/chat/new?userId=<string>
  Response: { conversationId, userId, createdAt }

POST /api/chat/stream
  Body: { message: string, conversationId: string, userId: string }
  Response: SSE stream (text/event-stream)

GET /api/chat/{conversationId}?userId=<string>
  Response: { id, userId, messages, createdAt, updatedAt }

DELETE /api/chat/{conversationId}?userId=<string>
  Response: { success: boolean }
```

---

## Testing Checklist

- [ ] Scenario 1: Light mode displays all messages correctly
- [ ] Scenario 2: Dark mode streaming with cursor animation
- [ ] Scenario 3: Sidebar chat switching works smoothly
- [ ] Scenario 4: Theme toggle persists across page reload
- [ ] Mobile: Sidebar collapses, chat is full width
- [ ] Accessibility: WCAG AA contrast ratio (4.5:1)
- [ ] Performance: Initial load < 2 seconds
- [ ] Error handling: Network failures show graceful message
- [ ] Copy button: Copies assistant response to clipboard
- [ ] Clear chat: Asks for confirmation before clearing

---

## Next Steps

1. ✅ Review all 4 scenarios above
2. ⏳ Choose preferred UI implementation (recommend Scenario 3 + 4)
3. ⏳ Build React components
4. ⏳ Connect to Chat API (localhost:5000)
5. ⏳ Local testing in browser
6. ⏳ Deploy to Azure Container Apps
7. ⏳ Integration with main RiskInsure platform

---

**Status**: Ready to build 🚀  
**Last Updated**: March 1, 2026  
**Author**: GitHub Copilot

// Global using directives for all projects in the RiskInsure solution
// These namespaces are automatically available without explicit using statements

// .NET Core fundamentals
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Async operations
global using System.Collections.Concurrent;
global using System.Threading.Tasks.Dataflow;

// Serialization
global using System.Text.Json;
global using System.Text.Json.Serialization;

// Diagnostics and logging
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;

// Note: Project-specific usings should NOT be here
// Examples of what NOT to include:
// - NServiceBus namespaces (only in Infrastructure/Endpoint projects)
// - Microsoft.Azure.Cosmos (only in Infrastructure projects)
// - Microsoft.Extensions.* (context-specific)
// - Domain-specific namespaces (use explicit usings)

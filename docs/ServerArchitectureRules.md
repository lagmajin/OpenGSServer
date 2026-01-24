# Server Architecture Rules

## General Principles

### Language and Framework Usage
- **C# Version**: Use the latest stable version of C# (currently C# 14.0) where possible to leverage modern features like records, pattern matching, and performance improvements.
- **.NET Target**: Target .NET 10 or later for server components to ensure compatibility with the latest runtime features and security updates.

### Game Logic Separation
- **Server-Side Focus**: The server is responsible for authoritative state management, synchronization, and validation. It does not perform client-side computations like rendering or detailed physics.
- **Client-Side Focus**: Clients handle rendering, user input, and preliminary computations (e.g., prediction). The server validates and corrects as needed.

### Collision Detection and Physics
- **Principle**: The server does NOT perform collision detection or physics simulations.
  - Reason: To reduce server load, avoid latency issues, and prevent desynchronization. Collisions and physics are handled client-side for responsiveness.
  - Exception: Basic server-side validation (e.g., boundary checks) may be implemented if necessary for anti-cheat, but detailed simulations are avoided.
- **Implementation**: GameScene.cs and related classes should not include physics engines or collision logic. Use client-side libraries (e.g., Unity Physics) for such features.

### Synchronization
- **UDP for Real-Time**: Use reliable UDP (RUDP) for game state updates during matches.
- **TCP for Lobby**: Use TCP for non-real-time communications like lobby management and chat.

### Security and Performance
- **Anti-Cheat**: Server validates all critical actions (e.g., player positions, scores) to prevent cheating.
- **Rate Limiting**: Implement rate limits on messages to prevent DoS attacks.
- **Scalability**: Keep server logic lightweight; offload heavy computations to clients or dedicated services.

## Specific Rules for GameScene.cs
- Do not add collision detection or physics in UpdateFrame().
- Focus on object management, JSON serialization, and basic updates.
- For physics, rely on client-side simulation and server validation.

## References
- MatchServerComparison.md for server implementation details.
- LobbyServerComparison.md for lobby architecture.
# Easebnb

A modern full-stack platform built with .NET backend and Next.js frontend. This is a foundation project with infrastructure complete, ready for domain-specific feature development.

## Project Structure

```
easebnb/
├── server/                 # .NET Backend
│   ├── Aspire/            # .NET Aspire hosting configuration
│   ├── BuildingBlocks/    # Shared infrastructure libraries
│   ├── Database/          # Database configuration
│   ├── Easebnb.WebApi/    # Main API project
│   ├── Modules/           # Domain modules (Identity, Organization)
│   └── docker-compose.yml # Infrastructure services (PostgreSQL, RabbitMQ)
├── web/                   # Next.js Frontend
│   ├── src/
│   │   ├── app/          # Routes (App Router)
│   │   ├── components/   # UI components (shadcn/ui)
│   │   ├── features/     # Domain-specific business logic
│   │   ├── lib/          # Core utilities (API client, env, query-client)
│   │   ├── stores/       # Zustand state management
│   │   └── providers/    # React providers
│   └── README.md         # Detailed frontend documentation
└── README.md             # This file
```

## Tech Stack

### Backend (.NET)
- **.NET** with ASP.NET Core Web API
- **.NET Aspire** for cloud-ready development
- **Entity Framework Core** with PostgreSQL
- **MassTransit** for messaging (RabbitMQ)
- **MediatR** for CQRS pattern
- **FluentValidation** for request validation
- **Serilog** for structured logging
- **OpenTelemetry** for observability
- **Scalar** for API documentation
- **Modular architecture** with separation of concerns

### Frontend (Next.js)
- **Next.js 16** (App Router, Turbopack)
- **React 19** with TypeScript (strict mode)
- **Tailwind CSS v4** and **shadcn/ui** for styling
- **TanStack Query** for server state management
- **Zustand** for client-side state
- **Zod** for schema validation
- **API proxy** for seamless backend communication

## Getting Started

### Prerequisites

- .NET SDK (latest version)
- Node.js 18+ and npm
- Docker and Docker Compose
- Git

### Infrastructure Setup

Start the required infrastructure services (PostgreSQL and RabbitMQ):

```bash
cd server
docker compose up -d
```

This will start:
- **PostgreSQL** on port 5432
- **RabbitMQ** on ports 5672 (AMQP) and 15672 (management UI)

### Backend Setup

```bash
cd server

# Run the API
dotnet run --project Easebnb.WebApi/Easebnb.WebApi.csproj
```

The API will be available at `https://localhost:7000` (or configured port).
API documentation is available at `/docs` (Scalar UI) when running in development mode.

### Frontend Setup

```bash
cd web

# Install dependencies
npm install

# Copy environment configuration
cp .env.example .env.local

# Edit .env.local and set API_PROXY_URL to your backend URL
# Example: API_PROXY_URL=http://localhost:5282

# Start development server
npm run dev
```

The frontend will be available at `http://localhost:3000`.

## Architecture

### Backend Architecture

The backend follows a modular monolith architecture:

- **BuildingBlocks**: Shared infrastructure libraries (Application, Infrastructure, Utils, etc.)
- **Modules**: Domain-specific modules with clear boundaries
  - **Identity**: User authentication, authorization, account management
  - **Organization**: Organization and member management
- **Easebnb.WebApi**: Composition root and API endpoints
- **Database**: EF Core database configuration

Key patterns:
- CQRS with MediatR
- Domain Events for intra-module communication
- Integration Events for inter-module communication via MassTransit
- Repository pattern for data access

### Frontend Architecture

See [web/README.md](./web/README.md) for detailed frontend documentation.

Key principles:
- **Server Components by default**, Client Components only when needed
- **Feature-based organization** (`features/<domain>/`)
- **TanStack Query** for server state, **Zustand** for client UI state
- **Zod schemas** for type-safe API contracts
- **Unified API client** with automatic auth handling

## Environment Configuration

### Backend

Configure via `appsettings.json` or environment variables:
- Database connection strings
- RabbitMQ configuration
- Upload settings
- OpenTelemetry endpoint (optional)

### Frontend

Copy `.env.example` to `.env.local`:

| Variable              | Scope   | Description                                           |
| --------------------- | ------- | ----------------------------------------------------- |
| `NEXT_PUBLIC_API_URL` | Browser | Base URL for browser (empty = same-origin `/api`)     |
| `API_PROXY_URL`       | Server  | Backend origin for SSR and API proxy (e.g., `http://localhost:5282`) |

Recommended: Leave `NEXT_PUBLIC_API_URL` empty and set `API_PROXY_URL` for seamless same-origin requests.

## Available Commands

### Backend

```bash
# Run the API
dotnet run --project Easebnb.WebApi

# Run tests
dotnet test

# Build
dotnet build
```

### Frontend

| Command             | Description                          |
| ------------------- | ------------------------------------ |
| `npm run dev`       | Start dev server (Turbopack)         |
| `npm run build`     | Production build                     |
| `npm run start`     | Serve production build               |
| `npm run lint`      | ESLint check                         |
| `npm run typecheck` | TypeScript type checking             |

## API Documentation

When running the backend in development mode, API documentation is available at:
- **Scalar UI**: `https://localhost:<port>/docs`

The API uses JWT Bearer authentication. See the Scalar UI for available endpoints and authentication configuration.

## Authentication

The platform uses JWT-based authentication:

- **Backend**: JWT tokens with refresh token support
- **Frontend**: Automatic token handling via unified API client
- **Auto-refresh**: Concurrent 401s share a single refresh request
- **Storage**: sessionStorage (default) or localStorage based on "remember me" option

See [web/README.md](./web/README.md#authentication) for detailed frontend auth implementation.

## Development Guidelines

### Adding a New Feature

**Backend:**
1. Create module structure under `Modules/<Domain>/`
2. Define entities, value objects in the Core project
3. Implement infrastructure (repositories, services) in Infrastructure project
4. Register module services in Program.cs
5. Add API endpoints using the endpoints pattern

**Frontend:**
1. Create `features/<domain>/` with `api.ts`, `queries.ts`, `schemas.ts`, `components/`
2. Define Zod schemas for request/response types
3. Implement API service functions
4. Create TanStack Query hooks
5. Build UI components consuming the queries

### Code Style

- **Backend**: Follow .NET conventions, use records for DTOs, async/await pattern
- **Frontend**: TypeScript strict mode, ESLint rules, Prettier formatting

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

MIT License - see [LICENSE](./LICENSE) for details.

Copyright (c) 2026 Tang Ho Trung Nam
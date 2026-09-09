# Easebnb

**Easebnb** is a modern full-stack platform built with **ASP.NET Core, .NET Aspire, PostgreSQL, RabbitMQ, Next.js, and React**.

It follows a **modular monolith architecture** on the backend and a **feature-based architecture** on the frontend, providing a scalable foundation for building domain-driven applications.

## ✨ Tech Stack

### Backend

* **.NET / ASP.NET Core Web API**
* **.NET Aspire** — cloud-ready orchestration
* **Entity Framework Core + PostgreSQL**
* **MassTransit + RabbitMQ** — messaging
* **MediatR** — CQRS
* **FluentValidation** — request validation
* **Serilog + OpenTelemetry** — logging & observability
* **Scalar** — API documentation

### Frontend

* **Next.js 16** — App Router & Turbopack
* **React 19 + TypeScript**
* **Tailwind CSS v4 + shadcn/ui**
* **TanStack Query** — server state
* **Zustand** — client state
* **Zod** — schema validation

## 🏗️ Architecture

Easebnb uses a **modular monolith** architecture designed around clear domain boundaries.

```text
easebnb/
├── server/
│   ├── Aspire/              # .NET Aspire orchestration
│   ├── BuildingBlocks/      # Shared infrastructure
│   ├── Database/            # Database configuration
│   ├── Modules/             # Domain modules
│   └── Easebnb.WebApi/      # API composition root
│
└── web/
    └── src/
        ├── app/             # Next.js routes
        ├── components/      # Shared UI
        ├── features/       # Domain features
        ├── lib/             # Core utilities
        ├── providers/       # React providers
        └── stores/          # Client state
```

### Backend

* **Modular Monolith** for domain isolation
* **CQRS** with MediatR
* **Domain Events** for intra-module communication
* **Integration Events** with MassTransit/RabbitMQ
* **EF Core** for data persistence

Current modules include:

* **Identity** — authentication, authorization, and account management
* **Organization** — organizations and member management

### Frontend

* **Server Components by default**
* **Feature-based architecture**
* **TanStack Query** for server state
* **Zustand** for client state
* **Zod** for API contracts
* Centralized API client with authentication handling

See [`web/README.md`](./web/README.md) for frontend-specific documentation.

## 🚀 Getting Started

### Prerequisites

* .NET SDK
* Node.js 18+
* npm
* Docker & Docker Compose

### 1. Start infrastructure

```bash
cd server
docker compose up -d
```

This starts PostgreSQL and RabbitMQ.

### 2. Start the backend

```bash
cd server
dotnet run --project Easebnb.WebApi/Easebnb.WebApi.csproj
```

API documentation is available at:

```text
https://localhost:<port>/docs
```

### 3. Start the frontend

```bash
cd web
npm install
cp .env.example .env.local
npm run dev
```

The frontend runs at:

```text
http://localhost:3000
```

Configure `API_PROXY_URL` in `.env.local` to point to the backend API.

## 📚 Documentation

* [Frontend Documentation](./web/README.md)
* [API Documentation](https://localhost:<port>/docs) — available in development
* [License](./LICENSE)

## 🧪 Development

### Backend

```bash
dotnet build
dotnet test
```

### Frontend

```bash
npm run build
npm run lint
npm run typecheck
```

## 🤝 Contributing

Contributions are welcome. Please fork the repository, create a feature branch, and submit a pull request.

## 📄 License

MIT License. See [`LICENSE`](./LICENSE) for details.

Copyright © 2026 Tang Ho Trung Nam.

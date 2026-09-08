# REIGN AI

REIGN is an AI-assisted customer service and scheduling system I built while teaching myself full-stack application development, integrations, deployment, and production-oriented backend design.

The project combines conversational state, customer memory, appointment workflows, SMS, calendar integrations, a web dashboard, database persistence, health checks, Docker deployment, and provider fallbacks in one application.

## What It Does

REIGN is designed to handle a customer conversation from first contact through booking and follow-up.

Core workflow:

```text
Incoming customer message
        ↓
Conversation / intent handling
        ↓
Customer + conversation memory
        ↓
Service recommendation / scheduling
        ↓
Availability + overlap checks
        ↓
Pending appointment
        ↓
Customer confirmation
        ↓
Calendar synchronization
        ↓
Ongoing reschedule / cancel / follow-up
```

## Core Systems

- AI-assisted customer conversation with a built-in fallback path
- Persistent conversation state and customer intent memory
- Appointment booking, confirmation, rescheduling, and cancellation
- Scheduling conflict / overlap checks
- SMS provider abstraction with webhook-based inbound messaging
- Google Calendar integration with simulated fallback for development
- Blazor dashboard and operational UI
- EF Core persistence and migrations
- Health and configuration-status endpoints
- Environment-based secret management
- Docker deployment support
- Production CORS configuration

## Architecture

The repository is split into application layers rather than a single prototype script.

```text
Customer / SMS
     ↓
REIGN API
     ├── Conversation engine
     ├── Intent detection
     ├── Scheduling services
     ├── Customer memory
     ├── Calendar integration
     ├── SMS providers
     └── Persistence
            ↓
        EF Core / SQLite

REIGN Web
     ↓
Blazor dashboard / management UI
```

## Technology

- C# / .NET
- ASP.NET Core Web API
- Blazor
- Entity Framework Core
- SQLite
- Docker
- REST APIs
- OAuth
- Webhooks
- SMS provider integrations
- Google Calendar integration

## Production-Oriented Work

A major part of this project was learning the difference between code that merely runs locally and code that can survive deployment.

That work includes:

- secrets kept out of committed configuration
- provider configuration through environment variables
- database migrations
- persistent-volume planning
- startup validation
- health endpoints
- provider failure handling
- CORS configuration
- webhook parsing and validation
- Docker publishing
- deployment documentation for hosted environments

## Current Status

The core application, scheduling logic, AI pipeline, memory, database, UI, Docker support, and provider abstractions are implemented.

Live external integrations still depend on the appropriate hosted environment, credentials, OAuth consent, provider configuration, and webhooks. I intentionally keep that distinction visible rather than treating a simulated integration as a production success.

The production-readiness audit in this repository documents a checkpoint at approximately **95%**, with the remaining work centered on live hosting and external-provider configuration rather than application redesign.

See:

- [`REIGN-AI-DEPLOYMENT-STATUS.md`](REIGN-AI-DEPLOYMENT-STATUS.md)
- [`REIGN-AI-PRODUCTION-READINESS.md`](REIGN-AI-PRODUCTION-READINESS.md)
- [`DEPLOYMENT.md`](DEPLOYMENT.md)
- [`HOSTING.md`](HOSTING.md)
- [`PRODUCTION-LAUNCH-CHECKLIST.md`](PRODUCTION-LAUNCH-CHECKLIST.md)

## Verification

At the documented production-readiness checkpoint, the project reported:

- clean build with 0 errors / 0 warnings
- automated tests passing
- EF migrations applying successfully
- fresh database creation verified
- API health checks working
- Docker publish verified

The repository has continued to evolve after that checkpoint, especially around live SMS behavior and webhook handling.

## Running / Building

Build and test the solution with the installed .NET SDK:

```bash
dotnet build
dotnet test
```

Build the API Docker image:

```bash
docker build -t reign-api -f REIGN.API/Dockerfile .
```

Full environment and hosting instructions are in [`DEPLOYMENT.md`](DEPLOYMENT.md) and [`HOSTING.md`](HOSTING.md).

## Why I Built It

REIGN started as a practical automation idea and became one of the projects I used to teach myself application architecture, stateful workflows, API integrations, databases, deployment, failure handling, and the less glamorous parts of software engineering that appear after the demo works.

It is an active project, not a claim that every external integration is magically production-ready without credentials or provider setup.
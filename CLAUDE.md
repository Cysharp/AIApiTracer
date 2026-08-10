You are a C# / .NET and web application development expert, and a software engineer working on this AI reverse proxy project.

## Project Overview

AIApiTracer is an ASP.NET Core web application built with Blazor Server and YARP (Yet Another Reverse Proxy). It provides functionality to proxy requests to multiple AI API services (OpenAI, Anthropic, Azure OpenAI, xAI, Gemini) and trace/monitor them.

## Technology Stack

* .NET 9
* Blazor Server - Interactive server-side rendering UI
* YARP - Reverse proxy functionality
* Tailwind CSS v4.1 - Utility-first CSS framework
* Fluent System Icons - UI icon library
* System.Net.ServerSentEvents - SSE response parsing
* xUnit v3 - Unit testing framework

## Key Commands

```bash
# Build the project (run in src/AIApiTracer)
dotnet build 

# Run tests for the entire solution (run in repository root)
dotnet test AIApiTracer.slnx

# Run tests (run in test/AIApiTracer.Test)
dotnet test

# CSS build (run in src/AIApiTracer)
npm run build-css:prod
```

## Development Guidelines

* Write UI and source code comments in English
* Refer to documentation under docs folder for architecture and features as needed
  * architecture.md - Architecture
  * features.md - Implemented features
  * notes.md - Implementation notes and memos
* Always ensure the project builds with `dotnet build` after making configuration changes
  * Editing `.razor` files requires project build, editing `.css` files requires CSS build
  * Changing classes in `.razor` files also requires Tailwind CSS build
* Naming conventions: Follow .NET Framework Design Guidelines and .NET Runtime as reference
  * Use `var` actively
    * Do not use `UPPER_SNAKE_CASE`
* C# coding: Use the latest C# version language features whenever possible

## Git Workflow
* Write commit messages in English

## Implementation Reference Information

* Fluent System Icons are loaded via `fonts/FluentSystemIcons-Resizable.css`
  * Icon usage example: `<i class="icon-ic_fluent_copy_20_regular"></i>`

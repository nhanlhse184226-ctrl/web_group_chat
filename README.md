# PRN222 Lab3 - Razor Pages SignalR Group Chat

Web group chat built with ASP.NET Core Razor Pages and SignalR.

## Architecture

This project follows a simple 3-layer structure:

- Presentation layer: Razor Pages, SignalR Hub, static UI files
- Business layer: chat and file upload services
- Data access layer: in-memory user repository and local file storage repository

## Features

- Real-time group chat with SignalR
- Username setup
- Online user list
- Typing indicator
- Text messages
- Emoji shortcuts
- Sticker messages
- File upload and shared download link

## Run

```bash
dotnet run --project web_group_chat
```

Open:

```text
http://localhost:5017
```

Open two browser tabs with different usernames to test real-time chat and typing status.

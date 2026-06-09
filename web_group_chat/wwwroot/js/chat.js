(() => {
    const connectionStatus = document.getElementById("connectionStatus");
    const nameForm = document.getElementById("nameForm");
    const usernameInput = document.getElementById("usernameInput");
    const messageForm = document.getElementById("messageForm");
    const messageInput = document.getElementById("messageInput");
    const messagesList = document.getElementById("messagesList");
    const usersList = document.getElementById("usersList");
    const onlineCount = document.getElementById("onlineCount");
    const fileInput = document.getElementById("fileInput");
    const typingIndicator = document.getElementById("typingIndicator");
    const uploadStatus = document.getElementById("uploadStatus");
    const antiForgeryToken = document.querySelector("#antiForgeryForm input[name='__RequestVerificationToken']")?.value ?? "";

    let currentUsername = localStorage.getItem("chatUsername") || "";
    let isTyping = false;
    let typingStopTimer = 0;
    const typingUsers = new Map();

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect()
        .build();

    if (currentUsername) {
        usernameInput.value = currentUsername;
    }

    function setStatus(text, isOnline) {
        connectionStatus.textContent = text;
        connectionStatus.classList.toggle("online", isOnline);
    }

    function formatFileSize(bytes) {
        const units = ["B", "KB", "MB", "GB"];
        let value = bytes;
        let unit = 0;

        while (value >= 1024 && unit < units.length - 1) {
            value /= 1024;
            unit += 1;
        }

        return `${value.toFixed(value >= 10 || unit === 0 ? 0 : 1)} ${units[unit]}`;
    }

    function scrollToBottom() {
        messagesList.scrollTop = messagesList.scrollHeight;
    }

    function addSystemMessage(message) {
        const row = document.createElement("div");
        row.className = "system-message";
        row.textContent = message;
        messagesList.appendChild(row);
        scrollToBottom();
    }

    function updateTypingIndicator() {
        const now = Date.now();

        for (const [username, expiresAt] of typingUsers.entries()) {
            if (expiresAt <= now) {
                typingUsers.delete(username);
            }
        }

        const names = Array.from(typingUsers.keys());

        if (names.length === 0) {
            typingIndicator.textContent = "";
        } else if (names.length === 1) {
            typingIndicator.textContent = `${names[0]} is typing...`;
        } else if (names.length === 2) {
            typingIndicator.textContent = `${names[0]} and ${names[1]} are typing...`;
        } else {
            typingIndicator.textContent = "Several people are typing...";
        }
    }

    async function sendTypingState(nextTyping) {
        if (connection.state !== signalR.HubConnectionState.Connected || isTyping === nextTyping) {
            return;
        }

        isTyping = nextTyping;
        await connection.invoke("SendTyping", nextTyping);
    }

    function scheduleTypingStop() {
        window.clearTimeout(typingStopTimer);
        typingStopTimer = window.setTimeout(() => {
            sendTypingState(false).catch(error => console.error(error));
        }, 1200);
    }

    function addChatMessage(message) {
        const isMine = message.sender.toLowerCase() === currentUsername.toLowerCase();
        const row = document.createElement("article");
        row.className = `message-row ${isMine ? "mine" : "theirs"}`;

        const sender = document.createElement("div");
        sender.className = "message-sender";
        sender.textContent = isMine ? "Me" : message.sender;

        const bubble = document.createElement("div");
        bubble.className = "message-bubble";

        if (message.type === "sticker") {
            const image = document.createElement("img");
            image.className = "sticker-image";
            image.src = `/stickers/${message.content}.png`;
            image.alt = `${message.content} sticker`;
            bubble.appendChild(image);
        } else if (message.type === "file") {
            const link = document.createElement("a");
            link.className = "file-link";
            link.href = message.fileUrl;
            link.download = message.fileName;
            link.target = "_blank";
            link.rel = "noopener";
            link.textContent = message.fileName;

            const meta = document.createElement("span");
            meta.className = "file-meta";
            meta.textContent = formatFileSize(message.fileSize);

            bubble.append(link, meta);
        } else {
            bubble.textContent = message.content;
        }

        row.append(sender, bubble);
        messagesList.appendChild(row);
        scrollToBottom();
    }

    async function setUsername(username) {
        currentUsername = username.trim() || "Guest";
        localStorage.setItem("chatUsername", currentUsername);
        await connection.invoke("SetUsername", currentUsername);
    }

    connection.on("ReceiveMessage", message => {
        typingUsers.delete(message.sender);
        updateTypingIndicator();
        addChatMessage(message);
    });
    connection.on("ReceiveSystemMessage", addSystemMessage);
    connection.on("ReceiveUserList", users => {
        usersList.innerHTML = "";
        onlineCount.textContent = users.length.toString();
        users.forEach(user => {
            const item = document.createElement("li");
            const status = document.createElement("span");
            status.className = "user-dot";

            const name = document.createElement("span");
            name.textContent = user;

            item.append(status, name);
            usersList.appendChild(item);
        });
    });
    connection.on("ReceiveTyping", (username, typing) => {
        if (!username || username.toLowerCase() === currentUsername.toLowerCase()) {
            return;
        }

        if (typing) {
            typingUsers.set(username, Date.now() + 1800);
        } else {
            typingUsers.delete(username);
        }

        updateTypingIndicator();
    });

    connection.onreconnecting(() => setStatus("Reconnecting...", false));
    connection.onreconnected(async () => {
        setStatus("Connected", true);
        if (currentUsername) {
            await setUsername(currentUsername);
        }
    });
    connection.onclose(() => setStatus("Disconnected", false));

    nameForm.addEventListener("submit", async event => {
        event.preventDefault();

        try {
            await setUsername(usernameInput.value);
        } catch (error) {
            addSystemMessage("Could not set username.");
            console.error(error);
        }
    });

    messageForm.addEventListener("submit", async event => {
        event.preventDefault();
        const message = messageInput.value.trim();

        if (!message) {
            return;
        }

        try {
            if (!currentUsername) {
                await setUsername(usernameInput.value);
            }

            await connection.invoke("SendMessage", message);
            await sendTypingState(false);
            messageInput.value = "";
            messageInput.focus();
        } catch (error) {
            addSystemMessage("Could not send message.");
            console.error(error);
        }
    });

    messageInput.addEventListener("input", () => {
        if (!currentUsername) {
            return;
        }

        const hasText = messageInput.value.trim().length > 0;
        sendTypingState(hasText).catch(error => console.error(error));

        if (hasText) {
            scheduleTypingStop();
        }
    });

    messageInput.addEventListener("blur", () => {
        sendTypingState(false).catch(error => console.error(error));
    });

    document.querySelectorAll(".emoji-button").forEach(button => {
        button.addEventListener("click", () => {
            messageInput.value += button.dataset.emoji;
            messageInput.focus();
        });
    });

    document.querySelectorAll(".sticker-button").forEach(button => {
        button.addEventListener("click", async () => {
            try {
                if (!currentUsername) {
                    await setUsername(usernameInput.value);
                }

                await connection.invoke("SendSticker", button.dataset.sticker);
            } catch (error) {
                addSystemMessage("Could not send sticker.");
                console.error(error);
            }
        });
    });

    fileInput.addEventListener("change", async () => {
        const file = fileInput.files[0];

        if (!file) {
            return;
        }

        try {
            if (!currentUsername) {
                await setUsername(usernameInput.value);
            }
        } catch (error) {
            uploadStatus.textContent = "Set a username before sending files.";
            console.error(error);
            return;
        }

        const formData = new FormData();
        formData.append("chatFile", file);

        const request = new XMLHttpRequest();
        request.open("POST", "/?handler=UploadFile");
        request.setRequestHeader("RequestVerificationToken", antiForgeryToken);

        request.upload.addEventListener("progress", event => {
            if (!event.lengthComputable) {
                uploadStatus.textContent = `Uploading ${file.name}...`;
                return;
            }

            const progress = Math.round((event.loaded / event.total) * 100);
            uploadStatus.textContent = `Uploading ${file.name}: ${progress}%`;
        });

        request.addEventListener("load", async () => {
            try {
                if (request.status < 200 || request.status >= 300) {
                    const error = JSON.parse(request.responseText || "{}").error || "Upload failed.";
                    uploadStatus.textContent = error;
                    return;
                }

                const uploaded = JSON.parse(request.responseText);
                await connection.invoke("SendFileMessage", uploaded.fileName, uploaded.fileSize, uploaded.fileUrl);
                uploadStatus.textContent = `Uploaded ${uploaded.fileName}.`;
                fileInput.value = "";
            } catch (error) {
                uploadStatus.textContent = "Could not share uploaded file.";
                console.error(error);
            }
        });

        request.addEventListener("error", () => {
            uploadStatus.textContent = "Upload failed.";
        });

        request.send(formData);
    });

    connection.start()
        .then(async () => {
            setStatus("Connected", true);

            if (currentUsername) {
                await setUsername(currentUsername);
            } else {
                addSystemMessage("Set a username to start chatting.");
            }
        })
        .catch(error => {
            setStatus("Connection failed", false);
            console.error(error);
        });

    window.setInterval(updateTypingIndicator, 1000);
})();

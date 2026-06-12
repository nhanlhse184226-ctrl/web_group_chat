(() => {
    const connectionStatus = document.getElementById("connectionStatus");
    const nameForm = document.getElementById("nameForm");
    const usernameInput = document.getElementById("usernameInput");
    const messageForm = document.getElementById("messageForm");
    const messageInput = document.getElementById("messageInput");
    const messagesList = document.getElementById("messagesList");
    const usersList = document.getElementById("usersList");
    const onlineCount = document.getElementById("onlineCount");
    const typingIndicator = document.getElementById("typingIndicator");
    const emojiButton = document.getElementById("emojiButton");
    const emojiPicker = document.getElementById("emojiPicker");
    const stickerButton = document.getElementById("stickerButton");
    const stickerPicker = document.getElementById("stickerPicker");
    const attachInput = document.getElementById("attachInput");
    const attachPreview = document.getElementById("attachPreview");
    const lightbox = document.getElementById("lightbox");
    const lightboxImage = document.getElementById("lightboxImage");

    const ALLOWED_IMAGE_TYPES = new Set(["image/jpeg", "image/png", "image/gif", "image/webp"]);
    const ALLOWED_FILE_EXTENSIONS = new Set([
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
        "txt", "zip", "rar", "jpg", "jpeg", "png", "gif", "webp"
    ]);
    const MAX_BYTES = 5 * 1024 * 1024;

    // Local sticker metadata. URLs resolve to files under wwwroot/stickers and
    // the server only relays sticker URLs that point inside /stickers/.
    const STICKERS = [
        { id: "happy", name: "Happy", url: "/stickers/happy.svg" },
        { id: "laugh", name: "Laugh", url: "/stickers/laugh.svg" },
        { id: "love", name: "In Love", url: "/stickers/love.svg" },
        { id: "cool", name: "Cool", url: "/stickers/cool.svg" },
        { id: "sad", name: "Sad", url: "/stickers/sad.svg" },
        { id: "cry", name: "Crying", url: "/stickers/cry.svg" },
        { id: "angry", name: "Angry", url: "/stickers/angry.svg" },
        { id: "wink", name: "Wink", url: "/stickers/wink.svg" },
        { id: "thumbsup", name: "Thumbs Up", url: "/stickers/thumbsup.svg" },
        { id: "cute", name: "Cute", url: "/stickers/cute.png" },
        { id: "frog", name: "Frog", url: "/stickers/frog.png" },
        { id: "heart", name: "Heart", url: "/stickers/heart.png" }
    ];

    let currentUsername = localStorage.getItem("chatUsername") || "";
    let isTyping = false;
    let typingStopTimer = 0;
    const typingUsers = new Map();
    let pendingAttachment = null; // { kind: "image" | "file", dataUrl, name, type, size }

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

    function scrollToBottom() {
        messagesList.scrollTop = messagesList.scrollHeight;
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

    function fileExtension(name) {
        const dot = (name || "").lastIndexOf(".");
        return dot >= 0 ? name.slice(dot + 1).toLowerCase() : "";
    }

    function fileBadge(name) {
        const ext = fileExtension(name);
        return ext ? ext.toUpperCase().slice(0, 4) : "FILE";
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

    function openLightbox(src) {
        lightboxImage.src = src;
        lightbox.hidden = false;
    }

    function closeLightbox() {
        lightbox.hidden = true;
        lightboxImage.removeAttribute("src");
    }

    function renderImageMessage(bubble, message) {
        bubble.classList.add("image-bubble");
        const image = document.createElement("img");
        image.className = "chat-image";
        image.src = message.content;
        image.alt = message.fileName || "Shared image";
        image.loading = "lazy";
        image.addEventListener("click", () => openLightbox(message.content));
        image.addEventListener("load", scrollToBottom);
        bubble.appendChild(image);
    }

    function renderFileMessage(bubble, message) {
        bubble.classList.add("file-bubble");

        const card = document.createElement("div");
        card.className = "file-card";

        const icon = document.createElement("span");
        icon.className = "file-icon";
        icon.textContent = fileBadge(message.fileName);

        const meta = document.createElement("div");
        meta.className = "file-meta";

        const name = document.createElement("span");
        name.className = "file-name";
        name.textContent = message.fileName || "file";

        const size = document.createElement("span");
        size.className = "file-size";
        size.textContent = formatFileSize(message.fileSize || 0);

        meta.append(name, size);

        const download = document.createElement("a");
        download.className = "file-download";
        download.href = message.content;
        download.download = message.fileName || "file";
        download.textContent = "Download";

        card.append(icon, meta, download);
        bubble.appendChild(card);
    }

    function renderStickerMessage(bubble, message) {
        bubble.classList.add("sticker-bubble");
        const image = document.createElement("img");
        image.className = "chat-sticker";
        image.src = message.content;
        image.alt = message.fileName || "sticker";
        image.loading = "lazy";
        image.addEventListener("load", scrollToBottom);
        bubble.appendChild(image);
    }

    function addChatMessage(message) {
        const isMine = (message.user || "").toLowerCase() === currentUsername.toLowerCase();
        const row = document.createElement("article");
        row.className = `message-row ${isMine ? "mine" : "theirs"}`;

        const sender = document.createElement("div");
        sender.className = "message-sender";
        sender.textContent = isMine ? "Me" : message.user;

        const bubble = document.createElement("div");
        bubble.className = "message-bubble";

        switch (message.type) {
            case "image":
                renderImageMessage(bubble, message);
                break;
            case "file":
                renderFileMessage(bubble, message);
                break;
            case "sticker":
                renderStickerMessage(bubble, message);
                break;
            default:
                bubble.textContent = message.content;
                break;
        }

        row.append(sender, bubble);
        messagesList.appendChild(row);
        scrollToBottom();
    }

    function sendChatMessage(payload) {
        return connection.invoke("SendChatMessage", payload);
    }

    async function setUsername(username) {
        currentUsername = username.trim() || "Guest";
        localStorage.setItem("chatUsername", currentUsername);
        await connection.invoke("SetUsername", currentUsername);
    }

    async function ensureUsername() {
        if (!currentUsername) {
            await setUsername(usernameInput.value);
        }
    }

    function insertAtCaret(input, text) {
        const start = input.selectionStart ?? input.value.length;
        const end = input.selectionEnd ?? input.value.length;
        input.value = input.value.slice(0, start) + text + input.value.slice(end);
        const caret = start + text.length;
        input.setSelectionRange(caret, caret);
        input.focus();
    }

    // --- Emoji / sticker panel state (mutually exclusive) ---
    function setEmojiOpen(open) {
        emojiPicker.classList.toggle("open", open);
        emojiButton.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) {
            setStickerOpen(false);
        }
    }

    function setStickerOpen(open) {
        stickerPicker.classList.toggle("open", open);
        stickerButton.setAttribute("aria-expanded", open ? "true" : "false");
        if (open) {
            setEmojiOpen(false);
        }
    }

    function closePanels() {
        setEmojiOpen(false);
        setStickerOpen(false);
    }

    // --- Attachment preview ---
    function clearAttachment() {
        pendingAttachment = null;
        attachInput.value = "";
        attachPreview.innerHTML = "";
        attachPreview.hidden = true;
    }

    function renderAttachPreview(attachment) {
        attachPreview.innerHTML = "";

        let media;
        if (attachment.kind === "image") {
            media = document.createElement("img");
            media.className = "attach-thumb";
            media.src = attachment.dataUrl;
            media.alt = attachment.name;
        } else {
            media = document.createElement("span");
            media.className = "attach-file-icon";
            media.textContent = fileBadge(attachment.name);
        }

        const info = document.createElement("div");
        info.className = "attach-info";

        const name = document.createElement("span");
        name.className = "attach-name";
        name.textContent = attachment.name;
        info.appendChild(name);

        if (attachment.kind === "file") {
            const size = document.createElement("span");
            size.className = "attach-size";
            size.textContent = formatFileSize(attachment.size);
            info.appendChild(size);
        }

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "remove-image-button";
        remove.textContent = "Remove";
        remove.addEventListener("click", clearAttachment);

        attachPreview.append(media, info, remove);
        attachPreview.hidden = false;
    }

    async function sendPendingAttachment() {
        const attachment = pendingAttachment;
        if (!attachment) {
            return;
        }

        if (attachment.kind === "image") {
            await sendChatMessage({
                type: "image",
                content: attachment.dataUrl,
                fileName: attachment.name,
                mimeType: attachment.type
            });
        } else {
            await sendChatMessage({
                type: "file",
                content: attachment.dataUrl,
                fileName: attachment.name,
                mimeType: attachment.type,
                fileSize: attachment.size
            });
        }

        clearAttachment();
    }

    async function sendSticker(sticker) {
        try {
            await ensureUsername();
            await sendChatMessage({ type: "sticker", content: sticker.url, fileName: sticker.name });
            closePanels();
        } catch (error) {
            addSystemMessage("Could not send sticker.");
            console.error(error);
        }
    }

    // --- Build sticker picker grid ---
    STICKERS.forEach(sticker => {
        const option = document.createElement("button");
        option.type = "button";
        option.className = "sticker-option";
        option.setAttribute("role", "menuitem");
        option.title = sticker.name;

        const image = document.createElement("img");
        image.src = sticker.url;
        image.alt = sticker.name;
        image.loading = "lazy";

        option.appendChild(image);
        option.addEventListener("click", () => sendSticker(sticker));
        stickerPicker.appendChild(option);
    });

    // --- SignalR event handlers ---
    connection.on("ReceiveMessage", message => {
        typingUsers.delete(message.user);
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

    // --- Username ---
    nameForm.addEventListener("submit", async event => {
        event.preventDefault();

        try {
            await setUsername(usernameInput.value);
        } catch (error) {
            addSystemMessage("Could not set username.");
            console.error(error);
        }
    });

    // --- Composer submit: sends a pending attachment and/or text ---
    messageForm.addEventListener("submit", async event => {
        event.preventDefault();
        const message = messageInput.value.trim();

        if (!message && !pendingAttachment) {
            return;
        }

        try {
            await ensureUsername();

            if (pendingAttachment) {
                await sendPendingAttachment();
            }

            if (message) {
                await sendChatMessage({ type: "text", content: message });
                messageInput.value = "";
            }

            await sendTypingState(false);
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

    // --- Emoji picker ---
    emojiButton.addEventListener("click", event => {
        event.stopPropagation();
        setEmojiOpen(!emojiPicker.classList.contains("open"));
    });

    emojiPicker.querySelectorAll(".emoji-option").forEach(option => {
        option.addEventListener("click", () => {
            insertAtCaret(messageInput, option.textContent);
        });
    });

    // --- Sticker picker ---
    stickerButton.addEventListener("click", event => {
        event.stopPropagation();
        setStickerOpen(!stickerPicker.classList.contains("open"));
    });

    // --- Close panels on outside click / Escape ---
    document.addEventListener("click", event => {
        const insideEmoji = emojiPicker.contains(event.target) || emojiButton.contains(event.target);
        const insideSticker = stickerPicker.contains(event.target) || stickerButton.contains(event.target);
        if (!insideEmoji && !insideSticker) {
            closePanels();
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closePanels();
            if (!lightbox.hidden) {
                closeLightbox();
            }
        }
    });

    // --- Attachment selection (image -> inline image, otherwise file card) ---
    attachInput.addEventListener("change", async () => {
        const file = attachInput.files[0];

        if (!file) {
            return;
        }

        const isImage = ALLOWED_IMAGE_TYPES.has(file.type);

        if (!isImage && !ALLOWED_FILE_EXTENSIONS.has(fileExtension(file.name))) {
            addSystemMessage("Unsupported file type.");
            attachInput.value = "";
            return;
        }

        if (file.size > MAX_BYTES) {
            addSystemMessage("File must be 5 MB or smaller.");
            attachInput.value = "";
            return;
        }

        try {
            await ensureUsername();
        } catch (error) {
            addSystemMessage("Set a username before sending attachments.");
            console.error(error);
            attachInput.value = "";
            return;
        }

        const reader = new FileReader();
        reader.onload = () => {
            pendingAttachment = {
                kind: isImage ? "image" : "file",
                dataUrl: reader.result,
                name: file.name,
                type: file.type,
                size: file.size
            };
            renderAttachPreview(pendingAttachment);
            messageInput.focus();
        };
        reader.onerror = () => {
            addSystemMessage("Could not read the selected file.");
            attachInput.value = "";
        };
        reader.readAsDataURL(file);
    });

    // --- Lightbox ---
    lightbox.addEventListener("click", closeLightbox);

    // --- Start connection ---
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

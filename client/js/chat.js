(function () {
    // Get references to HTML elements
    const userInput = document.getElementById("userInput");
    const messageInput = document.getElementById("messageInput");
    const sendButton = document.getElementById("sendButton");
    const messagesList = document.getElementById("messagesList");

    // Initialize SignalR connection
    // Ensure this URL matches the MapHub path in Program.cs
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5195/chathub")
        .withAutomaticReconnect() // Enable automatic reconnection
        .build();

    // --- Event Handlers for SignalR Connection ---

    // Handle incoming messages from the hub
    connection.on("ReceiveMessage", function (user, message, timestamp) {
        console.log(`Received message: [${timestamp}] ${user}: ${message}`);
        addMessageToChat(user, message, timestamp, false); // Add as 'other' user
    });

    // Handle system messages (e.g., errors from the server)
    connection.on("ReceiveSystemMessage", function (message) {
        console.warn(`System Message: ${message}`);
        addSystemMessageToChat(message);
    });

    // Handle reconnection logic
    connection.onreconnecting(error => {
        console.warn(`Connection lost, attempting to reconnect... ${error}`);
        addSystemMessageToChat("Connection lost. Attempting to reconnect...");
        sendButton.disabled = true; // Disable send button during reconnection
    });

    connection.onreconnected(connectionId => {
        console.log(`Connection reestablished. Connection ID: ${connectionId}`);
        addSystemMessageToChat("Connection reestablished!");
        sendButton.disabled = false; // Re-enable send button
    });

    connection.onclose(error => {
        console.error(`Connection closed. ${error}`);
        addSystemMessageToChat("Connection closed. Please refresh the page.");
        sendButton.disabled = true; // Disable send button if connection is permanently closed
    });

    // Start the SignalR connection
    async function startConnection() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
            sendButton.disabled = false; // Enable send button once connected
            addSystemMessageToChat("Connected to chat!");
            // Set a default username if not already set
            if (!userInput.value) {
                userInput.value = `User-${Math.floor(Math.random() * 1000)}`;
            }
        } catch (err) {
            console.error("SignalR Connection Error: ", err);
            sendButton.disabled = true; // Keep send button disabled on initial failure
            addSystemMessageToChat("Failed to connect to chat. Retrying...");
            // Implement a retry mechanism if initial connection fails
            setTimeout(startConnection, 5000); // Retry after 5 seconds
        }
    }

    // --- UI Event Listeners ---

    sendButton.addEventListener("click", sendMessage);
    messageInput.addEventListener("keypress", function (e) {
        if (e.key === "Enter") {
            sendMessage();
        }
    });

    // --- Helper Functions ---

    function sendMessage() {
        const user = userInput.value.trim();
        const message = messageInput.value.trim();

        if (!user) {
            alert("Please enter your name.");
            userInput.focus();
            return;
        }
        if (!message) {
            alert("Please enter a message.");
            messageInput.focus();
            return;
        }

        // Send message to the hub
        // The hub will then broadcast it to all clients, including the sender
        connection.invoke("SendMessage", user, message)
            .then(() => {
                console.log("Message sent to hub.");
                // Clear the message input after sending
                messageInput.value = "";
                messageInput.focus();
            })
            .catch(err => {
                console.error("Error invoking SendMessage: ", err);
                addSystemMessageToChat(`Error sending message: ${err.message}`);
            });
    }

    function addMessageToChat(user, message, timestamp, isSelf) {
        const messageItem = document.createElement("div");
        messageItem.classList.add("message-item", isSelf ? "self" : "other");

        const messageBubble = document.createElement("div");
        messageBubble.classList.add("message-bubble");
        messageBubble.textContent = message;

        const messageInfo = document.createElement("div");
        messageInfo.classList.add("message-info");
        messageInfo.textContent = `${user} - ${timestamp}`;

        messageItem.appendChild(messageBubble);
        messageItem.appendChild(messageInfo);

        // Add to the top of the list (because of flex-direction: column-reverse)
        messagesList.prepend(messageItem);
    }

    function addSystemMessageToChat(message) {
        const systemMessageItem = document.createElement("div");
        systemMessageItem.classList.add("system-message");
        systemMessageItem.textContent = message;
        messagesList.prepend(systemMessageItem);
    }

    // Start the connection when the script loads
    startConnection();
})();

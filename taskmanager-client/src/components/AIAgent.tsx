import { useState } from 'react';
import axios from 'axios';

const AGENT_API_URL = 'http://localhost:5183/api/ClaudeAgent/chat';

interface Massage {
    role: 'user' | 'assistant';
    content: string;
}

export default function AIAgent({ onAction }: { onAction: () => void }) {
    const [messages, setMessages] = useState<Massage[]>([]);
    const [input, setInput] = useState('');
    const [loading, setLoading] = useState(false);

    const [conversationId, setConversationId] = useState<string | null>(null);

    const sendMessage = async () => {
        if (!input.trim()) return;
        setLoading(true);

        const userMessage = input
        setMessages(prev => [...prev, { role: 'user', content: userMessage }]);
        setInput('');

        try {
            const response = await axios.post(AGENT_API_URL, { message: userMessage, conversationId : conversationId });
            const assistantMessage = response.data.reply;

            if( response.data.conversationId) {
                setConversationId(response.data.conversationId);
            }
            setMessages(prev => [...prev, { role: 'assistant', content: assistantMessage }]);
            onAction();
        } catch (error) {
            console.error('Error sending message:', error);
        } finally {
            setLoading(false);
        }
    };
    
    return (
        <div className="bg-white rounded-lg shadow p-4 mb-6">
            <h2 className="text-lg font-semibold text-gray-700 mb-3">🤖 AI Assistant</h2>
            <div className="max-h-60 overflow-y-auto mb-3">
                {messages.length === 0 &&(
                    <p className="text-gray-500 text-sm">Try: "Create a task to review the code" or "Show me a summary</p>
                ) }
                {messages.map((msg, index) => (
                    <div key={index} className={`mb-2 p-2 rounded ${msg.role === 'user' ? 'bg-blue-100 text-blue-800 self-end' : 'bg-gray-100 text-gray-800 self-start'}`}>
                        <span className="font-medium">
                        {msg.role === 'user' ? 'You: ' : '🤖 '}
                        </span>
                        {msg.content}
                    </div>
                ))}
                {loading && (
                    <div className="bg-gray-50 text-gray-500 text-sm p-2 rounded mr-8">
                        <span className="font-medium">🤖 </span>Typing...
                    </div>
                )}
            </div>
            <div className="flex gap-2">
                <input
                    type="text"
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    onKeyDown={e => { if (e.key === 'Enter') sendMessage(); }}
                    placeholder="Ask the AI to manage your tasks..."
                    className="flex-1 border rounded p-2 text-sm"
                />
                <button

                    onClick={sendMessage}
                    disabled={loading}
                    className="bg-blue-600 text-white px-4 py-2 rounded text-sm hover:bg-blue-700 disabled:opacity-50"
                >
                    Send
                </button>
            </div>
        </div>
    );
}



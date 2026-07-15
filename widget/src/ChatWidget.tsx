import { useEffect, useRef, useState, KeyboardEvent } from 'react';
import { MessageCircle, X, Send, Wifi, WifiOff, Loader } from 'lucide-react';
import { useWidgetSignalR } from './useWidgetSignalR';

interface WidgetConfig {
    tenantId: string;
    widgetIconUrl: string | null;
    widgetPrimaryColor: string;
    widgetSecondaryColor: string;
    widgetWelcomeMessage: string;
}

interface ChatWidgetProps {
    tenantId: string;
    apiUrl: string;
}

export function ChatWidget({ tenantId, apiUrl }: ChatWidgetProps) {
    const [isOpen, setIsOpen] = useState(false);
    const [config, setConfig] = useState<WidgetConfig | null>(null);
    const [inputText, setInputText] = useState('');
    const messagesEndRef = useRef<HTMLDivElement>(null);

    // Only connect once the user has opened the widget
    const { connectionState, messages, sendMessage } = useWidgetSignalR({
        apiUrl,
        tenantId,
        enabled: isOpen,
    });

    // ── Fetch config once ──────────────────────────────────────────────────────
    useEffect(() => {
        const isRealTenant = tenantId && tenantId !== '00000000-0000-0000-0000-000000000000';

        if (isRealTenant) {
            fetch(`${apiUrl}/api/v1/widget/config/${tenantId}`)
                .then(r => r.ok ? r.json() : Promise.reject(r.status))
                .then((data: WidgetConfig) => setConfig(data))
                .catch(err => {
                    console.error('[NexConvo Widget] Could not fetch config:', err);
                    // Fall back to defaults so the widget is still usable
                    setConfig({ tenantId, widgetIconUrl: null, widgetPrimaryColor: '#0F172A', widgetSecondaryColor: '#3B82F6', widgetWelcomeMessage: 'Hi! How can I help?' });
                });
        } else {
            setConfig({ tenantId: 'mock', widgetIconUrl: null, widgetPrimaryColor: '#0F172A', widgetSecondaryColor: '#3B82F6', widgetWelcomeMessage: 'Hi there! How can I help you today?' });
        }
    }, [tenantId, apiUrl]);

    // ── Auto-scroll messages ───────────────────────────────────────────────────
    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    // ── Guards ─────────────────────────────────────────────────────────────────
    if (!config) return null;

    const primaryColor  = config.widgetPrimaryColor;
    const isConnected   = connectionState === 'connected';
    const isConnecting  = connectionState === 'connecting';
    const isAiTyping    = messages.some(m => m.isStreaming);
    const canSend       = isConnected && inputText.trim().length > 0 && !isAiTyping;

    // ── Handlers ───────────────────────────────────────────────────────────────
    const handleSend = async () => {
        if (!canSend) return;
        const text = inputText.trim();
        setInputText('');
        await sendMessage(text);
    };

    const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            void handleSend();
        }
    };

    const toggleOpen = () => setIsOpen(prev => !prev);

    // ── Render ─────────────────────────────────────────────────────────────────
    return (
        <div className="nc-widget-container">
            {/* ── Chat Window ──────────────────────────────────────────────────── */}
            {isOpen && (
                <div className="nc-chat-window" role="dialog" aria-label="Chat with us" aria-modal="true">
                    {/* Header */}
                    <div className="nc-chat-header" style={{ backgroundColor: primaryColor }}>
                        <div className="nc-chat-header-info">
                            {config.widgetIconUrl ? (
                                <img src={config.widgetIconUrl} alt="Logo" className="nc-chat-logo" />
                            ) : (
                                <MessageCircle size={22} color="white" />
                            )}
                            <span className="nc-chat-title">Chat with us</span>
                        </div>
                        <div className="nc-header-right">
                            {/* Connection indicator */}
                            <span className="nc-connection-badge" title={connectionState}>
                                {isConnecting ? (
                                    <Loader size={14} color="rgba(255,255,255,0.7)" className="nc-spin" />
                                ) : isConnected ? (
                                    <Wifi size={14} color="rgba(255,255,255,0.7)" />
                                ) : (
                                    <WifiOff size={14} color="rgba(255,255,255,0.5)" />
                                )}
                            </span>
                            <button className="nc-icon-btn" onClick={toggleOpen} aria-label="Close chat">
                                <X size={20} color="white" />
                            </button>
                        </div>
                    </div>

                    {/* Messages */}
                    <div className="nc-chat-messages" role="log" aria-live="polite">
                        {/* Welcome message (static, always shown first) */}
                        <div className="nc-message nc-message-ai">
                            <span className="nc-message-bubble nc-bubble-ai">
                                {config.widgetWelcomeMessage}
                            </span>
                        </div>

                        {messages.map(msg => (
                            <div
                                key={msg.id}
                                className={`nc-message ${msg.role === 'user' ? 'nc-message-user' : 'nc-message-ai'}`}
                            >
                                <span
                                    className={`nc-message-bubble ${msg.role === 'user' ? 'nc-bubble-user' : 'nc-bubble-ai'}`}
                                    style={msg.role === 'user' ? { backgroundColor: primaryColor } : undefined}
                                >
                                    {msg.text}
                                    {msg.isStreaming && <span className="nc-cursor" aria-hidden="true">▊</span>}
                                </span>
                            </div>
                        ))}

                        {/* Connecting / offline notice */}
                        {isConnecting && messages.length === 0 && (
                            <div className="nc-status-notice">
                                <Loader size={14} className="nc-spin" />
                                Connecting…
                            </div>
                        )}
                        {connectionState === 'disconnected' || connectionState === 'error' ? (
                            <div className="nc-status-notice nc-status-error">
                                Connection lost. Reconnecting…
                            </div>
                        ) : null}

                        <div ref={messagesEndRef} />
                    </div>

                    {/* Input */}
                    <div className="nc-chat-input-area">
                        <input
                            type="text"
                            className="nc-chat-input"
                            placeholder={isConnected ? 'Type a message…' : 'Connecting…'}
                            value={inputText}
                            onChange={e => setInputText(e.target.value)}
                            onKeyDown={handleKeyDown}
                            disabled={!isConnected || isAiTyping}
                            aria-label="Message input"
                            maxLength={4000}
                        />
                        <button
                            className="nc-send-btn"
                            style={{ color: canSend ? primaryColor : undefined }}
                            onClick={() => void handleSend()}
                            disabled={!canSend}
                            aria-label="Send message"
                        >
                            <Send size={20} />
                        </button>
                    </div>
                </div>
            )}

            {/* ── Launcher Button ───────────────────────────────────────────────── */}
            <button
                className="nc-launcher-btn"
                style={{ backgroundColor: primaryColor }}
                onClick={toggleOpen}
                aria-label={isOpen ? 'Close chat' : 'Open chat'}
                aria-expanded={isOpen}
            >
                {isOpen ? (
                    <X size={28} color="white" />
                ) : (
                    config.widgetIconUrl ? (
                        <img src={config.widgetIconUrl} alt="Chat" className="nc-launcher-icon" />
                    ) : (
                        <MessageCircle size={28} color="white" />
                    )
                )}
            </button>
        </div>
    );
}

import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';

type ConnectionState = 'connecting' | 'connected' | 'disconnected' | 'error';

export interface ChatMessage {
    id: string;
    role: 'user' | 'ai';
    text: string;
    isStreaming?: boolean;
}

interface UseWidgetSignalROptions {
    apiUrl: string;
    token: string;
    enabled: boolean;
}

interface UseWidgetSignalRResult {
    connectionState: ConnectionState;
    messages: ChatMessage[];
    sendMessage: (text: string) => Promise<void>;
}

export function useWidgetSignalR({ apiUrl, token, enabled }: UseWidgetSignalROptions): UseWidgetSignalRResult {
    const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const streamingIdRef = useRef<string | null>(null);

    const appendToken = useCallback((token: string) => {
        setMessages(prev => {
            if (!streamingIdRef.current) return prev;
            return prev.map(m =>
                m.id === streamingIdRef.current
                    ? { ...m, text: m.text + token }
                    : m
            );
        });
    }, []);

    const finalizeStreaming = useCallback(() => {
        setMessages(prev =>
            prev.map(m =>
                m.id === streamingIdRef.current ? { ...m, isStreaming: false } : m
            )
        );
        streamingIdRef.current = null;
    }, []);

    const appendError = useCallback((errorMsg: string) => {
        finalizeStreaming();
        setMessages(prev => [...prev, {
            id: crypto.randomUUID(),
            role: 'ai',
            text: `⚠️ ${errorMsg}`,
            isStreaming: false,
        }]);
    }, [finalizeStreaming]);

    // ── Real SignalR Connection ──────────────────────────────────────────────
    useEffect(() => {
        if (!enabled) {
            setConnectionState('disconnected');
            return;
        }

        // Prefer WebSockets but allow the negotiate handshake so SignalR can fall back to
        // long-polling where WebSockets are blocked (audit M6 — matches the documented design).
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiUrl}/hubs/widget?token=${encodeURIComponent(token)}`, {
                transport:
                    signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connection.on('receiveToken', appendToken);
        connection.on('receiveCompleted', finalizeStreaming);
        connection.on('receiveError', appendError);

        connection.onreconnecting(() => setConnectionState('connecting'));
        connection.onreconnected(() => setConnectionState('connected'));
        connection.onclose(() => setConnectionState('disconnected'));

        connectionRef.current = connection;

        setConnectionState('connecting');
        connection.start()
            .then(() => setConnectionState('connected'))
            .catch(err => {
                console.error('[NexConvo Widget] SignalR connection failed:', err);
                setConnectionState('error');
            });

        return () => {
            connection.off('receiveToken', appendToken);
            connection.off('receiveCompleted', finalizeStreaming);
            connection.off('receiveError', appendError);
            connection.stop();
            connectionRef.current = null;
        };
    }, [enabled, token, apiUrl, appendToken, finalizeStreaming, appendError]);

    // ── Send Message (handles both real hub and local mock simulation) ─────────
    const sendMessage = useCallback(async (text: string) => {
        // Add user message
        const userMsg: ChatMessage = {
            id: crypto.randomUUID(),
            role: 'user',
            text,
        };

        // Add AI placeholder for streaming
        const aiMsgId = crypto.randomUUID();
        streamingIdRef.current = aiMsgId;
        const aiMsg: ChatMessage = {
            id: aiMsgId,
            role: 'ai',
            text: '',
            isStreaming: true,
        };

        setMessages(prev => [...prev, userMsg, aiMsg]);

        const conn = connectionRef.current;
        if (!conn || conn.state !== signalR.HubConnectionState.Connected) {
            appendError('Not connected. Please try again in a moment.');
            return;
        }

        try {
            await conn.invoke('SendMessageAsync', text);
        } catch (err) {
            console.error('[NexConvo Widget] Failed to send message:', err);
            appendError('Failed to send message. Please try again.');
        }
    }, [appendError]);

    return { connectionState, messages, sendMessage };
}

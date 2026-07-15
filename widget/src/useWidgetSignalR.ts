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
    tenantId: string;
    enabled: boolean;
}

interface UseWidgetSignalRResult {
    connectionState: ConnectionState;
    messages: ChatMessage[];
    sendMessage: (text: string) => Promise<void>;
}

export function useWidgetSignalR({ apiUrl, tenantId, enabled }: UseWidgetSignalROptions): UseWidgetSignalRResult {
    const isMock = !tenantId || tenantId === '00000000-0000-0000-0000-000000000000' || tenantId === 'mock';
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

        if (isMock) {
            setConnectionState('connected');
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiUrl}/hubs/widget?tenantId=${tenantId}`, {
                skipNegotiation: true,
                transport: signalR.HttpTransportType.WebSockets,
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
    }, [enabled, tenantId, apiUrl, appendToken, finalizeStreaming, appendError, isMock]);

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

        if (isMock) {
            // Simulate AI token streaming response locally
            const responseText = `This is a mock streamed response to test the widget UI. You said: "${text}". SignalR is not connected because you are using the local developer UUID.`;
            const words = responseText.split(' ');
            let index = 0;

            const interval = setInterval(() => {
                if (index < words.length) {
                    appendToken(words[index] + ' ');
                    index++;
                } else {
                    clearInterval(interval);
                    finalizeStreaming();
                }
            }, 80);
            return;
        }

        const conn = connectionRef.current;
        if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;

        try {
            await conn.invoke('SendMessageAsync', text);
        } catch (err) {
            console.error('[NexConvo Widget] Failed to send message:', err);
            appendError('Failed to send message. Please try again.');
        }
    }, [appendError, isMock, appendToken, finalizeStreaming]);

    return { connectionState, messages, sendMessage };
}

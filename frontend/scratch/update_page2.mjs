import fs from 'fs';

const filePath = 'd:/Resources/Projects/NexConvo/frontend/src/app/(dashboard)/dashboard/chat/playground/page.tsx';
let content = fs.readFileSync(filePath, 'utf-8');

// Replace `streamText` and `sendMessage`
const startIdx = content.indexOf('  // Character-by-character streaming simulation');
const endIdx = content.indexOf('  return (', startIdx);

if (startIdx === -1 || endIdx === -1) {
  console.log('Could not find streamText or return');
  process.exit(1);
}

const replacement = `
  const sendMessage = useCallback(() => {
    const body = inputText.trim();
    if (!body || isStreaming) return;
    setInputText('');

    const userMsg: PlaygroundMessage = {
      id: nextId(),
      role: 'user',
      body,
      sentAt: new Date().toISOString(),
    };

    const aiId = nextId();
    const aiStreamMsg: PlaygroundMessage = {
      id: aiId,
      role: 'ai',
      body: '',
      isStreaming: true,
      sentAt: new Date().toISOString(),
    };

    if (compareMode) {
      setCompareLeftMessages((prev) => [...prev, userMsg, aiStreamMsg]);
      const rightAiId = nextId();
      setCompareRightMessages((prev) => [
        ...prev,
        userMsg,
        { ...aiStreamMsg, id: rightAiId },
      ]);
      setIsStreaming(false); // Compare mode SignalR not fully implemented in this script
      return;
    }

    setMessages((prev) => [...prev, userMsg, aiStreamMsg]);

    if (!connection || connection.state !== 'Connected') {
      console.warn('SignalR not connected');
      setIsStreaming(false);
      return;
    }

    setIsStreaming(true);

    const onToken = (token: string) => {
      setMessages((prev) =>
        prev.map((m) =>
          m.id === aiId
            ? { ...m, body: m.body + token }
            : m
        )
      );
    };

    const onDebug = (debugData: DebugData) => {
      setMessages((prev) =>
        prev.map((m) =>
          m.id === aiId ? { ...m, debugData } : m
        )
      );
      setCurrentDebugData(debugData);
      setSelectedDebugId(aiId);
    };

    const onCompleted = () => {
      setMessages((prev) =>
        prev.map((m) =>
          m.id === aiId ? { ...m, isStreaming: false } : m
        )
      );
      setIsStreaming(false);
      connection.off('ReceiveToken', onToken);
      connection.off('ReceiveDebugData', onDebug);
      connection.off('ReceiveCompleted', onCompleted);
    };

    connection.on('ReceiveToken', onToken);
    connection.on('ReceiveDebugData', onDebug);
    connection.on('ReceiveCompleted', onCompleted);

    connection.invoke('ExecuteScenarioAsync', {
      UserMessage: body,
      ProviderId: leftProvider,
      ModelId: leftModel,
      SystemPrompt: sysPromptOn ? sysPrompt : null
    }).catch(err => {
      console.error('SignalR Invoke Error:', err);
      setIsStreaming(false);
      connection.off('ReceiveToken', onToken);
      connection.off('ReceiveDebugData', onDebug);
      connection.off('ReceiveCompleted', onCompleted);
    });
  }, [inputText, isStreaming, compareMode, connection, leftProvider, leftModel, sysPromptOn, sysPrompt]);

`;

content = content.substring(0, startIdx) + replacement + content.substring(endIdx);
fs.writeFileSync(filePath, content, 'utf-8');
console.log('Update script 2 done.');

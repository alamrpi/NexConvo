// SSR-safe: all dates are ISO strings — never construct Date at module level.
// SLA anchors are fixed offsets from 2026-06-30T10:00:00Z.

// ─── Types ──────────────────────────────────────────────────────────────────

export type ConversationState =
  | 'AiHandling'
  | 'PendingHuman'
  | 'HumanHandling'
  | 'Resolved'
  | 'Closed';

export type Channel = 'whatsapp' | 'facebook' | 'instagram' | 'telegram' | 'web';

export type SenderRole = 'Contact' | 'Ai' | 'Agent' | 'System';

export type DeliveryStatus =
  | 'Sent'
  | 'Delivered'
  | 'Read'
  | 'Failed'
  | 'PermanentlyFailed';

export type AttachmentType = 'Image' | 'Document' | 'Audio' | 'Location';

export interface Attachment {
  type: AttachmentType;
  url: string;
  mimeType: string;
  fileSizeBytes?: number;
  caption?: string;
}

export interface Message {
  id: string;
  conversationId: string;
  senderRole: SenderRole;
  senderName?: string;
  body: string;
  sentAt: string;
  deliveryStatus?: DeliveryStatus;
  confidence?: number;
  isStreaming?: boolean;
  attachments?: Attachment[];
}

export interface Conversation {
  id: string;
  state: ConversationState;
  channel: Channel;
  contactName?: string;
  contactHandle: string;
  lastMessagePreview: string;
  lastMessageAt: string;
  lastMessageFromAi: boolean;
  unreadCount: number;
  assignedAgentName?: string;
  slaExpiresAt?: string;
  tags: string[];
  messages: Message[];
}

// ─── Agents & Workspace ─────────────────────────────────────────────────────

export const MOCK_AGENT = {
  id: 'agent-1',
  name: 'Md. Alam Hossain',
  initials: 'MA',
};

export const MOCK_WORKSPACE = {
  name: 'Dhaka Retail Co.',
  logoInitial: 'D',
};

// ─── Conversations (18) ──────────────────────────────────────────────────────
// 5 AiHandling | 3 PendingHuman | 4 HumanHandling | 4 Resolved | 2 Closed

export const MOCK_CONVERSATIONS: Conversation[] = [
  // ── AiHandling × 5 ────────────────────────────────────────────────────────
  {
    id: 'conv-001',
    state: 'AiHandling',
    channel: 'whatsapp',
    contactName: 'Razia Begum',
    contactHandle: '+8801711234567',
    lastMessagePreview: 'আমার অর্ডারটি কোথায়? এটা ৩ দিন ধরে আসছে না।',
    lastMessageAt: '2026-06-30T09:55:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['order', 'delivery'],
    messages: [
      {
        id: 'msg-001-1',
        conversationId: 'conv-001',
        senderRole: 'Contact',
        senderName: 'Razia Begum',
        body: 'আমার অর্ডারটি কোথায়? এটা ৩ দিন ধরে আসছে না।',
        sentAt: '2026-06-30T09:50:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-001-2',
        conversationId: 'conv-001',
        senderRole: 'Ai',
        body: 'আপনার অর্ডার নম্বরটি কি একটু শেয়ার করবেন? আমি এখনই ট্র্যাক করে দেখছি।',
        sentAt: '2026-06-30T09:51:00.000Z',
        confidence: 0.87,
      },
      {
        id: 'msg-001-3',
        conversationId: 'conv-001',
        senderRole: 'Contact',
        senderName: 'Razia Begum',
        body: 'অর্ডার নম্বর: DRC-2026-8821',
        sentAt: '2026-06-30T09:53:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-001-4',
        conversationId: 'conv-001',
        senderRole: 'Ai',
        body: 'আপনার অর্ডার DRC-2026-8821 এখন ঢাকা ডেলিভারি হাবে আছে এবং আজই পৌঁছে যাবে। ধন্যবাদ আপনার ধৈর্যের জন্য।',
        sentAt: '2026-06-30T09:55:00.000Z',
        confidence: 0.92,
      },
    ],
  },
  {
    id: 'conv-002',
    state: 'AiHandling',
    channel: 'facebook',
    contactName: 'Tanvir Islam',
    contactHandle: 'tanvir.islam.fb',
    lastMessagePreview: 'What are the return policies for electronics?',
    lastMessageAt: '2026-06-30T09:48:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 2,
    tags: ['returns', 'electronics'],
    messages: [
      {
        id: 'msg-002-1',
        conversationId: 'conv-002',
        senderRole: 'Contact',
        senderName: 'Tanvir Islam',
        body: 'What are the return policies for electronics?',
        sentAt: '2026-06-30T09:45:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-002-2',
        conversationId: 'conv-002',
        senderRole: 'Ai',
        body: 'Electronics can be returned within 7 days of delivery if unopened, or within 3 days if opened but defective. You will need your order receipt. Would you like me to start a return request?',
        sentAt: '2026-06-30T09:48:00.000Z',
        confidence: 0.79,
      },
    ],
  },
  {
    id: 'conv-003',
    state: 'AiHandling',
    channel: 'instagram',
    contactName: 'Nusrat Jahan',
    contactHandle: '@nusrat.jahan.ig',
    lastMessagePreview: 'Do you have the saree in blue color?',
    lastMessageAt: '2026-06-30T09:40:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['product-inquiry', 'fashion'],
    messages: [
      {
        id: 'msg-003-1',
        conversationId: 'conv-003',
        senderRole: 'Contact',
        senderName: 'Nusrat Jahan',
        body: 'Do you have the Jamdani saree from your last post in blue color?',
        sentAt: '2026-06-30T09:38:00.000Z',
        deliveryStatus: 'Read',
        attachments: [
          {
            type: 'Image',
            url: 'https://placehold.co/400x400/6366f1/white?text=Saree',
            mimeType: 'image/jpeg',
            fileSizeBytes: 128000,
            caption: 'Reference image',
          },
        ],
      },
      {
        id: 'msg-003-2',
        conversationId: 'conv-003',
        senderRole: 'Ai',
        body: 'Yes, the Jamdani saree (SKU: JAM-042) is available in blue, red, and green. The blue variant is currently in stock. Would you like to place an order or check the price?',
        sentAt: '2026-06-30T09:40:00.000Z',
        confidence: 0.83,
      },
    ],
  },
  {
    id: 'conv-004',
    state: 'AiHandling',
    channel: 'telegram',
    contactName: 'Karim Uddin',
    contactHandle: '@karimuddin_tg',
    lastMessagePreview: 'আমার কাছে একটা বাল্ক অর্ডার করার দরকার আছে।',
    lastMessageAt: '2026-06-30T09:35:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 1,
    tags: ['bulk', 'b2b'],
    messages: [
      {
        id: 'msg-004-1',
        conversationId: 'conv-004',
        senderRole: 'Contact',
        senderName: 'Karim Uddin',
        body: 'আমার কাছে একটা বাল্ক অর্ডার করার দরকার আছে। ৫০০ পিস শার্ট লাগবে।',
        sentAt: '2026-06-30T09:35:00.000Z',
        deliveryStatus: 'Delivered',
      },
      {
        id: 'msg-004-2',
        conversationId: 'conv-004',
        senderRole: 'Ai',
        body: 'বাল্ক অর্ডারের জন্য আমাদের B2B টিমের সাথে কথা বলা দরকার। আপনার ব্যবসার নাম এবং যোগাযোগ নম্বর দিলে তারা ২৪ ঘণ্টার মধ্যে যোগাযোগ করবে।',
        sentAt: '2026-06-30T09:36:00.000Z',
        confidence: 0.71,
      },
    ],
  },
  {
    id: 'conv-005',
    state: 'AiHandling',
    channel: 'web',
    contactName: 'Sarah Ahmed',
    contactHandle: 'sarah.ahmed@example.com',
    lastMessagePreview: 'Can I track my order in real-time?',
    lastMessageAt: '2026-06-30T09:30:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['tracking'],
    messages: [
      {
        id: 'msg-005-1',
        conversationId: 'conv-005',
        senderRole: 'Contact',
        senderName: 'Sarah Ahmed',
        body: 'Can I track my order in real-time?',
        sentAt: '2026-06-30T09:28:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-005-2',
        conversationId: 'conv-005',
        senderRole: 'Ai',
        body: "Yes! You can track your order in real-time from your account dashboard under 'My Orders'. You'll also receive SMS and email updates at each stage. Would you like me to send you a tracking link right now?",
        sentAt: '2026-06-30T09:30:00.000Z',
        confidence: 0.95,
      },
    ],
  },

  // ── PendingHuman × 3 (slaExpiresAt = anchor + 2–8 min) ───────────────────
  {
    id: 'conv-006',
    state: 'PendingHuman',
    channel: 'whatsapp',
    contactName: 'Fatema Khatun',
    contactHandle: '+8801899876543',
    lastMessagePreview: 'I need to speak to a manager about my refund!',
    lastMessageAt: '2026-06-30T09:58:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 3,
    slaExpiresAt: '2026-06-30T10:02:00.000Z',
    tags: ['refund', 'escalation', 'urgent'],
    messages: [
      {
        id: 'msg-006-1',
        conversationId: 'conv-006',
        senderRole: 'Contact',
        senderName: 'Fatema Khatun',
        body: 'I ordered 2 weeks ago and still no refund! This is unacceptable.',
        sentAt: '2026-06-30T09:52:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-006-2',
        conversationId: 'conv-006',
        senderRole: 'Ai',
        body: 'I sincerely apologize for the delay. Let me look into your refund status right away.',
        sentAt: '2026-06-30T09:53:00.000Z',
        confidence: 0.45,
      },
      {
        id: 'msg-006-3',
        conversationId: 'conv-006',
        senderRole: 'Contact',
        senderName: 'Fatema Khatun',
        body: 'I need to speak to a manager about my refund!',
        sentAt: '2026-06-30T09:57:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-006-4',
        conversationId: 'conv-006',
        senderRole: 'System',
        body: 'AI escalated · low confidence · 09:57',
        sentAt: '2026-06-30T09:57:30.000Z',
      },
      {
        id: 'msg-006-5',
        conversationId: 'conv-006',
        senderRole: 'Ai',
        body: "I completely understand your frustration. I'm connecting you with a manager now. Someone will be with you in a moment.",
        sentAt: '2026-06-30T09:58:00.000Z',
        confidence: 0.52,
      },
    ],
  },
  {
    id: 'conv-007',
    state: 'PendingHuman',
    channel: 'instagram',
    contactName: 'Rakib Hasan',
    contactHandle: '@rakibhasan_ig',
    lastMessagePreview: 'The AI could not help me with my custom order.',
    lastMessageAt: '2026-06-30T09:56:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 1,
    slaExpiresAt: '2026-06-30T10:05:00.000Z',
    tags: ['custom-order'],
    messages: [
      {
        id: 'msg-007-1',
        conversationId: 'conv-007',
        senderRole: 'Contact',
        senderName: 'Rakib Hasan',
        body: 'I want a customized polo shirt with my company logo. Can you help?',
        sentAt: '2026-06-30T09:50:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-007-2',
        conversationId: 'conv-007',
        senderRole: 'Ai',
        body: "I'm not sure I can handle custom branding orders. Let me get a human agent to assist you.",
        sentAt: '2026-06-30T09:52:00.000Z',
        confidence: 0.48,
      },
      {
        id: 'msg-007-3',
        conversationId: 'conv-007',
        senderRole: 'System',
        body: 'AI escalated · low confidence · 09:52',
        sentAt: '2026-06-30T09:52:30.000Z',
      },
      {
        id: 'msg-007-4',
        conversationId: 'conv-007',
        senderRole: 'Contact',
        senderName: 'Rakib Hasan',
        body: 'The AI could not help me with my custom order.',
        sentAt: '2026-06-30T09:56:00.000Z',
        deliveryStatus: 'Delivered',
      },
    ],
  },
  {
    id: 'conv-008',
    state: 'PendingHuman',
    channel: 'web',
    contactName: 'Mehnaz Parvin',
    contactHandle: 'mehnaz@retailcorp.bd',
    lastMessagePreview: 'Please connect me with your sales team.',
    lastMessageAt: '2026-06-30T09:52:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    slaExpiresAt: '2026-06-30T10:08:00.000Z',
    tags: ['b2b', 'sales'],
    messages: [
      {
        id: 'msg-008-1',
        conversationId: 'conv-008',
        senderRole: 'Contact',
        senderName: 'Mehnaz Parvin',
        body: 'We need enterprise pricing for 1,000+ units per month.',
        sentAt: '2026-06-30T09:49:00.000Z',
        deliveryStatus: 'Read',
        attachments: [
          {
            type: 'Document',
            url: 'https://placehold.co/doc',
            mimeType: 'application/pdf',
            fileSizeBytes: 245000,
            caption: 'Product requirements.pdf',
          },
        ],
      },
      {
        id: 'msg-008-2',
        conversationId: 'conv-008',
        senderRole: 'Ai',
        body: 'Enterprise pricing requires a human sales representative. I am escalating this now.',
        sentAt: '2026-06-30T09:50:00.000Z',
        confidence: 0.63,
      },
      {
        id: 'msg-008-3',
        conversationId: 'conv-008',
        senderRole: 'System',
        body: 'AI escalated · explicit request · 09:50',
        sentAt: '2026-06-30T09:50:30.000Z',
      },
      {
        id: 'msg-008-4',
        conversationId: 'conv-008',
        senderRole: 'Contact',
        senderName: 'Mehnaz Parvin',
        body: 'Please connect me with your sales team.',
        sentAt: '2026-06-30T09:52:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },

  // ── HumanHandling × 4 (2 mine, 2 Fatima Khanam) ─────────────────────────
  {
    id: 'conv-009',
    state: 'HumanHandling',
    channel: 'whatsapp',
    contactName: 'Alamgir Hossain',
    contactHandle: '+8801755443322',
    lastMessagePreview: 'Yes, I will pay via bKash.',
    lastMessageAt: '2026-06-30T09:45:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 0,
    assignedAgentName: MOCK_AGENT.name,
    tags: ['payment', 'bkash'],
    messages: [
      {
        id: 'msg-009-1',
        conversationId: 'conv-009',
        senderRole: 'Contact',
        senderName: 'Alamgir Hossain',
        body: 'I want to pay for my order using bKash.',
        sentAt: '2026-06-30T09:40:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-009-2',
        conversationId: 'conv-009',
        senderRole: 'Agent',
        senderName: MOCK_AGENT.name,
        body: 'Sure! I can send you the bKash payment link. Your total is ৳2,450. Shall I proceed?',
        sentAt: '2026-06-30T09:42:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-009-3',
        conversationId: 'conv-009',
        senderRole: 'Contact',
        senderName: 'Alamgir Hossain',
        body: 'Yes, I will pay via bKash.',
        sentAt: '2026-06-30T09:45:00.000Z',
        deliveryStatus: 'Delivered',
      },
    ],
  },
  {
    id: 'conv-010',
    state: 'HumanHandling',
    channel: 'facebook',
    contactName: 'Shirin Akter',
    contactHandle: 'shirin.akter.fb',
    lastMessagePreview: 'OK, I understand. Thank you for explaining.',
    lastMessageAt: '2026-06-30T09:38:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 0,
    assignedAgentName: MOCK_AGENT.name,
    tags: ['warranty', 'explanation'],
    messages: [
      {
        id: 'msg-010-1',
        conversationId: 'conv-010',
        senderRole: 'Contact',
        senderName: 'Shirin Akter',
        body: 'My phone broke after 6 months. Is it still under warranty?',
        sentAt: '2026-06-30T09:30:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-010-2',
        conversationId: 'conv-010',
        senderRole: 'Agent',
        senderName: MOCK_AGENT.name,
        body: 'Our warranty covers manufacturing defects for 12 months. Physical damage is not covered. Could you describe what happened?',
        sentAt: '2026-06-30T09:35:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-010-3',
        conversationId: 'conv-010',
        senderRole: 'Contact',
        senderName: 'Shirin Akter',
        body: 'OK, I understand. Thank you for explaining.',
        sentAt: '2026-06-30T09:38:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
  {
    id: 'conv-011',
    state: 'HumanHandling',
    channel: 'telegram',
    contactName: 'Monirul Islam',
    contactHandle: '@monirul_tg',
    lastMessagePreview: 'Please check the tracking number again.',
    lastMessageAt: '2026-06-30T09:25:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 2,
    assignedAgentName: 'Fatima Khanam',
    tags: ['tracking', 'lost-parcel'],
    messages: [
      {
        id: 'msg-011-1',
        conversationId: 'conv-011',
        senderRole: 'Contact',
        senderName: 'Monirul Islam',
        body: 'My parcel shows delivered but I never received it.',
        sentAt: '2026-06-30T09:20:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-011-2',
        conversationId: 'conv-011',
        senderRole: 'Agent',
        senderName: 'Fatima Khanam',
        body: "I'm so sorry to hear that. I'll escalate this to our logistics team immediately. Can you confirm your delivery address?",
        sentAt: '2026-06-30T09:22:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-011-3',
        conversationId: 'conv-011',
        senderRole: 'Contact',
        senderName: 'Monirul Islam',
        body: 'Please check the tracking number again.',
        sentAt: '2026-06-30T09:25:00.000Z',
        deliveryStatus: 'Delivered',
      },
    ],
  },
  {
    id: 'conv-012',
    state: 'HumanHandling',
    channel: 'web',
    contactName: 'Dilruba Nasrin',
    contactHandle: 'dilruba@business.com',
    lastMessagePreview: "I've sent the invoice. Please confirm receipt.",
    lastMessageAt: '2026-06-30T09:15:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 0,
    assignedAgentName: 'Fatima Khanam',
    tags: ['invoice', 'b2b'],
    messages: [
      {
        id: 'msg-012-1',
        conversationId: 'conv-012',
        senderRole: 'Contact',
        senderName: 'Dilruba Nasrin',
        body: 'Can you send me a consolidated invoice for last month?',
        sentAt: '2026-06-30T09:10:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-012-2',
        conversationId: 'conv-012',
        senderRole: 'Agent',
        senderName: 'Fatima Khanam',
        body: "I've sent the invoice. Please confirm receipt.",
        sentAt: '2026-06-30T09:15:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },

  // ── Resolved × 4 ─────────────────────────────────────────────────────────
  {
    id: 'conv-013',
    state: 'Resolved',
    channel: 'whatsapp',
    contactName: 'Jahangir Kabir',
    contactHandle: '+8801611223344',
    lastMessagePreview: 'Thank you! Issue resolved.',
    lastMessageAt: '2026-06-30T08:55:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['resolved'],
    messages: [
      {
        id: 'msg-013-1',
        conversationId: 'conv-013',
        senderRole: 'Contact',
        senderName: 'Jahangir Kabir',
        body: 'My account was charged twice for order #4421.',
        sentAt: '2026-06-30T08:45:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-013-2',
        conversationId: 'conv-013',
        senderRole: 'Ai',
        body: 'I can see the duplicate charge. A refund of ৳1,200 has been initiated and will reflect within 3–5 business days.',
        sentAt: '2026-06-30T08:48:00.000Z',
        confidence: 0.88,
      },
      {
        id: 'msg-013-3',
        conversationId: 'conv-013',
        senderRole: 'Contact',
        senderName: 'Jahangir Kabir',
        body: 'Thank you! Issue resolved.',
        sentAt: '2026-06-30T08:55:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
  {
    id: 'conv-014',
    state: 'Resolved',
    channel: 'facebook',
    contactName: 'Parveen Sultana',
    contactHandle: 'parveen.sultana.fb',
    lastMessagePreview: 'Great, thanks for the quick help!',
    lastMessageAt: '2026-06-30T08:40:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 0,
    tags: ['exchange'],
    messages: [
      {
        id: 'msg-014-1',
        conversationId: 'conv-014',
        senderRole: 'Contact',
        senderName: 'Parveen Sultana',
        body: 'I received the wrong size. Can I exchange it?',
        sentAt: '2026-06-30T08:30:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-014-2',
        conversationId: 'conv-014',
        senderRole: 'Agent',
        senderName: MOCK_AGENT.name,
        body: 'Absolutely! I have initiated an exchange request. A courier will pick up the item within 24 hours.',
        sentAt: '2026-06-30T08:35:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-014-3',
        conversationId: 'conv-014',
        senderRole: 'Contact',
        senderName: 'Parveen Sultana',
        body: 'Great, thanks for the quick help!',
        sentAt: '2026-06-30T08:40:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
  {
    id: 'conv-015',
    state: 'Resolved',
    channel: 'instagram',
    contactName: 'Milon Chowdhury',
    contactHandle: '@milon.chowdhury',
    lastMessagePreview: 'Got it. Will update my address.',
    lastMessageAt: '2026-06-30T08:20:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['address-change'],
    messages: [
      {
        id: 'msg-015-1',
        conversationId: 'conv-015',
        senderRole: 'Contact',
        senderName: 'Milon Chowdhury',
        body: 'I need to change my delivery address before it ships.',
        sentAt: '2026-06-30T08:15:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-015-2',
        conversationId: 'conv-015',
        senderRole: 'Ai',
        body: "Your order hasn't shipped yet. You can update the address from your account under 'My Orders' → 'Edit Address'.",
        sentAt: '2026-06-30T08:18:00.000Z',
        confidence: 0.91,
      },
      {
        id: 'msg-015-3',
        conversationId: 'conv-015',
        senderRole: 'Contact',
        senderName: 'Milon Chowdhury',
        body: 'Got it. Will update my address.',
        sentAt: '2026-06-30T08:20:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
  {
    id: 'conv-016',
    state: 'Resolved',
    channel: 'web',
    contactName: 'Farhana Yesmin',
    contactHandle: 'farhana@example.com',
    lastMessagePreview: 'Perfect, I can see the discount now.',
    lastMessageAt: '2026-06-30T07:55:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['promo', 'discount'],
    messages: [
      {
        id: 'msg-016-1',
        conversationId: 'conv-016',
        senderRole: 'Contact',
        senderName: 'Farhana Yesmin',
        body: "My promo code isn't working at checkout.",
        sentAt: '2026-06-30T07:50:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-016-2',
        conversationId: 'conv-016',
        senderRole: 'Ai',
        body: "Your promo code EIDSPECIAL requires a minimum order of ৳2,000. Your cart total is ৳1,850 — just ৳150 more and you'll unlock the discount!",
        sentAt: '2026-06-30T07:52:00.000Z',
        confidence: 0.94,
      },
      {
        id: 'msg-016-3',
        conversationId: 'conv-016',
        senderRole: 'Contact',
        senderName: 'Farhana Yesmin',
        body: 'Perfect, I can see the discount now.',
        sentAt: '2026-06-30T07:55:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },

  // ── Closed × 2 ───────────────────────────────────────────────────────────
  {
    id: 'conv-017',
    state: 'Closed',
    channel: 'whatsapp',
    contactName: 'Abul Kashem',
    contactHandle: '+8801944556677',
    lastMessagePreview: 'Confirmed. Order cancelled.',
    lastMessageAt: '2026-06-29T17:30:00.000Z',
    lastMessageFromAi: false,
    unreadCount: 0,
    tags: ['cancellation'],
    messages: [
      {
        id: 'msg-017-1',
        conversationId: 'conv-017',
        senderRole: 'Contact',
        senderName: 'Abul Kashem',
        body: 'I want to cancel my order #3302.',
        sentAt: '2026-06-29T17:20:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-017-2',
        conversationId: 'conv-017',
        senderRole: 'Agent',
        senderName: MOCK_AGENT.name,
        body: 'Confirmed. Order cancelled.',
        sentAt: '2026-06-29T17:30:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
  {
    id: 'conv-018',
    state: 'Closed',
    channel: 'telegram',
    contactName: 'Nasrin Sultana',
    contactHandle: '@nasrin_tg',
    lastMessagePreview: 'All done, thank you!',
    lastMessageAt: '2026-06-29T15:10:00.000Z',
    lastMessageFromAi: true,
    unreadCount: 0,
    tags: ['subscription'],
    messages: [
      {
        id: 'msg-018-1',
        conversationId: 'conv-018',
        senderRole: 'Contact',
        senderName: 'Nasrin Sultana',
        body: 'How do I subscribe to the newsletter?',
        sentAt: '2026-06-29T15:05:00.000Z',
        deliveryStatus: 'Read',
      },
      {
        id: 'msg-018-2',
        conversationId: 'conv-018',
        senderRole: 'Ai',
        body: "I've subscribed your Telegram account to our newsletter. You'll receive weekly deals and updates. Type /unsubscribe anytime to opt out.",
        sentAt: '2026-06-29T15:08:00.000Z',
        confidence: 0.89,
      },
      {
        id: 'msg-018-3',
        conversationId: 'conv-018',
        senderRole: 'Contact',
        senderName: 'Nasrin Sultana',
        body: 'All done, thank you!',
        sentAt: '2026-06-29T15:10:00.000Z',
        deliveryStatus: 'Read',
      },
    ],
  },
];

// ─── Analytics ───────────────────────────────────────────────────────────────
// 30-day daily data, handoff reasons, and agent performance for the analytics page.

const DAYS_30 = Array.from({ length: 30 }, (_, i) => {
  const d = new Date('2026-05-31');
  d.setDate(d.getDate() + i + 1);
  return d.toISOString().slice(0, 10);
});

export const MOCK_ANALYTICS = {
  dailyVolume: DAYS_30.map((date, i) => ({
    date,
    whatsapp: 40 + Math.round(Math.sin(i * 0.4) * 15 + Math.random() * 10),
    facebook: 25 + Math.round(Math.sin(i * 0.3) * 8 + Math.random() * 8),
    instagram: 18 + Math.round(Math.sin(i * 0.5) * 6 + Math.random() * 6),
    telegram: 12 + Math.round(Math.sin(i * 0.6) * 4 + Math.random() * 5),
    web: 20 + Math.round(Math.sin(i * 0.2) * 7 + Math.random() * 7),
  })),

  handoffReasons: [
    { reason: 'Low confidence', count: 142 },
    { reason: 'Negative sentiment', count: 89 },
    { reason: 'Explicit request', count: 61 },
    { reason: 'Tool failure', count: 27 },
    { reason: 'Out-of-scope', count: 44 },
  ],

  confidenceBands: [
    { band: 'High (≥0.8)', count: 1204 },
    { band: 'Medium (0.6–0.8)', count: 387 },
    { band: 'Low (<0.6)', count: 142 },
  ],

  dailyTokenCost: DAYS_30.map((date, i) => ({
    date,
    cost: parseFloat((8.5 + Math.sin(i * 0.3) * 2.1 + Math.random() * 1.5).toFixed(2)),
  })),

  kpis: {
    totalConversations: 1733,
    totalConversationsTrend: 12,
    containmentRate: 82,
    containmentRateTrend: 3,
    handoffRate: 18,
    handoffRateTrend: -3,
    avgResponseTimeMs: 1240,
    avgResponseTimeTrend: -8,
    csat: 4.3,
    csatTrend: 5,
    tokenCostUsd: 247.83,
    tokenCostTrend: -2,
  },

  agentPerformance: [
    {
      id: 'agent-1',
      name: 'Md. Alam Hossain',
      handled: 312,
      resolved: 298,
      avgTimeMs: 240000,
      csat: 4.6,
    },
    {
      id: 'agent-2',
      name: 'Fatima Khanam',
      handled: 287,
      resolved: 271,
      avgTimeMs: 315000,
      csat: 4.4,
    },
    {
      id: 'agent-3',
      name: 'Sumaiya Islam',
      handled: 198,
      resolved: 185,
      avgTimeMs: 278000,
      csat: 4.2,
    },
    {
      id: 'agent-4',
      name: 'Arif Chowdhury',
      handled: 154,
      resolved: 143,
      avgTimeMs: 352000,
      csat: 4.1,
    },
  ],

  lowConfidenceConversations: [
    {
      id: 'conv-006',
      contactName: 'Fatema Khatun',
      channel: 'whatsapp' as Channel,
      confidence: 0.45,
      topic: 'refund',
      lastMessageAt: '2026-06-30T09:58:00.000Z',
    },
    {
      id: 'conv-007',
      contactName: 'Rakib Hasan',
      channel: 'instagram' as Channel,
      confidence: 0.48,
      topic: 'custom-order',
      lastMessageAt: '2026-06-30T09:56:00.000Z',
    },
    {
      id: 'conv-004',
      contactName: 'Karim Uddin',
      channel: 'telegram' as Channel,
      confidence: 0.51,
      topic: 'bulk-order',
      lastMessageAt: '2026-06-30T09:35:00.000Z',
    },
    {
      id: 'conv-008',
      contactName: 'Mehnaz Parvin',
      channel: 'web' as Channel,
      confidence: 0.55,
      topic: 'enterprise-pricing',
      lastMessageAt: '2026-06-30T09:52:00.000Z',
    },
  ],
};

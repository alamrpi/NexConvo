'use client';

import * as React from 'react';
import { motion, useReducedMotion } from 'framer-motion';

interface MotionInProps {
  children: React.ReactNode;
  /** Stagger offset in seconds. */
  delay?: number;
  className?: string;
}

/**
 * Subtle entrance animation (frontend standard S26): ≤300ms, transform/opacity only,
 * and fully disabled under `prefers-reduced-motion`. Keeps pages as server components
 * by isolating the client boundary to this leaf (S6).
 */
export function MotionIn({ children, delay = 0, className }: MotionInProps) {
  const reduce = useReducedMotion();

  return (
    <motion.div
      className={className}
      initial={reduce ? false : { opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, delay, ease: 'easeOut' }}
    >
      {children}
    </motion.div>
  );
}

import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Merge Tailwind class names, resolving conflicts predictably.
 * The single class-composition helper used across the design system (S18).
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

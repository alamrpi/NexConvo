'use client';

import { useEffect } from 'react';

/**
 * Reveals scrollbars only while the user is actively scrolling, project-wide.
 * Adds `is-scrolling` to <html> on any scroll (capture phase catches nested scroll
 * containers too) and removes it after a short idle delay; the CSS in globals.css
 * keeps the thumb transparent otherwise. Renders nothing.
 */
export function AutoHideScrollbar() {
  useEffect(() => {
    const root = document.documentElement;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const onScroll = () => {
      root.classList.add('is-scrolling');
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => root.classList.remove('is-scrolling'), 700);
    };

    const options: AddEventListenerOptions = { capture: true, passive: true };
    window.addEventListener('scroll', onScroll, options);
    return () => {
      window.removeEventListener('scroll', onScroll, options);
      if (timer) clearTimeout(timer);
    };
  }, []);

  return null;
}

import * as React from 'react';
import { Label } from '@/shared/ui/label';

/**
 * Labelled form field with an inline error slot (S11/S27). Shared by the account forms so
 * the label/error markup stays consistent and accessible.
 */
export function Field({
  id,
  label,
  error,
  children,
}: {
  id: string;
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-xs">
        {label}
      </Label>
      {children}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

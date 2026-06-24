'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/dialog';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';

/** Create-role dialog: just the name; the role starts with a Read-only preset (parent supplies it). */
export function NewRoleDialog({
  open,
  onOpenChange,
  onCreate,
  pending,
  error,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (name: string) => void;
  pending: boolean;
  error?: string;
}) {
  const t = useTranslations('settings.roles.newRoleDialog');
  const [name, setName] = React.useState('');

  const close = (value: boolean) => {
    onOpenChange(value);
    if (!value) setName('');
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{t('description')}</DialogDescription>
        </DialogHeader>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (name.trim()) onCreate(name.trim());
          }}
          className="space-y-4"
        >
          <div className="space-y-1.5">
            <label htmlFor="new-role-name" className="text-xs font-medium">
              {t('nameLabel')}
            </label>
            <Input
              id="new-role-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoFocus
              maxLength={100}
            />
            {error && (
              <p role="alert" className="text-xs text-destructive">
                {t(`errors.${error}`)}
              </p>
            )}
          </div>
          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" size="sm">
                {t('cancel')}
              </Button>
            </DialogClose>
            <Button type="submit" size="sm" disabled={pending || !name.trim()}>
              {pending ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  {t('creating')}
                </>
              ) : (
                t('create')
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

import { getTranslations } from 'next-intl/server';
import { Quote } from 'lucide-react';
import { Logo } from '@/shared/ui/logo';

/**
 * The branded left half of the login split-screen. Hidden on mobile by the page
 * layout (S24). Dark indigo gradient + subtle grid; restrained, not noisy (S28).
 */
export async function BrandTestimonialPanel() {
  const [tCommon, tLogin] = await Promise.all([
    getTranslations('common'),
    getTranslations('login'),
  ]);

  return (
    <div className="relative flex h-full flex-col justify-between overflow-hidden bg-zinc-950 p-12 text-zinc-100">
      {/* Brand gradient + pattern. */}
      <div aria-hidden className="pointer-events-none absolute inset-0">
        <div className="absolute inset-0 bg-gradient-to-br from-indigo-600/30 via-zinc-950 to-zinc-950" />
        <div className="absolute -left-24 top-1/3 h-96 w-96 rounded-full bg-indigo-500/20 blur-3xl" />
        <div className="absolute inset-0 bg-[linear-gradient(to_right,rgba(255,255,255,0.04)_1px,transparent_1px),linear-gradient(to_bottom,rgba(255,255,255,0.04)_1px,transparent_1px)] [background-size:32px_32px]" />
      </div>

      <div className="relative">
        <Logo label={tCommon('appName')} className="text-zinc-50" />
      </div>

      <figure className="relative space-y-6">
        <Quote className="h-10 w-10 text-indigo-400" aria-hidden />
        <blockquote className="text-pretty text-2xl font-medium leading-relaxed tracking-tight">
          {tLogin('testimonial.quote')}
        </blockquote>
        <figcaption className="text-sm">
          <span className="font-semibold text-zinc-50">{tLogin('testimonial.author')}</span>
          <span className="block text-zinc-400">{tLogin('testimonial.role')}</span>
        </figcaption>
      </figure>
    </div>
  );
}

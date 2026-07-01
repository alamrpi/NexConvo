import { describe, expect, it } from 'vitest';
import en from './messages/en.json';
import bn from './messages/bn.json';

type Tree = { [key: string]: string | Tree };

/** Recursively collect dotted key paths so we can compare catalog shapes. */
function keyPaths(tree: Tree, prefix = ''): string[] {
  return Object.entries(tree).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === 'string' ? [path] : keyPaths(value, path);
  });
}

/** Resolve a dotted path to its leaf value (or undefined). */
function valueAt(tree: Tree, path: string): string | Tree | undefined {
  return path.split('.').reduce<string | Tree | undefined>((node, key) => {
    if (node && typeof node === 'object') return node[key];
    return undefined;
  }, tree);
}

describe('i18n catalog parity', () => {
  it('en and bn expose exactly the same keys (Bengali is first-class — S12)', () => {
    const enKeys = keyPaths(en as Tree).sort();
    const bnKeys = keyPaths(bn as Tree).sort();
    expect(bnKeys).toEqual(enKeys);
  });

  it('has no empty translated values', () => {
    for (const [locale, tree] of [
      ['en', en],
      ['bn', bn],
    ] as const) {
      const empties = keyPaths(tree as Tree).filter((path) => {
        const value = valueAt(tree as Tree, path);
        return typeof value === 'string' && value.trim() === '';
      });
      expect(empties, `empty values in ${locale}`).toEqual([]);
    }
  });
});

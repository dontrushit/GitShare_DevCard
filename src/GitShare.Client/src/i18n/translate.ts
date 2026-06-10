import type { AppLocale } from './types';
import { messages } from './messages';

export type Translator = ReturnType<typeof createTranslator>;

type MessageTree = (typeof messages)[AppLocale];

function getNestedValue(tree: MessageTree, path: string): string | undefined {
  const value = path.split('.').reduce<unknown>((node, key) => {
    if (node && typeof node === 'object' && key in node) {
      return (node as Record<string, unknown>)[key];
    }
    return undefined;
  }, tree);

  return typeof value === 'string' ? value : undefined;
}

export function createTranslator(locale: AppLocale) {
  const tree = messages[locale];

  return function t(key: string, params?: Record<string, string | number>): string {
    let text = getNestedValue(tree, key) ?? getNestedValue(messages.en, key) ?? key;

    if (params) {
      for (const [name, value] of Object.entries(params)) {
        text = text.replaceAll(`{{${name}}}`, String(value));
      }
    }

    return text;
  };
}

export function formatLanguageCount(locale: AppLocale, count: number): string {
  if (locale === 'en') {
    return count === 1 ? `${count} language` : `${count} languages`;
  }

  const mod10 = count % 10;
  const mod100 = count % 100;
  if (mod10 === 1 && mod100 !== 11) {
    return `${count} язык`;
  }
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) {
    return `${count} языка`;
  }
  return `${count} языков`;
}

// @vitest-environment node
import { afterEach, describe, expect, it } from 'vitest';
import { publicEnv, serverEnv } from './env';

describe('publicEnv', () => {
  it('exposes safe defaults for browser-visible values', () => {
    expect(publicEnv.NEXT_PUBLIC_APP_NAME).toBeTruthy();
    expect(publicEnv.NEXT_PUBLIC_APP_URL).toMatch(/^https?:\/\//);
  });
});

describe('serverEnv', () => {
  const original = process.env.API_GATEWAY_URL;
  afterEach(() => {
    process.env.API_GATEWAY_URL = original;
  });

  it('parses a valid gateway url', () => {
    process.env.API_GATEWAY_URL = 'http://gateway:8080';
    expect(serverEnv().API_GATEWAY_URL).toBe('http://gateway:8080');
  });

  it('throws when the gateway url is missing', () => {
    delete process.env.API_GATEWAY_URL;
    expect(() => serverEnv()).toThrow();
  });

  it('throws when the gateway url is not a url', () => {
    process.env.API_GATEWAY_URL = 'not-a-url';
    expect(() => serverEnv()).toThrow();
  });
});

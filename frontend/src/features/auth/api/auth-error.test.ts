import { describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { classifyAuthError } from './auth-error';

function axiosErrorWithStatus(status: number): AxiosError {
  const err = new AxiosError('failed', 'ERR_BAD_RESPONSE');
  err.response = {
    status,
    statusText: '',
    data: {},
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return err;
}

describe('classifyAuthError', () => {
  it('classifies a no-response error as network', () => {
    const err = new AxiosError('Network Error', 'ERR_NETWORK');
    expect(classifyAuthError(err)).toEqual({ status: null, code: 'network' });
  });

  it('classifies a 5xx response as server', () => {
    expect(classifyAuthError(axiosErrorWithStatus(502))).toEqual({ status: 502, code: 'server' });
  });

  it('classifies a 4xx response as generic (specific codes handled by callers)', () => {
    expect(classifyAuthError(axiosErrorWithStatus(409))).toEqual({ status: 409, code: 'generic' });
    expect(classifyAuthError(axiosErrorWithStatus(401))).toEqual({ status: 401, code: 'generic' });
  });

  it('classifies a non-axios error as generic', () => {
    expect(classifyAuthError(new Error('boom'))).toEqual({ status: null, code: 'generic' });
  });
});
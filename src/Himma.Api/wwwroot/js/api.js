import { t, lang } from './i18n/index.js';
// Every network call goes through here.
// The access token lives only in memory (never localStorage). The refresh token is an
// HttpOnly cookie the browser sends to /api/auth automatically; JavaScript never sees it.

let accessToken = null;
let refreshInFlight = null;
let onExpired = () => {};

export class ApiError extends Error {
  constructor(status, problem) {
    super(problem?.detail || problem?.title || (status === 0 ? 'network' : `HTTP ${status}`));
    this.status = status;
    this.title = problem?.title ?? null;
    this.detail = problem?.detail ?? null;
    this.errors = problem?.errors ?? {};
    this.problem = problem ?? null; // extra fields, e.g. takenAtUtc on a 409
  }

  get isNetwork() { return this.status === 0; }
}

export function onSessionExpired(handler) { onExpired = handler; }
export function setSession(auth) { accessToken = auth?.accessToken ?? null; }
export function clearSession() { accessToken = null; }

/**
 * Asks the server for a new access token using the refresh cookie.
 * Returns the auth response, or null when there is no valid session.
 * Concurrent callers share one request, so the cookie is rotated only once.
 */
export function refreshSession() {
  refreshInFlight ??= (async () => {
    try {
      const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Accept-Language': lang },
      });
      if (!response.ok) {
        accessToken = null;
        return null;
      }
      const auth = await response.json();
      accessToken = auth.accessToken;
      return auth;
    } catch {
      throw new ApiError(0, null);
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

export async function api(path, { method = 'GET', body, retry = true } = {}) {
  // The server answers (messages, food and exercise names) in the user's language.
  const headers = { Accept: 'application/json', 'Accept-Language': lang };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  let response;
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: 'same-origin',
    });
  } catch {
    throw new ApiError(0, null);
  }

  // Access token expired: refresh silently once and repeat the request.
  if (response.status === 401 && retry && !path.startsWith('/auth/')) {
    const session = await refreshSession();
    if (session) return api(path, { method, body, retry: false });
    onExpired();
    throw new ApiError(401, { title: t('انتهت الجلسة') });
  }

  if (response.status === 204) return null;
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(response.status, data);
  return data;
}

export const get = (path) => api(path);
export const post = (path, body) => api(path, { method: 'POST', body });
export const put = (path, body) => api(path, { method: 'PUT', body });
export const del = (path) => api(path, { method: 'DELETE' });

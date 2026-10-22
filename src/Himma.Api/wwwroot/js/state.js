// Small shared state plus a tiny event bus, so screens can react to changes made elsewhere
// (for example: water logged from the reminder banner while the Today screen is open).

export const state = {
  user: null, // { id, username, displayName, roles }
};

const listeners = new Map();

export function on(event, handler) {
  if (!listeners.has(event)) listeners.set(event, new Set());
  listeners.get(event).add(handler);
  return () => listeners.get(event)?.delete(handler);
}

export function emit(event, data) {
  listeners.get(event)?.forEach((handler) => handler(data));
}

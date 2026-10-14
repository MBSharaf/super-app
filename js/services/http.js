/**
 * Mock transport. Every service call goes through `request`, which adds latency and returns a
 * deep copy — the same contract a fetch() wrapper would give. To go live, replace `request(handler)`
 * with `fetch(API_BASE + path)` inside each service; the UI does not change.
 */
(function (SA) {
  const { sleep } = SA.core.utils;
  const [lo, hi] = SA.config.latency;

  class ApiError extends Error {
    constructor(message, code = 'error', details) { super(message); this.code = code; this.details = details; }
  }

  const request = async (handler, { latency } = {}) => {
    await sleep(latency ?? lo + Math.random() * (hi - lo));
    const out = await handler();
    return out === undefined ? out : structuredClone(out);
  };

  SA.services.http = { request, ApiError };
})(window.SA);

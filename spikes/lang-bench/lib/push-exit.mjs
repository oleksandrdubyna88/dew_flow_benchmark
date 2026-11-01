// Exit codes of the pre-push check: 0 clean, 1 refused, 3 the environment (git, the network, an unreadable aid), 4 configuration.
export const EXIT = Object.freeze({ PASS: 0, REFUSED: 1, ENVIRONMENT: 3, CONFIG: 4 });

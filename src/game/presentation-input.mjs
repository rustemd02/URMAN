function requiredString(value, label) {
  if (typeof value !== 'string' || !value) throw new TypeError(`${label} must be a non-empty string.`);
  return value;
}

/** Maps a resolved generic presentation action to the provider's typed input. */
export function presentationInputFor(kind, actionId) {
  requiredString(actionId, 'Presentation action ID');
  if (kind === 'dialogue') return Object.freeze({ type: 'dialogue.choose', choiceId: actionId });
  if (kind === 'route') return Object.freeze({ type: 'route.choose', interactionId: actionId });
  return Object.freeze({ type: 'scene.interact', interactionId: actionId });
}

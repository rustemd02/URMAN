/**
 * The legacy direct Playwright smoke runner was retired by MM-60. This is a
 * declarative scenario consumed by the built-in node:test E2E suite, so there
 * is no host-specific browser path, production global or storage reset here.
 */
export const DEFAULT_MVP_JOURNEY = Object.freeze([
  'urman.chapter1:interaction/arrival-enter-house',
  'urman.chapter1:interaction/house-to-route',
  'urman.chapter1:interaction/route-to-fap',
  'urman.chapter1:interaction/fap-to-document-desk',
  'urman.chapter1:interaction/fap-document-desk-to-official-record',
  'urman.chapter1:interaction/official-to-internal-register',
  'urman.chapter1:interaction/internal-register-to-rinat',
  'urman.chapter1:interaction/internal-register-to-saved-message',
  'urman.chapter1:interaction/saved-message-to-boundary-source',
  'urman.chapter1:interaction/boundary-source-to-reread',
  'urman.chapter1:interaction/reread-to-edge-sketch',
  'urman.chapter1:interaction/edge-sketch-to-zirat-road',
  'urman.chapter1:interaction/zirat-road-to-forest',
]);

export const FINAL_RULE_ID = 'urman.chapter1:knowledge/clue_do_not_answer_rule';

export const JOURNEY_DIALOGUE_CONTINUE_AFTER = Object.freeze([
  'urman.chapter1:interaction/internal-register-to-rinat',
]);

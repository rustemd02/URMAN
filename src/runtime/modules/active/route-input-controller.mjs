import { activeProviderError, ActiveProviderErrorCode } from './active-provider-errors.mjs';

/** Maps host input into a single content-declared route action. */
export class RouteInputController {
  action(input) {
    if (!input || typeof input !== 'object' || input.type !== 'route.choose' || typeof input.interactionId !== 'string') {
      throw activeProviderError(ActiveProviderErrorCode.InvalidInput, 'Route input must be route.choose with interactionId.');
    }
    return input.interactionId;
  }
}

import { initOldPcHub, OldPcHubController, renderOldPcHub } from './oldPcHub';

export const renderBrowser = () => renderOldPcHub();

export const initBrowser = (root: HTMLElement, controller: OldPcHubController) => initOldPcHub(root, controller);
